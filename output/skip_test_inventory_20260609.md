# Gangland Undercover — 跳过/忽略测试清点

> 日期: 2026-06-09 | 版本: v0.2.0-dev

---

## 1. EditMode 测试: 20/20 passed

全部通过，无跳过。文件：`Assets/Tests/CoreSystemTests.cs`

---

## 2. PlayMode 测试: 6 passed / 2 ignored / 0 failed

### 已通过的 6 项

| # | 文件 | 测试方法 | 覆盖风险点 |
|---|------|---------|-----------|
| 1 | MatchLoopPlayTests | FullMatchLoop_RunsThroughEveryPhaseAndRestarts | 全阶段循环 |
| 2 | MatchLoopPlayTests | Character2DAnimator_UpdatesLocalAndRemoteWalkFrames | 角色动画帧 |
| 3 | MatchLoopPlayTests | ClientDisconnect_ReleasesTaskLocksVotesAndKeepsBodyReportable | 断线清理 |
| 4 | MiniGameOnlineIntegrationPlayTests | OnlineTasks_OpenRichMinigames_AndCompleteThroughServerPath | 小游戏多样性+闭环 |
| 5 | MiniGameAuthorityPlayTests | MiniGameBridge_RejectsUnopenedTask_AndCompletesServerOpenedTaskOverRpc | ServerRpc 授权 |
| 6 | NetworkCustomMessagePlayTests | CustomMessages_RejectMalformedAndSpoofedMessagesOverNetcode | 恶意消息拒绝 |

### 被忽略的 2 项

| # | 文件 | 测试方法 | 忽略原因 | 触发代码 |
|---|------|---------|---------|---------|
| 7 | RelayTwoProcessPlayTests | RelayHost_PublishesCodeAndAcceptsPeer | 环境变量 `GANGLAND_RELAY_ROLE` 未设为 `host` | `Assert.Ignore()` L75 |
| 8 | RelayTwoProcessPlayTests | RelayClient_JoinsHostByCode | 环境变量 `GANGLAND_RELAY_ROLE` 未设为 `client` | `Assert.Ignore()` L131 |

---

## 3. 忽略测试运行方式

### 一键编排脚本

```bash
bash run-relay-twoprocess.sh
```

脚本自动完成：
1. 为 Client 创建独立 Library 的 clone 工程（符号链接 Assets/Packages/ProjectSettings）
2. 首次运行时预热 clone 工程 Library
3. 启动 Host 进程（GANGLAND_RELAY_ROLE=host）
4. 延迟 20 秒后启动 Client 进程（GANGLAND_RELAY_ROLE=client）
5. 两进程通过 `/tmp/gangland-relay-code.txt` 交换 Relay 房间码
6. 超时看门狗（默认 600 秒）

### 所需环境变量

| 变量 | 用途 | 默认值 | 必需? |
|------|------|--------|-------|
| `GANGLAND_RELAY_ROLE` | 区分 Host/Client 进程 | 无（未设置则 Ignore） | ✅ |
| `GANGLAND_RELAY_CODEFILE` | 共享房间码文件路径 | `/tmp/gangland-relay-code.txt` | 否 |
| `GANGLAND_RELAY_CLIENT_PROJECT` | Client 端 clone 工程路径 | `{项目}_relayclient` | 否 |
| `GANGLAND_RELAY_TIMEOUT_SECONDS` | 双进程超时 | `600` | 否 |
| `GANGLAND_RELAY_KILL_GRACE_SECONDS` | KILL 前等待 | `8` | 否 |

### 前置条件

| 条件 | 说明 | 当前状态 |
|------|------|---------|
| Unity Relay Service 已启用 | Unity Dashboard 中启用 Relay 服务 | ✅ |
| Unity Authentication 已配置 | 匿名登录可用 | ✅ |
| 双进程独立 Library | 两个 Unity 实例不能共享同一 Library | 脚本自动处理（符号链接 clone） |
| 网络 | Host/Client 需要公网连接到 Unity Relay | ✅ |

### 手动分步（不推荐）

```bash
# 进程 A：Host
GANGLAND_RELAY_ROLE=host \
  /Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -nographics \
  -projectPath /path/to/main/project \
  -testPlatform PlayMode \
  -testFilter "RelayTwoProcessPlayTests.RelayHost_PublishesCodeAndAcceptsPeer" \
  -testResults Logs/relay-host-results.xml \
  -logFile Logs/relay-host.log

# 进程 B：Client（需独立 Library 的项目副本）
GANGLAND_RELAY_ROLE=client \
  /Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -nographics \
  -projectPath /path/to/clone/project \
  -testPlatform PlayMode \
  -testFilter "RelayTwoProcessPlayTests.RelayClient_JoinsHostByCode" \
  -testResults Logs/relay-client-results.xml \
  -logFile Logs/relay-client.log
```

---

## 4. 无 CI 硬编码跳过

项目中无 `[Ignore]` 特性标记，所有忽略均通过 `Assert.Ignore()` 在运行时根据环境变量动态触发。
`CIRunner.cs` 的 `skipCount` 字段为 CI 统计值，非硬编码跳过列表。
