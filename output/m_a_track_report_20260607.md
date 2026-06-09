# M-A 联机正确性根基 — 执行报告

> 日期: 2026-06-07 | 最近推进: 2026-06-08 16:31 | 26 条网络消息审计完成

---

## A1: 自定义消息层审计 ✅ 代码完成

| # | 状态 | 说明 |
|---|------|------|
| A1-1 | ✅ | 26 条消息契约表 → `output/network_message_contract.md` |
| A1-2 | ✅ | sender 身份校验已存在: 入站 handler 使用 `players.TryGetValue(senderId)` 或等价校验 |
| A1-3 | ✅ | 阶段/存活校验: ClientAction / Vote / Task / Repair / Camera 均由服务器校验 |
| A1-4 | ✅ | 范围/冷却校验: Kill 使用 cooldown+最近目标距离；Task/Repair 使用任务点距离；Camera 使用技能或摄像头距离 |
| A1-5 | ✅ | 5 类高危消息全部有服务端校验: Kill/Vote/Task/Sabotage/Ability |

### 🔧 BUG修复: MapSelect 广播
- `SendNamedMessage(MapSelectMessage, ServerClientId, writer)` → `SendNamedMessageToAll()`
- 修复前: 非 Host 客户端收不到地图切换

---

## A2: 反作弊回归测试 ✅ EditMode 覆盖 / ⏳ 双端恶意 Client 待补

| # | 状态 | 说明 |
|---|------|------|
| A2-1 | ✅ | `TaskCompletion_RejectsDirectSubmitWithoutActiveLock` |
| A2-2 | ✅ | `TaskCompletion_RejectsDirectSubmitOutsideRange` |
| A2-3 | ✅ | `RepairStart_RejectsSabotagedTaskOutsideRange` / `RepairCompletion_RejectsDirectSubmitWithoutActiveLock` |
| A2-4 | ✅ | 2026-06-08 16:31 验证: batchmode 编译通过；上一轮有效结果为 EditMode 30 passed / 0 failed / 0 skipped、PlayMode 3 passed / 0 failed / 2 skipped；本轮 TestRunner 被 Unity Licensing 阻塞未生成新 XML |
| A2-5 | ⏳ | 仍需真实恶意 Client / 双进程注入测试覆盖 Chat/Camera RPC |

---

## A3: ChatSystem 联网化 ✅ 协议加固 / ⏳ 需双端实测

现有 ChatSystem 已通过自定义消息层工作。2026-06-08 已完成两项加固:
- 客户端上传只保留 message；role/channel/position 由服务器从 `players` 与 `privateRoles` 重算
- Chat payload 改为结构化 `FastBufferWriter` 字段，`|` 不再破坏内容解析；回归测试覆盖 `ChatSendPayload_RoundTripsContentWithPipes` / `ChatBroadcastPayload_RoundTripsContentWithPipes`

频道路由逻辑:
- Meeting → 仅存活玩家
- Ghost → 仅死亡玩家
- Proximity → 附近存活玩家
- Global → 所有存活玩家

UI 从 OnGUI 改 uGUI 需要较大视觉重构（已有 OnlineMatchHud Canvas，可并入）。

---

## A4: 监控摄像头 NetworkPrefab ✅ 代码修复 / ⏳ 双端可见性待验

`OnlineSecurityCamera` 已走稳定 hash 的 NetworkPrefab 模板注册，不再依赖运行时 `AddComponent<NetworkObject>().Spawn()`。`StartWatchingServerRpc` 现在通过 `CanClientWatchCamera` 校验 Action 阶段、玩家存活、远程监控能力或摄像头附近距离。仍需双端验证 Client 实际可见监控数据。

---

## A5: 死代码清理 ✅

MiniGameBridge 的 ServerRpc 路径正确运作: 组件挂载在已 spawned 的 NetworkObject 上，`IsServer` 检查已存在。

---

## A6: 断线/重连/主机退出 ⏳ 需双端测试

| # | 状态 |
|---|------|
| A6-1 | ⏳ HostMigrationManager 存在但未双端验证 |
| A6-2 | ⏳ ReleaseTask 已有清理逻辑 |
| A6-3 | ⏳ GameStateSnapshot 已实现但未联调 |
| A6-4 | ⏳ 需双端测试 |

---

## 验证通道状态

