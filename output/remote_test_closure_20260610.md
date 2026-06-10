# Gangland Undercover — 远程测试闭环记录

> 日期: 2026-06-10
> 目标: 以“今天能和朋友远程测试”为验收目标，确认自动化、手工脚本、构建状态和剩余阻塞。

---

## 1. 结论

今天已完成远程联机前置闭环:

- Relay 双进程真实云服务验证通过。
- EditMode 全量通过。
- PlayMode 常规套件通过，无失败；Relay 两个 PlayMode 用例在常规套件中按设计 ignored，并已由双进程脚本单独验证。
- 朋友手工测试脚本已补齐: `output/friend_remote_test_runbook_20260610.md`。

未完成项:

- 今天的新 macOS FriendTest 构建未生成。原因是 Unity batchmode 构建卡在本机 Unity Licensing Client 重连，不是项目代码或测试失败。

---

## 2. 自动化证据

| 验证项 | 命令/入口 | 结果 | 证据 |
|--------|-----------|------|------|
| Relay 双进程 | `bash run-relay-twoprocess.sh` | PASS | `Logs/relay-host-result.txt`, clone `Logs/relay-client-result.txt` |
| EditMode | Unity `-runTests -testPlatform EditMode` | 83 passed, 0 failed, 0 skipped | `Logs/friendtest-editmode-20260610.xml` |
| PlayMode | Unity `-runTests -testPlatform PlayMode` | 8 passed, 0 failed, 2 ignored | `Logs/friendtest-playmode-20260610.xml` |
| macOS FriendTest 构建 | `BuildScript.Build` | 阻塞 | `Logs/build-friendtest-macos-20260610.log`, `Logs/build-friendtest-macos-20260610-gfx.log` |

Relay 双进程本轮结果:

```text
Host:   2026-06-10 08:52:08 PASS, connectedClients(incl self)=2
Client: 2026-06-10 08:52:08 PASS, connected=true
```

测试结果摘要:

```text
EditMode: testcasecount=83, passed=83, failed=0, skipped=0
PlayMode: testcasecount=10, passed=8, failed=0, skipped=2
```

PlayMode ignored 说明:

- `RelayTwoProcessPlayTests.RelayHost_PublishesCodeAndAcceptsPeer`
- `RelayTwoProcessPlayTests.RelayClient_JoinsHostByCode`

这两个用例在未设置 `GANGLAND_RELAY_ROLE` 时会 ignored；真实 Relay 路径由 `run-relay-twoprocess.sh` 单独启动 Host/Client 双进程验证。

---

## 3. 构建阻塞

目标输出:

```text
Builds/FriendTest-20260610/StandaloneOSX/GanglandUndercover.app
```

实际结果:

- 首次构建在沙盒内失败: Unity Package Manager 无法创建 `/tmp/Unity-Upm-*.sock`。
- 沙盒外重跑 `-nographics` 后越过 UPM，但 Unity Licensing Client 反复断线重连，目标构建目录未生成。
- 去掉 `-nographics` 后再次重跑，Metal/GPU 初始化正常，但仍停在 Unity Licensing Client 重连，未进入 `BuildScript` 输出阶段。
- 已终止卡住的 Unity 构建进程，避免占用工程。

关键日志片段含义:

```text
Error: 'com.unity.editor.headless' was not found.
The connection with the Unity Licensing Client has been lost.
```

恢复动作:

1. 打开 Unity Hub 或 Unity Editor，确认账号/许可证状态正常。
2. 关闭所有 Unity 进程。
3. 重跑 FriendTest 构建命令；若 `-nographics` 继续触发 Licensing 问题，改用不带 `-nographics` 的 batchmode 命令。
4. 确认日志出现 `[BuildScript] BUILD SUCCESS`，且目标目录生成后，再把 `Builds/FriendTest-20260610/StandaloneOSX/GanglandUndercover.app` 发给朋友。

---

## 4. 今天可以继续推进的测试

如果需要在新构建前继续验证:

- 本机继续跑 `run-relay-twoprocess.sh` 做 Relay 稳定性复测。
- 使用 Unity Editor PlayMode 双开验证最新代码的 Lobby/聊天/会议流程。
- 等新构建恢复后，再执行 `friend_remote_test_runbook_20260610.md` 的两人公网测试。

今天不要用旧构建冒充新构建:

- `Builds/macOS/GanglandUndercover.app` 存在，但 Info.plist 显示 Unity 6000.4.5f1 旧产物。
- 它不能代表 2026-06-10 最新聊天 HUD/Relay 验证状态。

---

## 5. 远程测试通过门槛

新构建恢复后，朋友测试只要满足下面 6 条即可记为今天 P0 闭环通过:

- Host 创建 Relay 房间成功。
- Client 使用房间码加入成功。
- 两端 Ready 后能开始对局。
- 两端移动同步可见。
- 文本聊天至少发送 1 条并显示正确。
- Client/Host 任一端退出时，另一端不崩溃并有可解释状态。
