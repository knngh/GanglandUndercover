#!/usr/bin/env bash
# Task#6 真·云 Relay 双进程端到端联调编排脚本。
#
# 两个 Unity 批处理进程不能共享同一个 project 的 Library，故为 Client 端
# 建一个兄弟目录 clone，用符号链接复用 Assets/Packages/ProjectSettings，
# 各自拥有独立的 Library。两端通过 /tmp 下的共享文件交换 Relay 房间码。
#
#   normal:
#     进程A（主工程，role=host）   : 创建 Relay 房间 → 写房间码 → 等 Client 连入
#     进程B（clone 工程，role=client）: 读房间码 → 加入 → 断言连上
#   migration:
#     进程A（主工程，role=migration-host）   : 创建旧 Relay → 等新 Relay 码 → 断开旧 Host → 重连新 Relay
#     进程B（clone 工程，role=migration-client）: 加入旧 Relay → 接管为新 Relay Host → 写新码 → 等旧端重连
#   migration-threeclient:
#     进程A（主工程，role=migration-host-threeclient）     : 创建旧 Relay → 等两端 Client → 重连新 Relay
#     进程B（clone 工程，role=migration-candidate-threeclient）: 加入旧 Relay → 接管新 Relay → 跑迁移后连续性断言
#     进程C（第二 clone，role=migration-observer-threeclient） : 加入旧 Relay → 跟随重连新 Relay
#
# 用法： bash run-relay-twoprocess.sh
#      GANGLAND_RELAY_SCENARIO=migration bash run-relay-twoprocess.sh
#      GANGLAND_RELAY_SCENARIO=migration-threeclient bash run-relay-twoprocess.sh
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity}"
MAIN="${MAIN:-$SCRIPT_DIR}"
CLONE="${GANGLAND_RELAY_CLIENT_PROJECT:-${MAIN}_relayclient}"
OBSERVER_CLONE="${GANGLAND_RELAY_OBSERVER_PROJECT:-${MAIN}_relayobserver}"
CODEFILE="${GANGLAND_RELAY_CODEFILE:-/tmp/gangland-relay-code.txt}"
SCENARIO="${GANGLAND_RELAY_SCENARIO:-normal}"

HOST_LOG="$MAIN/Logs/relay-host.log"
CLIENT_LOG="$CLONE/Logs/relay-client.log"
OBSERVER_LOG="$OBSERVER_CLONE/Logs/relay-observer.log"
HOST_XML="$MAIN/Logs/relay-host-results.xml"
CLIENT_XML="$CLONE/Logs/relay-client-results.xml"
OBSERVER_XML="$OBSERVER_CLONE/Logs/relay-observer-results.xml"
TIMEOUT_MARK="$MAIN/Logs/relay-timeout.txt"
RELAY_TIMEOUT_SECONDS="${GANGLAND_RELAY_TIMEOUT_SECONDS:-600}"
RELAY_KILL_GRACE_SECONDS="${GANGLAND_RELAY_KILL_GRACE_SECONDS:-8}"
CLIENT_START_TIMEOUT_SECONDS="${GANGLAND_RELAY_CLIENT_START_TIMEOUT_SECONDS:-300}"

THREEPROCESS=false
OBSERVER_ROLE=""
OBSERVER_TEST=""

if [ "$SCENARIO" = "migration-threeclient" ]; then
  THREEPROCESS=true
  HOST_ROLE="migration-host-threeclient"
  CLIENT_ROLE="migration-candidate-threeclient"
  OBSERVER_ROLE="migration-observer-threeclient"
  HOST_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayMigration_ThreeClientOldHostReconnectsToReplacementRelay"
  CLIENT_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayMigration_ThreeClientCandidatePromotesAndRunsPostRestoreFlow"
  OBSERVER_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayMigration_ThreeClientObserverFollowsReplacementRelay"
elif [ "$SCENARIO" = "migration" ]; then
  HOST_ROLE="migration-host"
  CLIENT_ROLE="migration-client"
  HOST_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayMigration_OldHostReconnectsToReplacementRelay"
  CLIENT_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayMigration_ClientPromotesToReplacementRelayHost"
else
  HOST_ROLE="host"
  CLIENT_ROLE="client"
  HOST_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayHost_PublishesCodeAndAcceptsPeer"
  CLIENT_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayClient_JoinsHostByCode"
fi

