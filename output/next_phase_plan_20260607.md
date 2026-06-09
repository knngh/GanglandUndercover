# Gangland Undercover — 下一阶段长期开发计划（质量补强版）

> **日期**: 2026-06-07 | **版本**: v0.2.0-dev → v0.3.0
> **定位**: 质量补强（Depth over Breadth）。不再走名义 checklist。
> **前置审查**: 本计划基于 2026-06-07 源码实测，取代"凭 git commit 即视为完成"的判定。
> **权威性**: 本文为下一阶段（Stabilization）唯一权威路线图，与 master_development_plan_v2 互补——v2 定义广度蓝图，本文定义"把骨架做成真东西"的深度补强。

---

## 0. 审查结论：为什么需要本计划

git log 显示 v2 的 Phase 1–6 已"全部完成"，但实测证明这些完成是**骨架级 / 占位级**，验收标准被降级为"代码提交 + 编译 0/0 + 单个集成测试 Passed"，而非"玩家可感知 + 联机双端可验证"。

### 实测真相表（2026-06-07）

| 维度 | 文档声称 | 实测真相 | 评级 |
|------|---------|---------|------|
| 控制器拆分 D-01 | Phase 1 完成 | 主文件仍 5745 行（目标<4000），partial 合计 ~11400 行 | 部分 |
| 网络架构 | NGO Host/Client | 绕开 NGO 标准 RPC，全走 CustomMessagingManager 自定义消息+快照（Network.cs 47处），权限全靠手写 IsServer/IsHost | 需安全审计 |
| 标准 RPC | — | 全项目仅 2 文件用 [ServerRpc]，OnlineMiniGameBridge 是死代码（无 NetworkObject，IsSpawned 恒 false） | 死代码 |
| ChatSystem D-08 | Phase 1 联网化 | 仍是 public class ChatSystem，非 NetworkBehaviour，联机聊天实际不可用 | 未做 |
| 职业能力 Phase 3.2 | 完成 | Abilities.cs 仅 233 行服务端方法存根；MoleIntel 错用 SabotageCooldownReduce 当门禁；未确认接 RPC/HUD | 部分 |
| 监控摄像头 D-06 | Phase 2 完成 | globalObjectIdHash=0 复制 bug，远端 Client 看不到摄像头 | 已知 bug |
| AI Bot | 完成 | 771 行状态机能任务/击杀/报告/投票，但不会暗线寻路、复杂地图卡死 | 部分 |
| OnGUI 迁移 Phase 1.4 | 全 uGUI | OnlineMatchController.OnGUI.cs 仍 1653 行，OnlineMatchHud 2273 行 | 未做 |
| Resources 体积 | 104MB / 832MB | 审计文档自相矛盾，需复核实测 | 存疑 |
| 编译 / 技术债 | 0/0 | 编译干净，TODO 标记仅 17 处 | 好 |

### 核心判断
- 项目**广度已足**（地图/美术/音频/小游戏/能力枚举/Meta 框架都有文件）。
- 项目**深度普遍不足**（联机正确性、能力可感知、UI 现代化、Bot 智能、内容打磨均为骨架）。
- 真正的关键路径是**联机正确性根基**——这是 online-first 游戏的命门，当前最薄弱。

---

## 设计支柱（沿用 v2，不可妥协）
1. 欺骗与推理 — 信息是被发现的，不是给定的
2. 职业即玩法 — 不同职业能做的事根本不同
3. 港味 noir — 霓虹、雨夜、信任崩塌
4. 高压时刻 — 紧急任务/破坏/会议指证
5. 可重复性 — 每局不同的故事

### 新增"完成"的定义（本阶段强制）
一个功能只有同时满足以下三条才算完成：
1. **代码正确**：编译 0/0，逻辑无占位/死代码。
2. **联机双端可验证**：双进程（或双机）实测，Host 与 Client 表现一致，非 Host 无法伪造。
3. **玩家可感知**：HUD/VFX/音频有反馈，不看文档能理解。

---

## 主线 A：联机正确性根基（最高优先 · M-A · 约 3-4 周）