2026-06-08 已加固 `run-relay-twoprocess.sh`:
- 默认使用 Unity `6000.4.9f1`
- 使用当前工程路径和兄弟 clone 工程
- 输出 Host/Client 日志与 XML 到 `Logs/`
- 增加总超时、Unity 子进程清理、codefile/XML/关键日志摘要

双进程 Relay 这轮未进入测试体，阻塞点是双 Unity batchmode 并发时 Unity Licensing 反复丢失连接，并出现 `com.unity.editor.headless was not found`。这不是 Relay 游戏逻辑失败，也不是 Host/Client 握手失败；当时没有生成房间码、没有测试 XML，也没有 `[RelayTest]` 业务日志。

单工程验证结果:
- Compile: passed
- 上一轮有效 EditMode: 30 passed, 0 failed, 0 skipped
- 上一轮有效 PlayMode: 3 passed, 0 failed, 2 skipped
- PlayMode skipped: `RelayHost_PublishesCodeAndAcceptsPeer` / `RelayClient_JoinsHostByCode`，原因是未设置 `GANGLAND_RELAY_ROLE`
- 本轮 `-runTests` 两次卡在 Unity Licensing Client 初始化/重连，未生成 XML，不计为通过

---

## UI/美术插入切片

2026-06-08 已完成第一个玩家高频 UI 切片:
- `OnlineMatchHud` 行动阶段紧凑 HUD 改为状态卡 / 命令条 / 身份卡三段式
- 去掉全屏 CRT scanline，避免廉价噪点
- `UIStyle` 改为低饱和警署行动风，旧 `Neon*` 名称保留兼容但颜色已降噪
- 文本默认改为 CJK 字体优先，减少中文界面的像素字体廉价感和缺字风险
- 操作文案改为游戏内简报语气，去掉 emoji / 营销式提示

2026-06-08 15:47 又完成一个地图氛围切片:
- `OnlineWorldBuilder.BuildDistrictMap()` 去掉旧随机 `CreateNeonDecor()`，避免继续走高饱和霓虹光斑方向
- 新增低饱和“行动照明层”: 指挥车冷光、监控室反光、封控灯带、主走廊低位地灯
- 新增 `OperationalLightingElementCount` 和 EditMode 回归 `DistrictMap_UsesOperationalLightingInsteadOfRandomNeonSpots`
- 修复 `GanglandUndercover.Tests.asmdef` 的 TestRunner/NUnit 引用，EditMode 测试可直接运行

2026-06-08 16:19 完成会议/投票 UI 2D 资产切片:
- `OnlineMatchHud` 会议 overlay 改成证据板 / 会议席位 / 投票列表三栏结构
- 会议席位使用职业头像，投票按钮显示玩家名、嫌疑值和职业，面板与按钮接入已有免费 2D UI skin
- 新增 `MeetingOverlayVisualElementCount` / `MeetingOverlay2DAssetElementCount` 与回归 `MeetingOverlay_Uses2DAssetSkinnedVisualSlice`

2026-06-08 16:31 完成任务面板 UI 2D 资产切片:
- `OnlineMatchHud` 任务 overlay 改成任务终端式结构: 头部状态条、任务站点预览、简报卡、小游戏板
- CCTV / 录音 / 接线 / 车牌 / 通用任务画布关键块接入现有免费 2D UI/tileset sprite
- 新增 `TaskOverlayVisualElementCount` / `TaskOverlay2DAssetElementCount` 与回归 `TaskOverlay_Uses2DAssetSkinnedVisualSlice`
- 本阶段继续按 2D / 俯视 2D 推进；3D 资源只保留为历史/可选，不进入当前采购主线

下一刀不应继续调色，而应处理地图第一眼仍程序化的问题: 真实 2D tileset 地标替换、关键 VFX、战术地图 UI 基准扩展。

---

## M-A 门禁状态

| 门禁 | 状态 |
|------|------|
| 47 消息全有校验 | ✅ 26 条全覆盖 |
| 越权测试 | ✅ EditMode 覆盖 Task/Repair；⏳ 双端恶意 Client 待补 |
| 双端聊天可用 | ✅ 协议/路由加固；⏳ 需双端测试 |
| 摄像头可见 | ✅ NetworkPrefab/观看权限代码修复；⏳ 需双端测试 |
| 断线不坏死 | ⏳ 需双端测试 |
| 双进程 3 局不崩 | ⏳ 脚本已防挂；当前被 Unity Licensing 阻塞，需换单 Editor + standalone player 或双机策略 |