# Keep scenario-specific NUnit XML so a later migration run cannot overwrite
# the normal baseline evidence. The generic names remain for normal runs and
# for compatibility with existing tooling.
if [ "$SCENARIO" != "normal" ]; then
  HOST_XML="$MAIN/Logs/relay-${SCENARIO}-host-results.xml"
  CLIENT_XML="$CLONE/Logs/relay-${SCENARIO}-client-results.xml"
  OBSERVER_XML="$OBSERVER_CLONE/Logs/relay-${SCENARIO}-observer-results.xml"
fi
HOST_RESULT="$MAIN/Logs/relay-$HOST_ROLE-result.txt"
CLIENT_RESULT="$CLONE/Logs/relay-$CLIENT_ROLE-result.txt"
OBSERVER_RESULT="$OBSERVER_CLONE/Logs/relay-${OBSERVER_ROLE:-observer}-result.txt"

WATCHDOG_PID=""
HOST_PID=""
CLIENT_PID=""
OBSERVER_PID=""

terminate_tree() {
  local pid="$1"
  local signal="$2"

  if [ -z "$pid" ]; then
    return 0
  fi

  # Kill Unity children first. Unity batchmode can leave UPM/licensing children
  # alive after the editor process stops responding.
  pkill "-$signal" -P "$pid" 2>/dev/null || true
  kill "-$signal" "$pid" 2>/dev/null || true
}

print_log_summary() {
  local label="$1"
  local file="$2"

  echo "--- $label key lines ---"
  if [ -f "$file" ]; then
    grep -E "RelayTest|Licensing|Timed-out|headless|Unity\\.Licensing|Test results|Aborting|Exception|Error:" "$file" 2>/dev/null | tail -n 80 || true
  else
    echo "(missing: $file)"
  fi

  echo "--- $label tail ---"
  if [ -f "$file" ]; then
    tail -n 80 "$file" || true
  else
    echo "(missing: $file)"
  fi
}

print_diagnostics() {
  echo "--- diagnostics ---"
  if [ -f "$CODEFILE" ]; then
    echo "code file exists: $CODEFILE"
    cat "$CODEFILE" || true
  else
    echo "code file missing: $CODEFILE"
  fi

  if [ -f "$HOST_XML" ]; then
    echo "host xml exists: $HOST_XML"
  else
    echo "host xml missing: $HOST_XML"
  fi

  if [ -f "$CLIENT_XML" ]; then
    echo "client xml exists: $CLIENT_XML"
  else
    echo "client xml missing: $CLIENT_XML"
  fi

  print_log_summary "host" "$HOST_LOG"
  print_log_summary "client" "$CLIENT_LOG"
  if [ "$THREEPROCESS" = "true" ]; then
    if [ -f "$OBSERVER_XML" ]; then
      echo "observer xml exists: $OBSERVER_XML"
    else
      echo "observer xml missing: $OBSERVER_XML"
    fi
    print_log_summary "observer" "$OBSERVER_LOG"
  fi
}

stop_watchdog() {
  if [ -n "$WATCHDOG_PID" ]; then
    kill "$WATCHDOG_PID" 2>/dev/null || true
    wait "$WATCHDOG_PID" 2>/dev/null || true
    WATCHDOG_PID=""
  fi
}

cleanup_on_signal() {
  stop_watchdog
  terminate_tree "$HOST_PID" TERM
  terminate_tree "$CLIENT_PID" TERM
  terminate_tree "$OBSERVER_PID" TERM
  sleep "$RELAY_KILL_GRACE_SECONDS"
  terminate_tree "$HOST_PID" KILL
  terminate_tree "$CLIENT_PID" KILL
  terminate_tree "$OBSERVER_PID" KILL
}

trap 'cleanup_on_signal; exit 130' INT
trap 'cleanup_on_signal; exit 143' TERM

echo "=== 准备 Client 端 clone 工程（符号链接复用源码/包/设置）==="
rm -f "$CODEFILE" "$CODEFILE.tmp" "$CODEFILE.malicious" "$CODEFILE.malicious.tmp" \
      "$CODEFILE.migration" "$CODEFILE.migration.tmp" \
      "$CODEFILE.candidate-old" "$CODEFILE.candidate-old.tmp" \
      "$CODEFILE.observer-old" "$CODEFILE.observer-old.tmp" \
      "$CODEFILE.observer-new" "$CODEFILE.observer-new.tmp" \
      "$CODEFILE.oldhost-reconnected" "$CODEFILE.oldhost-reconnected.tmp" \
      "$CODEFILE.remote-task" "$CODEFILE.remote-task.tmp" \
      "$CODEFILE.remote-task-submitted" "$CODEFILE.remote-task-submitted.tmp" \
      "$CODEFILE.remote-vote" "$CODEFILE.remote-vote.tmp" \
      "$CODEFILE.oldhost-vote-submitted" "$CODEFILE.oldhost-vote-submitted.tmp" \
      "$CODEFILE.observer-vote-submitted" "$CODEFILE.observer-vote-submitted.tmp" \
      "$CODEFILE.camera-legal-ready" "$CODEFILE.camera-legal-ready.tmp" \
      "$CODEFILE.camera-data-received" "$CODEFILE.camera-data-received.tmp" \
      "$TIMEOUT_MARK"