> online-first 游戏的命门。当前全靠 CustomMessagingManager 手写消息，权限校验分散且未审计。

### A1. 自定义消息层服务端权威审计
- 枚举 Network.cs 中全部 47 处自定义命名消息，逐条建立"消息契约表"：发送方、接收方、服务端校验项。
- 每条入站消息在服务端校验：sender 身份合法、玩家存活、阶段允许、目标在范围内、冷却就绪。
- 杜绝客户端伪造：击杀、投票、任务完成、破坏、能力释放。
- 产出：output/network_message_contract_20260607.md（契约表）。

### A2. 反作弊回归测试
- PlayMode 测试：模拟恶意 Client 发送越权消息（杀已死玩家/非己方投票/重复完成任务），断言服务端拒绝。
- 纳入 CI 门禁。

### A3. ChatSystem 真正联网化（D-08 修复）
- 改为 NetworkBehaviour 或并入自定义消息层；三频道（meeting/global/proximity）+ ghost 频道实际可收发。
- UI 从 OnGUI 改 uGUI InputField + ScrollView。
- 双端实测：A 发言 B 能收到，死者只进 ghost 频道。

### A4. 监控摄像头复制 bug 修复（D-06）
- 改注册为 NetworkPrefab（替换运行时 AddComponent<NetworkObject>().Spawn() globalObjectIdHash=0）。
- 双端实测：Client 能看到摄像头画面。

### A5. 死代码清理
- 删除 OnlineMiniGameBridge 的 ServerRpc 服务器驱动路径（IsSpawned 恒 false），或正式接 NetworkObject。二选一并落地。

### A6. 断线 / 重连 / 主机退出
- HostMigrationManager 真实联调（当前仅存在，未充分测试）。
- Client 断线 → 服务器 ReleaseTask + 释放其占用；重连恢复快照。
- Host 退出 → 房间不坏死（迁移或干净结束）。

### M-A 验收
- [ ] 47 条消息全部有服务端校验，契约表完整
- [ ] 恶意 Client 越权测试全部被拒
- [ ] 双端聊天可用、摄像头可见
- [ ] 断线/重连/Host 退出不坏死房间
- [ ] 双进程完整 3 局不崩

---

## 主线 B：控制器架构真收口（M-B · 约 2-3 周）

> 主文件 5745 行未达 v2 的 <4000 目标。每加一个功能都在 god object 上堆代码。

### B1. 主文件减重到 <4000 行
- 抽取 MatchSnapshotService（复用 GameStateSnapshot.cs）：CaptureSnapshot / RestoreFromSnapshot / BroadcastSnapshot。
- 把 Bot 相关计时器/目标字典彻底移入 OnlineBotController。
- 把击杀冷却/范围/报告冷却移入 KillSystem。

### B2. OnGUI 全量迁移 uGUI（Phase 1.4 真做）
- OnlineMatchController.OnGUI.cs（1653 行）+ OnlineMatchHud.cs（2273 行）→ uGUI Canvas 预制件。
- MatchHUD / MeetingUI / GameOverUI / LobbyUI 四大面板。
- 验收：全流程无运行时 OnGUI 调用（编辑器工具除外）。

### B3. 子系统可单测
- 拆分后每个 service（Task/Sabotage/Kill/Snapshot/Ability）能脱离控制器单测。

### M-B 验收
- [ ] OnlineMatchController.cs < 4000 行
- [ ] 运行时 0 处 OnGUI
- [ ] 拆分后 PlayMode 双进程一局不崩（回归绿）

---

## 主线 C：差异化机制做实（M-C · 约 4-6 周）

> "比 Among Us 多"的卖点。当前职业能力是服务端存根，证据链只有数据结构。

### C1. 职业能力 12 种全量接入（接 RPC + HUD + VFX）
- Abilities.cs 现有存根（足迹/暗视/拖尸/任务加速/Mole情报等）补齐：
  - 修复 MoleIntel 错用 SabotageCooldownReduce 当门禁 → 改用正确角色判定。
  - 每个能力：服务端逻辑 + 自定义消息触发 + HUD 按钮/冷却 + VFX/SFX 反馈。
