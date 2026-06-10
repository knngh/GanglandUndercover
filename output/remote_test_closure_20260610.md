# Gangland Undercover — 远程测试闭环记录

> 日期: 2026-06-10
> 目标: 以“今天能和朋友远程测试”为验收目标，确认自动化、手工脚本、构建状态和剩余风险。

---

## 1. 结论

今天已完成远程联机前置闭环:

- Relay 双进程真实云服务验证通过。
- EditMode 全量通过。
- PlayMode 常规套件通过，无失败；Relay 两个 PlayMode 用例在常规套件中按设计 ignored，并已由双进程脚本单独验证。
- macOS FriendTest 构建已生成，并已压缩成可发送给朋友的 zip。
- 朋友手工测试脚本已补齐: `output/friend_remote_test_runbook_20260610.md`。

---

## 2. 自动化证据

| 验证项 | 命令/入口 | 结果 | 证据 |
|--------|-----------|------|------|
| Relay 双进程 | `bash run-relay-twoprocess.sh` | PASS | `Logs/relay-host-result.txt`, clone `Logs/relay-client-result.txt` |
| EditMode | Unity `-runTests -testPlatform EditMode` | 83 passed, 0 failed, 0 skipped | `Logs/friendtest-editmode-20260610.xml` |
| PlayMode | Unity `-runTests -testPlatform PlayMode` | 8 passed, 0 failed, 2 ignored | `Logs/friendtest-playmode-20260610.xml` |
| macOS FriendTest 构建 | `BuildScript.Build` | SUCCESS | `Logs/build-friendtest-macos-20260610-retry2.log` |
| macOS FriendTest zip | `ditto -c -k --sequesterRsrc --keepParent` | 84 MB zip | `Builds/FriendTest-20260610/GanglandUndercover-FriendTest-macOS-20260610.zip` |

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

## 3. 构建产物

目标输出:

```text
Builds/FriendTest-20260610/StandaloneOSX/GanglandUndercover.app
```

实际产物:

- App: `Builds/FriendTest-20260610/StandaloneOSX/GanglandUndercover.app`
- Zip: `Builds/FriendTest-20260610/GanglandUndercover-FriendTest-macOS-20260610.zip`
- Zip 大小: 84 MB
- Zip sha256: `8556990ab3c226176fc6c7f495f81f7ef96c84395afde4fd63208923a2fced89`
- Unity: 6000.4.9f1
- App version: 0.1.0-dev
- 构建代码 commit: `faaf88be`

构建日志摘要:

```text
[BuildScript] BUILD SUCCESS: .../Builds/FriendTest-20260610/StandaloneOSX/GanglandUndercover.app (234 MB, 00:00:16.0679490)
```

构建恢复过程:

- 前两次构建卡在 Unity Licensing Client 重连，未进入 `BuildScript` 输出阶段。
- 清理残留 `Unity.Licensing.Client` 进程后重跑成功。
- 成功构建命令未使用 `-nographics`，避免再次触发 headless/licensing 组合问题。

---

## 4. 今天可以继续推进的测试

现在可以继续推进:

- 把 zip 发给朋友，让双方使用同一包执行 `friend_remote_test_runbook_20260610.md`。
- 本机保留 `run-relay-twoprocess.sh` 作为云服务健康复测入口。
- 朋友反馈统一按 runbook 的问题记录模板回填。

今天不要用旧构建冒充新构建:

- `Builds/macOS/GanglandUndercover.app` 存在，但 Info.plist 显示 Unity 6000.4.5f1 旧产物。
- 它不能代表 2026-06-10 最新聊天 HUD/Relay 验证状态。

---

## 5. 远程测试通过门槛

朋友测试只要满足下面 6 条即可记为今天 P0 闭环通过:

- Host 创建 Relay 房间成功。
- Client 使用房间码加入成功。
- 两端 Ready 后能开始对局。
- 两端移动同步可见。
- 文本聊天至少发送 1 条并显示正确。
- Client/Host 任一端退出时，另一端不崩溃并有可解释状态。