mkdir -p "$CLONE"
if [ "$THREEPROCESS" = "true" ]; then
  mkdir -p "$OBSERVER_CLONE"
fi
mkdir -p "$MAIN/Logs" "$CLONE/Logs"
if [ "$THREEPROCESS" = "true" ]; then
  mkdir -p "$OBSERVER_CLONE/Logs"
fi
for d in Assets Packages ProjectSettings; do
  if [ ! -e "$CLONE/$d" ]; then
    ln -s "$MAIN/$d" "$CLONE/$d"
  fi
  if [ "$THREEPROCESS" = "true" ] && [ ! -e "$OBSERVER_CLONE/$d" ]; then
    ln -s "$MAIN/$d" "$OBSERVER_CLONE/$d"
  fi
done

# 预热：让 clone 工程先完整导入并编译一次（首次冷导入很慢，且会出现 shadergraph
# 半导入报错）。预热后其 Library 就绪，正式联调时 Client 能快速进入 PlayMode，
# 不会因冷启动拖到 Host 的 Relay 分配过期。
if [ ! -d "$CLONE/Library/ScriptAssemblies" ]; then
  echo "=== 预热 clone 工程 Library（一次性，可能数分钟）==="
  "$UNITY" -quit -batchmode -nographics \
    -projectPath "$CLONE" \
    -logFile "$CLONE/Logs/relay-client-warmup.log" \
    -accept-apiupdate
  echo "预热完成 exit=$?"
fi
if [ "$THREEPROCESS" = "true" ] && [ ! -d "$OBSERVER_CLONE/Library/ScriptAssemblies" ]; then
  echo "=== 预热 observer clone 工程 Library（一次性，可能数分钟）==="
  "$UNITY" -quit -batchmode -nographics \
    -projectPath "$OBSERVER_CLONE" \
    -logFile "$OBSERVER_CLONE/Logs/relay-observer-warmup.log" \
    -accept-apiupdate
  echo "observer 预热完成 exit=$?"
fi

echo "=== 启动 Host（主工程）与 Client（clone）两进程 ==="
rm -f "$MAIN/Logs/relay-host-result.txt" "$CLONE/Logs/relay-client-result.txt" \
      "$MAIN/Logs/relay-migration-host-result.txt" "$CLONE/Logs/relay-migration-client-result.txt" \
      "$MAIN/Logs/relay-migration-host-threeclient-result.txt" \
      "$CLONE/Logs/relay-migration-candidate-threeclient-result.txt" \
      "$OBSERVER_CLONE/Logs/relay-migration-observer-threeclient-result.txt" \
      "$HOST_LOG" "$CLIENT_LOG" "$HOST_XML" "$CLIENT_XML"
if [ "$THREEPROCESS" = "true" ]; then
  rm -f "$OBSERVER_LOG" "$OBSERVER_XML"
fi

echo "Unity: $UNITY"
echo "Main:  $MAIN"
echo "Clone: $CLONE"
if [ "$THREEPROCESS" = "true" ]; then
  echo "Observer clone: $OBSERVER_CLONE"
fi
echo "Code:  $CODEFILE"
echo "Scenario: $SCENARIO"
echo "Timeout: ${RELAY_TIMEOUT_SECONDS}s"
echo "Client start wait: ${CLIENT_START_TIMEOUT_SECONDS}s"

GANGLAND_RELAY_ROLE="$HOST_ROLE" GANGLAND_RELAY_CODEFILE="$CODEFILE" \
  "$UNITY" -runTests -batchmode -nographics \
    -projectPath "$MAIN" \
    -testPlatform PlayMode \
    -testFilter "$HOST_TEST" \
    -testResults "$HOST_XML" \
    -logFile "$HOST_LOG" \
    -accept-apiupdate &
HOST_PID=$!

(
  sleep "$RELAY_TIMEOUT_SECONDS"
  echo "Relay two-process run exceeded ${RELAY_TIMEOUT_SECONDS}s." > "$TIMEOUT_MARK"
  echo "=== Relay run timed out after ${RELAY_TIMEOUT_SECONDS}s; terminating Unity processes ==="
  terminate_tree "$HOST_PID" TERM
  terminate_tree "$CLIENT_PID" TERM
  terminate_tree "$OBSERVER_PID" TERM
  sleep "$RELAY_KILL_GRACE_SECONDS"
  terminate_tree "$HOST_PID" KILL
  terminate_tree "$CLIENT_PID" KILL
  terminate_tree "$OBSERVER_PID" KILL
) &
WATCHDOG_PID=$!