- 每职业 ≥2 个能力，PlayMode 可触发可验证。

### C2. 证据链平衡实测
- EvidenceChain 关联矩阵（同类+1/跨类+2/发现者链+1）实跑。
- 会议指证投票权重（链强度≥3 → +2 票）平衡 pass。
- 验证 Bot 局能产出足够证据生成有意义的 CaseLog。

### C3. 卧底双身份 + 内鬼翻盘做实
- 伪装任务/情报窃取/背叛窗口/双结局实际可走通。
- 内鬼 Intel 满 5 → 暗杀名单 → 翻盘条件，PlayMode 验证。

### C4. Bot 智能补强
- 暗线/通风寻路（当前完全没有，P2-2）。
- 卡死根治（导航替换简单直线移动）。

### M-C 验收
- [ ] 每职业 ≥2 能力可感知可验证
- [ ] 证据链/卧底/内鬼在 Bot 局能自然产生转折
- [ ] Bot 会用暗线、不卡死

---

## 主线 D：内容与品质（M-D · 约 6-8 周）

### D1. 美术运行时确认
- Play Mode 确认角色显示 CC0 像素图而非程序化几何体（审计存疑项）。
- 地图 tile 用 CC0 PNG 而非纯色 Circle/Rect。
- 尸体用角色倒下 sprite（P2-1）。
- 角色 Animator walk 帧动画验证（P1-1）。

### D2. 地图内容
- HarbourDistrict / PoliceStation / KowloonWalledCity 三图 MapValidator 跑通。
- 3 图 × 6/8/10 人节奏 balance pass。

### D3. 音频健壮性
- AudioManager.Awake() 加 Resources.Load fallback（当前 SerializeField 空槽位静默不播放）。

### M-D 验收
- [ ] 3 张地图美术化、视觉清晰可辨
- [ ] 音频完整、无静默失败

---

## 主线 E：发布工程做实（M-E · 约 4 周）

### E1. Resources 体积复核
- 实测 Resources 真实大小（文档 104MB vs 832MB 矛盾）。
- 将 Synty/Quaternius/Free Pack 等 3D 遗留资产移出 Resources。

### E2. 多平台真机构建
- macOS 签名+公证；Windows 真机构建验证。

### E3. 性能基线
- 60 FPS @1080p / 内存 <2GB / 网络 <50KB/s per client / 进大厅 <15s。

### M-E 验收
- [ ] 非开发机可运行
- [ ] 30 分钟无 P0
- [ ] 第三方资源清单清楚

---

## 里程碑与依赖

```
M-A 联机正确性根基   ████████ (3-4周)  ← 最高优先，命门
M-B 架构真收口       ██████   (2-3周)  ← 可与 A 部分并行
M-C 差异化做实       ████████████ (4-6周) ← 依赖 A+B
M-D 内容与品质       ████████████████ (6-8周) ← 依赖 C 玩法定型
M-E 发布工程         ████████ (4周)   ← 依赖全部就绪
```

不可跳过的依赖：
- M-A 是一切的根基（联机不正确，其余功能在错误地基上）。
- M-B 应在 M-C 之前（否则继续在 god object 上堆）。
- M-A 与 M-B 可部分并行。

---

## 阶段门禁（每个里程碑结束强制）
- [ ] batchmode 编译 0 error
- [ ] 全套 EditMode + PlayMode 测试绿
- [ ] 双进程/双机手动联机 ≥3 局
- [ ] 该里程碑 P0/P1 清零
- [ ] KNOWN_ISSUES.md 更新（实测证据，非凭记忆）
- [ ] 提交推送 GitHub

## 反虚假完成度纪律
- 禁止把"git commit + 编译通过"等同于"完成"。
- 每个验收项必须有"双端实测证据"或"测试名 + Passed"。
- 每个里程碑复跑一次实测重基线（沿用 v2 的 0.0 重新基线化纪律）。
