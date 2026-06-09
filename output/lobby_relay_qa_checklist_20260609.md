# Gangland Undercover — Lobby + Relay 手工 QA 清单

> 创建: 2026-06-09 | 版本: v0.2.0-dev
> 目的: 验证 Unity Lobby + Relay 云服务的真实端到端联机流程
> 前提: Unity Cloud Project 已绑定，Authentication/Lobby/Relay 服务已启用

---

## 环境准备

| 项目 | 要求 | 验证 |
|------|------|------|
| Unity Cloud Project | Dashboard 中 Project ID 已绑定编辑器 | ☐ |
| Authentication | 匿名登录可用（`UnityServiceBootstrap` 初始化成功） | ☐ |
| Relay | Dashboard 中 Relay 服务已启用 | ☐ |
| 两个客户端 | 同一局域网或公网可互通的两台机器/两实例 | ☐ |
| 网络状态栏 | HUD 显示 `Cloud OK | Services OK | Auth OK | Relay OK | Vivox 已移除` | ☐ |

---

## 场景 A: Host 创建 Relay 房间

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| A1 | 客户端 1 启动，进入主菜单 | 显示主菜单面板，网络状态栏正常 | ☐ |
| A2 | 点击「Relay 开房」 | HUD 显示"正在创建 Relay 房间…" | ☐ |
| A3 | 等待 3-10 秒 | 显示 6 位房间码（如 `ABC123`），状态变为"等待 Client 加入…" | ☐ |
| A4 | 检查 Console | 无 NullReferenceException 或 Relay 相关错误 | ☐ |
| A5 | 记录房间码 | — | ☐ |

---

## 场景 B: Session 出现在 Lobby 列表

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| B1 | 客户端 2 启动，进入主菜单 | 主菜单面板正常 | ☐ |
| B2 | 点击「刷新房间列表」 | 房间列表中出现客户端 1 创建的房间 | ☐ |
| B3 | 观察房间信息 | 显示房间名/人数等基本信息 | ☐ |
| B4 | 房间码一致（如果列表展示） | 与 A5 记录的码一致 | ☐ |

> 注：如果当前 UI 未实现 Lobby 列表刷新功能（仅支持手动输入房间码），
> 则 B1-B4 标记为 N/A，改用 C 系列步骤。

---

## 场景 C: 第二客户端通过房间码加入

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| C1 | 客户端 2 点击「Relay 加入」 | 弹出房间码输入框 | ☐ |
| C2 | 输入 A5 步骤的 6 位房间码 | 输入框接受输入 | ☐ |
| C3 | 确认加入 | 客户端 2 状态变为"已加入"，玩家列表中出现 2 名玩家 | ☐ |
| C4 | 检查客户端 1 的玩家列表 | 客户端 2 的玩家名出现在 Host 的玩家列表中 | ☐ |
| C5 | 两端检查 Console | 均无错误 | ☐ |

---

## 场景 D: Ready → Start → 完整对局

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| D1 | 两端都点击「Ready」 | 按钮变灰/显示"已就绪" | ☐ |
| D2 | 客户端 1（Host）点击「开始」 | 两端进入身份简报阶段 | ☐ |
| D3 | 等待简报倒计时结束 | 两端进入 Action 阶段，地图加载 | ☐ |
| D4 | 两端分别 WASD 移动 | 两端都能看到对方角色移动 | ☐ |
| D5 | 跑完一局到结算 | 正常进入 Result 阶段 | ☐ |

---

## 场景 E: Host 离开房间后 Session 消失

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| E1 | 回到 Lobby 准备界面 | 两端都在 Lobby | ☐ |
| E2 | 客户端 1（Host）点击「离开房间」 | Host 断开连接 | ☐ |
| E3 | 客户端 2 观察 | 状态变为"Host 已断开"或自动返回主菜单 | ☐ |
| E4 | 第三客户端（或客户端 2 重启）刷新房间列表 | 原房间不再出现在列表中 | ☐ |
| E5 | 使用原房间码尝试加入 | 加入失败，提示"房间不存在"或"房间码无效" | ☐ |

---

## 场景 G: 开局后房间锁定（Host 开始对局）

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| G1 | 回到场景 C3 后（2 名玩家已加入） | 两端在 Lobby 阶段 | ☐ |
| G2 | Host 点击「开始」或「补 AI 开局」 | 两端进入身份简报阶段 | ☐ |
| G3 | 第三客户端刷新房间列表 | 原房间显示状态为「锁定」 | ☐ |
| G4 | 第三客户端尝试通过房间码加入 | 加入失败，提示"该 Lobby 已锁定，可能已经开局" | ☐ |
| G5 | 检查 Host 端 HUD 规则区域 | 规则滑块/开关变灰不可编辑（`CanEditRoomSettings()` 返回 false） | ☐ |

> 代码路径: `OnlineMatchController.cs:1005 → SetPublishedLobbySessionLocked(true)`
> 拒绝逻辑: `LobbyBrowser.cs:744 → if (isLocked) return "锁定"`

---

