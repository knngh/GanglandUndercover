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

UNITY="/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity"
MAIN="/Users/zhugehao/projects/GanglandUndercover"
CLONE="/Users/zhugehao/projects/GanglandUndercover_relayclient"
CODEFILE="/tmp/gangland-relay-code.txt"

HOST_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayHost_PublishesCodeAndAcceptsPeer"
CLIENT_TEST="GanglandUndercover.PlayTests.RelayTwoProcessPlayTests.RelayClient_JoinsHostByCode"

echo "=== 准备 Client 端 clone 工程（符号链接复用源码/包/设置）==="
rm -f "$CODEFILE" "$CODEFILE.tmp"
mkdir -p "$CLONE"
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
    -logFile "$CLONE/relay-client-warmup.log" \
    -accept-apiupdate
  echo "预热完成 exit=$?"
fi

echo "=== 启动 Host（主工程）与 Client（clone）两进程 ==="
rm -f "$MAIN/Logs/relay-host-result.txt" "$CLONE/Logs/relay-client-result.txt"

GANGLAND_RELAY_ROLE=host GANGLAND_RELAY_CODEFILE="$CODEFILE" \
  "$UNITY" -runTests -batchmode -nographics \
    -projectPath "$MAIN" \
    -testPlatform PlayMode \
    -testFilter "$HOST_TEST" \
    -testResults "$MAIN/relay-host-results.xml" \
    -logFile "$MAIN/relay-host.log" \
    -accept-apiupdate &
HOST_PID=$!

# 让 Host 先抢到 license 与编译，再启动 Client（避免两进程同时首次导入争用）。
sleep 20

GANGLAND_RELAY_ROLE=client GANGLAND_RELAY_CODEFILE="$CODEFILE" \
  "$UNITY" -runTests -batchmode -nographics \
    -projectPath "$CLONE" \
    -testPlatform PlayMode \
    -testFilter "$CLIENT_TEST" \
    -testResults "$CLONE/relay-client-results.xml" \
    -logFile "$CLONE/relay-client.log" \
    -accept-apiupdate &
CLIENT_PID=$!

wait $HOST_PID;   HOST_EXIT=$?
wait $CLIENT_PID; CLIENT_EXIT=$?

echo "=== 结果 ==="
echo "HOST_EXIT=$HOST_EXIT  CLIENT_EXIT=$CLIENT_EXIT"
echo "--- host result ---";   cat "$MAIN/Logs/relay-host-result.txt"   2>/dev/null || echo "(无 host 结果文件)"
echo "--- client result ---"; cat "$CLONE/Logs/relay-client-result.txt" 2>/dev/null || echo "(无 client 结果文件)"