# 等 Host 真正进入测试体并写出 Relay 码后再启动 Client，避免两个 Unity
# 批处理进程同时抢 Licensing/UPM。Host 若卡在 license 或 Relay 创建阶段，
# 这里会提前给出 Host 侧诊断，而不是再启动 Client 放大噪音。
echo "=== 等待 Host 写出 Relay 房间码后启动 Client ==="
client_wait_elapsed=0
while [ "$client_wait_elapsed" -lt "$CLIENT_START_TIMEOUT_SECONDS" ]; do
  if [ -s "$CODEFILE" ]; then
    echo "Host code ready after ${client_wait_elapsed}s."
    break
  fi

  if ! kill -0 "$HOST_PID" 2>/dev/null; then
    wait "$HOST_PID"; HOST_EXIT=$?
    CLIENT_EXIT=127
    stop_watchdog
    echo "Host exited before writing Relay code."
    echo "=== 结果 ==="
    echo "HOST_EXIT=$HOST_EXIT  CLIENT_EXIT=$CLIENT_EXIT"
    echo "--- host result ---";   cat "$HOST_RESULT"   2>/dev/null || echo "(无 host 结果文件: $HOST_RESULT)"
    echo "--- client result ---"; echo "(Client 未启动，等待 Host 码文件失败)"
    echo "--- logs ---"
    echo "host log:   $HOST_LOG"
    echo "client log: $CLIENT_LOG"
    echo "host xml:   $HOST_XML"
    echo "client xml: $CLIENT_XML"
    print_diagnostics
    exit 1
  fi

  if [ -f "$TIMEOUT_MARK" ]; then
    break
  fi

  sleep 2
  client_wait_elapsed=$((client_wait_elapsed + 2))
done

if [ ! -s "$CODEFILE" ]; then
  echo "Host did not write Relay code within ${CLIENT_START_TIMEOUT_SECONDS}s."
  terminate_tree "$HOST_PID" TERM
  sleep "$RELAY_KILL_GRACE_SECONDS"
  terminate_tree "$HOST_PID" KILL
  wait "$HOST_PID"; HOST_EXIT=$?
  CLIENT_EXIT=127
  stop_watchdog
  echo "=== 结果 ==="
  echo "HOST_EXIT=$HOST_EXIT  CLIENT_EXIT=$CLIENT_EXIT"
  echo "--- host result ---";   cat "$HOST_RESULT"   2>/dev/null || echo "(无 host 结果文件: $HOST_RESULT)"
  echo "--- client result ---"; echo "(Client 未启动，Host 未写出码文件)"
  echo "--- logs ---"
  echo "host log:   $HOST_LOG"
  echo "client log: $CLIENT_LOG"
  echo "host xml:   $HOST_XML"
  echo "client xml: $CLIENT_XML"
  print_diagnostics
  exit 1
fi

GANGLAND_RELAY_ROLE="$CLIENT_ROLE" GANGLAND_RELAY_CODEFILE="$CODEFILE" \
  "$UNITY" -runTests -batchmode -nographics \
    -projectPath "$CLONE" \
    -testPlatform PlayMode \
    -testFilter "$CLIENT_TEST" \
    -testResults "$CLIENT_XML" \
    -logFile "$CLIENT_LOG" \
    -accept-apiupdate &
CLIENT_PID=$!