## 场景 H: 返回大厅后房间解锁

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| H1 | 完整对局跑完，进入 Result 阶段 | 两端显示结算画面 | ☐ |
| H2 | 点击「返回大厅」/等待自动返回 | 两端回到 Lobby 阶段 | ☐ |
| H3 | 检查 Host 端 HUD 规则区域 | 规则滑块/开关恢复可编辑（`CanEditRoomSettings()` 返回 true） | ☐ |
| H4 | 第三客户端刷新房间列表 | 原房间恢复为「可加入」状态 | ☐ |
| H5 | 第三客户端通过房间码加入 | 成功加入，玩家列表出现 3 名玩家 | ☐ |

> 代码路径: `OnlineMatchController.cs:3482 → SetPublishedLobbySessionLocked(false)`

---

## 场景 I: 满员房间不可加入

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| I1 | Host 创建房间，设置最大人数为 2 | 规则区域显示"最大 2" | ☐ |
| I2 | 客户端 2 加入 | 成功，玩家列表 2/2 | ☐ |
| I3 | 第三客户端刷新房间列表 | 该房间显示"已满" | ☐ |
| I4 | 第三客户端尝试通过房间码加入 | 加入失败，提示"该 Lobby 已满，暂不能加入" | ☐ |

> 拒绝逻辑: `LobbyBrowser.cs:754 → if (playerCount >= maxPlayers) return "已满"`

---

## 场景 J: 密码/私密房间不可加入

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| J1 | Host 创建私密房间（`IsPrivate = true`） | 房间不在公开列表出现 | ☐ |
| J2 | 第三客户端尝试通过房间码加入私密房间 | 加入失败，提示"该 Lobby 需要密码，当前列表暂不支持加入" | ☐ |
| J3 | （如果后续实现密码输入 UI）输入正确密码后 | 成功加入 | ☐ |

> 拒绝逻辑: `LobbyBrowser.cs:749 → if (hasPassword) return "密码"`
> 注意: 当前版本密码输入 UI 尚未实现，`hasPassword` 的房间统一拒绝

---

## 场景 K: 锁定/满员/密码房间的列表显示

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| K1 | 同时创建 3 个房间：正常房、满员房、锁定房 | 3 个房间都在列表中 | ☐ |
| K2 | 观察每个房间行的状态标签 | 正常→「可加入」，满员→「已满」，锁定→「锁定」 | ☐ |
| K3 | 点击已满房间的加入按钮 | 加入被拒绝，状态提示"已满" | ☐ |
| K4 | 点击锁定房间的加入按钮 | 加入被拒绝，状态提示"锁定" | ☐ |
| K5 | 观察无 Relay 码的房间 | 状态显示"待发布 Relay" | ☐ |

> 状态优先级: 锁定 > 密码 > 已满 > 待发布 Relay > 可加入
> 代码路径: `LobbyBrowser.cs:742 → RoomJoinState()`

---

## 场景 L: 边界情况

| 步骤 | 操作 | 预期结果 | 通过? |
|------|------|---------|-------|
| F1 | 输入错误房间码 | 提示"房间码无效"或加入超时 | ☐ |
| F2 | 房间满人后尝试加入 | 提示"房间已满" | ☐ |
| F3 | 对局中 Host 断网/强退 | 客户端检测到断线，回到主菜单（当前无 Host 迁移） | ☐ |
| F4 | 对局中 Client 断线 | Host 端该玩家从列表消失，尸体保留 | ☐ |
| F5 | 同一房间码加入两次 | 第二次应被拒绝或视为重连 | ☐ |
| F6 | 锁定房间在列表中的颜色/标记 | 显示「锁定」状态，与其他状态区分 | ☐ |

---

## 自动化对应

| 场景 | 自动化测试 | 备注 |
|------|-----------|------|
| A1-A5 | `RelayTwoProcessPlayTests.RelayHost_PublishesCodeAndAcceptsPeer` | 需 `GANGLAND_RELAY_ROLE=host` |
| C1-C5 | `RelayTwoProcessPlayTests.RelayClient_JoinsHostByCode` | 需 `GANGLAND_RELAY_ROLE=client` |
| D1-D5 | `MatchLoopPlayTests.FullMatchLoop_RunsThroughEveryPhaseAndRestarts` | 本地模式 |
| F4 | `MatchLoopPlayTests.ClientDisconnect_ReleasesTaskLocksVotesAndKeepsBodyReportable` | 本地模式 |
| G1-G5 | `CoreSystemTests.LobbyRoomSessionJoin_BlocksLockedRooms` | EditMode |
| I1-I4 | `CoreSystemTests.LobbyRoomSessionJoin_BlocksFullRooms` | EditMode |
| J1-J3 | `CoreSystemTests.LobbyRoomSessionJoin_BlocksPasswordRooms` | EditMode |

---

## 验收标准

- [ ] 场景 A-K 全部通过
- [ ] 场景 L 的 L1-L5 不产生 NullReferenceException
- [ ] 两端全程 Console 无红色 Error
- [ ] Host 离开后 Relay Session 确实被清理（E4 验证）
- [ ] 开局后房间自动锁定（G3-G4 验证），返回大厅后自动解锁（H4-H5 验证）
- [ ] 满员/密码/锁定房间正确拒绝加入（I4/J2/K3-K4 验证）
- [ ] 房间码格式为 6 位字母数字，输入校验正常
