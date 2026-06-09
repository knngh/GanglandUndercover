#!/usr/bin/env bash
# Task#6 真·云 Relay 双进程端到端联调编排脚本。
#
# 两个 Unity 批处理进程不能共享同一个 project 的 Library，故为 Client 端
# 建一个兄弟目录 clone，用符号链接复用 Assets/Packages/ProjectSettings，
# 各自拥有独立的 Library。两端通过 /tmp 下的共享文件交换 Relay 房间码。
#
#   进程A（主工程，role=host）   : 创建 Relay 房间 → 写房间码 → 等 Client 连入
#   进程B（clone 工程，role=client）: 读房间码 → 加入 → 断言连上
#
# 用法： bash run-relay-twoprocess.sh
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity}"
MAIN="${MAIN:-$SCRIPT_DIR}"
CLONE="${GANGLAND_RELAY_CLIENT_PROJECT:-${MAIN}_relayclient}"
CODEFILE="${GANGLAND_RELAY_CODEFILE:-/tmp/gangland-relay-code.txt}"

HOST_LOG="$MAIN/Logs/relay-host.log"
CLIENT_LOG="$CLONE/Logs/relay-client.log"
HOST_XML="$MAIN/Logs/relay-host-results.xml"
CLIENT_XML="$CLONE/Logs/relay-client-results.xml"
TIMEOUT_MARK="$MAIN/Logs/relay-timeout.txt"
RELAY_TIMEOUT_SECONDS="${GANGLAND_RELAY_TIMEOUT_SECONDS:-600}"
RELAY_KILL_GRACE_SECONDS="${GANGLAND_RELAY_KILL_GRACE_SECONDS:-8}"

HOST_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayHost_PublishesCodeAndAcceptsPeer"
CLIENT_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayClient_JoinsHostByCode"

WATCHDOG_PID=""
HOST_PID=""
CLIENT_PID=""

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
  sleep "$RELAY_KILL_GRACE_SECONDS"
  terminate_tree "$HOST_PID" KILL
  terminate_tree "$CLIENT_PID" KILL
}

trap 'cleanup_on_signal; exit 130' INT
trap 'cleanup_on_signal; exit 143' TERM

echo "=== 准备 Client 端 clone 工程（符号链接复用源码/包/设置）==="
rm -f "$CODEFILE" "$CODEFILE.tmp" "$TIMEOUT_MARK"
mkdir -p "$CLONE"
mkdir -p "$MAIN/Logs" "$CLONE/Logs"
for d in Assets Packages ProjectSettings; do
  if [ ! -e "$CLONE/$d" ]; then
    ln -s "$MAIN/$d" "$CLONE/$d"
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

echo "=== 启动 Host（主工程）与 Client（clone）两进程 ==="
rm -f "$MAIN/Logs/relay-host-result.txt" "$CLONE/Logs/relay-client-result.txt" \
      "$HOST_LOG" "$CLIENT_LOG" "$HOST_XML" "$CLIENT_XML"

echo "Unity: $UNITY"
echo "Main:  $MAIN"
echo "Clone: $CLONE"
echo "Code:  $CODEFILE"
echo "Timeout: ${RELAY_TIMEOUT_SECONDS}s"

GANGLAND_RELAY_ROLE=host GANGLAND_RELAY_CODEFILE="$CODEFILE" \
  "$UNITY" -runTests -batchmode -nographics \
    -projectPath "$MAIN" \
    -testPlatform PlayMode \
    -testFilter "$HOST_TEST" \
    -testResults "$HOST_XML" \
    -logFile "$HOST_LOG" \
    -accept-apiupdate &
HOST_PID=$!

# 让 Host 先抢到 license 与编译，再启动 Client（避免两进程同时首次导入争用）。
sleep 20

GANGLAND_RELAY_ROLE=client GANGLAND_RELAY_CODEFILE="$CODEFILE" \
  "$UNITY" -runTests -batchmode -nographics \
    -projectPath "$CLONE" \
    -testPlatform PlayMode \
    -testFilter "$CLIENT_TEST" \
    -testResults "$CLIENT_XML" \
    -logFile "$CLIENT_LOG" \
    -accept-apiupdate &
CLIENT_PID=$!

(
  sleep "$RELAY_TIMEOUT_SECONDS"
  echo "Relay two-process run exceeded ${RELAY_TIMEOUT_SECONDS}s." > "$TIMEOUT_MARK"
  echo "=== Relay run timed out after ${RELAY_TIMEOUT_SECONDS}s; terminating Unity processes ==="
  terminate_tree "$HOST_PID" TERM
  terminate_tree "$CLIENT_PID" TERM
  sleep "$RELAY_KILL_GRACE_SECONDS"
  terminate_tree "$HOST_PID" KILL
  terminate_tree "$CLIENT_PID" KILL
) &
WATCHDOG_PID=$!

wait $HOST_PID;   HOST_EXIT=$?
wait $CLIENT_PID; CLIENT_EXIT=$?
stop_watchdog

echo "=== 结果 ==="
echo "HOST_EXIT=$HOST_EXIT  CLIENT_EXIT=$CLIENT_EXIT"
echo "--- host result ---";   cat "$MAIN/Logs/relay-host-result.txt"   2>/dev/null || echo "(无 host 结果文件)"
echo "--- client result ---"; cat "$CLONE/Logs/relay-client-result.txt" 2>/dev/null || echo "(无 client 结果文件)"
echo "--- logs ---"
echo "host log:   $HOST_LOG"
echo "client log: $CLIENT_LOG"
echo "host xml:   $HOST_XML"
echo "client xml: $CLIENT_XML"

if [ -f "$TIMEOUT_MARK" ]; then
  echo "--- timeout ---"
  cat "$TIMEOUT_MARK" || true
  print_diagnostics
  exit 124
fi

if [ "$HOST_EXIT" -ne 0 ] || [ "$CLIENT_EXIT" -ne 0 ]; then
  print_diagnostics
  exit 1
fi