if [ "$THREEPROCESS" = "true" ]; then
  echo "=== 等待 candidate 连入旧 Relay 后启动 observer ==="
  candidate_wait_elapsed=0
  while [ "$candidate_wait_elapsed" -lt "$CLIENT_START_TIMEOUT_SECONDS" ]; do
    if [ -s "$CODEFILE.candidate-old" ]; then
      echo "Candidate old-relay marker ready after ${candidate_wait_elapsed}s."
      break
    fi

    if ! kill -0 "$CLIENT_PID" 2>/dev/null; then
      wait "$CLIENT_PID"; CLIENT_EXIT=$?
      HOST_EXIT=127
      OBSERVER_EXIT=127
      stop_watchdog
      echo "Candidate exited before writing old-relay marker."
      echo "=== 结果 ==="
      echo "HOST_EXIT=$HOST_EXIT  CLIENT_EXIT=$CLIENT_EXIT  OBSERVER_EXIT=$OBSERVER_EXIT"
      echo "--- host result ---";   cat "$HOST_RESULT"   2>/dev/null || echo "(无 host 结果文件: $HOST_RESULT)"
      echo "--- client result ---"; cat "$CLIENT_RESULT" 2>/dev/null || echo "(无 client 结果文件: $CLIENT_RESULT)"
      echo "--- observer result ---"; echo "(Observer 未启动，等待 Candidate 旧 Relay 标记失败)"
      print_diagnostics
      exit 1
    fi

    if [ -f "$TIMEOUT_MARK" ]; then
      break
    fi

    sleep 2
    candidate_wait_elapsed=$((candidate_wait_elapsed + 2))
  done

  if [ ! -s "$CODEFILE.candidate-old" ]; then
    echo "Candidate did not write old-relay marker within ${CLIENT_START_TIMEOUT_SECONDS}s."
    terminate_tree "$HOST_PID" TERM
    terminate_tree "$CLIENT_PID" TERM
    sleep "$RELAY_KILL_GRACE_SECONDS"
    terminate_tree "$HOST_PID" KILL
    terminate_tree "$CLIENT_PID" KILL
    wait "$HOST_PID"; HOST_EXIT=$?
    wait "$CLIENT_PID"; CLIENT_EXIT=$?
    OBSERVER_EXIT=127
    stop_watchdog
    echo "=== 结果 ==="
    echo "HOST_EXIT=$HOST_EXIT  CLIENT_EXIT=$CLIENT_EXIT  OBSERVER_EXIT=$OBSERVER_EXIT"
    echo "--- host result ---";   cat "$HOST_RESULT"   2>/dev/null || echo "(无 host 结果文件: $HOST_RESULT)"
    echo "--- client result ---"; cat "$CLIENT_RESULT" 2>/dev/null || echo "(无 client 结果文件: $CLIENT_RESULT)"
    echo "--- observer result ---"; echo "(Observer 未启动，Candidate 未写出旧 Relay 标记)"
    print_diagnostics
    exit 1
  fi

  GANGLAND_RELAY_ROLE="$OBSERVER_ROLE" GANGLAND_RELAY_CODEFILE="$CODEFILE" \
    "$UNITY" -runTests -batchmode -nographics \
      -projectPath "$OBSERVER_CLONE" \
      -testPlatform PlayMode \
      -testFilter "$OBSERVER_TEST" \
      -testResults "$OBSERVER_XML" \
      -logFile "$OBSERVER_LOG" \
      -accept-apiupdate &
  OBSERVER_PID=$!
fi

wait $HOST_PID;   HOST_EXIT=$?
wait $CLIENT_PID; CLIENT_EXIT=$?
if [ "$THREEPROCESS" = "true" ]; then
  wait $OBSERVER_PID; OBSERVER_EXIT=$?
else
  OBSERVER_EXIT=0
fi
stop_watchdog

echo "=== 结果 ==="
if [ "$THREEPROCESS" = "true" ]; then
  echo "HOST_EXIT=$HOST_EXIT  CLIENT_EXIT=$CLIENT_EXIT  OBSERVER_EXIT=$OBSERVER_EXIT"
else
  echo "HOST_EXIT=$HOST_EXIT  CLIENT_EXIT=$CLIENT_EXIT"
fi
echo "--- host result ---";   cat "$HOST_RESULT"   2>/dev/null || echo "(无 host 结果文件: $HOST_RESULT)"
echo "--- client result ---"; cat "$CLIENT_RESULT" 2>/dev/null || echo "(无 client 结果文件: $CLIENT_RESULT)"
if [ "$THREEPROCESS" = "true" ]; then
  echo "--- observer result ---"; cat "$OBSERVER_RESULT" 2>/dev/null || echo "(无 observer 结果文件: $OBSERVER_RESULT)"
fi
echo "--- logs ---"
echo "host log:   $HOST_LOG"
echo "client log: $CLIENT_LOG"
if [ "$THREEPROCESS" = "true" ]; then
  echo "observer log: $OBSERVER_LOG"
fi
echo "host xml:   $HOST_XML"
echo "client xml: $CLIENT_XML"
if [ "$THREEPROCESS" = "true" ]; then
  echo "observer xml: $OBSERVER_XML"
fi

if [ -f "$TIMEOUT_MARK" ]; then
  echo "--- timeout ---"
  cat "$TIMEOUT_MARK" || true
  print_diagnostics
  exit 124
fi

if [ "$HOST_EXIT" -ne 0 ] || [ "$CLIENT_EXIT" -ne 0 ] || [ "$OBSERVER_EXIT" -ne 0 ]; then
  print_diagnostics
  exit 1
fi
