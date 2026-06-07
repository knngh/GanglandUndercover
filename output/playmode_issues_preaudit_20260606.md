# Play Mode 潜在引用/体验问题 — 代码预审

> **日期**: 2026-06-06 | **编译**: ✅ 0 errors

---

## 一、代码级风险项（Play Mode 前修复）

### 🔴 P0 — 编译期可修复

| # | 文件 | 问题 | 修复建议 |
|---|------|------|---------|
| — | — | **无 P0 编译错误** | ✅ |

### 🟡 P1 — 运行时可能爆 NullRef

| # | 文件:行 | 问题 | 风险 |
|---|---------|------|------|
| 1 | `Visuals.cs:87` | `state.Character2DDirectionIndicator.GetComponent<SpriteRenderer>()` — 无 null check | 若 DirectionIndicator 未实例化则 NRE |
| 2 | `Visuals.cs:700` | `cameraObj.GetComponent<NetworkObject>()` — netObj 可为 null | 监控摄像头 Spawn 前需确保 NetworkObject 存在 |
| 3 | `OnlineMatchController.cs:2965` | `localChar.GetComponent<GhostMode>()` — ghost 可为 null | 鬼魂状态 UI 可能不显示 |
| 4 | `Network.cs:43` | `networkManager.GetComponent<UnityTransport>()` — 无 null check | Transport 未配置时静默 null |

### 🟢 P2 — 体验问题

| # | 描述 | 位置 | 影响 |
|---|------|------|------|
| 1 | 尸体显示使用几何图形而非角色 Sprite | `WorldBuilder.CreateBodyVisual` | 无法区分不同职业的尸体 |
| 2 | `AudioManager` 所有 AudioClip 槽位为 `[SerializeField]`，需要在 Editor 手动赋值 | `AudioManager.cs:54-77` | 若未赋值，所有音效静默不播放 |
| 3 | 地图渲染大量使用程序化 Sprite（Circle/Rect）， tileset PNG 可能未被实际使用 | `WorldBuilder.BuildDistrictMap` | 视觉上可能是纯色几何体而非像素风格 |
| 4 | 角色行走帧动画需要 `Animator` 组件，但未验证是否配置了 Animation Controller | `Character2DDirectionIndicator` | 角色可能静止不播放 walk 动画 |

---

## 二、需要 Play Mode 验证的运行时行为

以下无法通过代码分析确认，必须在 Editor Play Mode 中观察：

1. **角色渲染**: 7 个职业的 Sprite 是否按照 `Sprite2DAssetCache` 正确加载
2. **方向指示器**: 移动时 DirectionIndicator 是否旋转到正确方向
3. **暗线通道**: 进入/离开通道的视觉过渡是否平滑
4. **监控画面**: 监控室 UI 是否实时显示摄像头画面
5. **证据摘要**: 会议阶段证据是否按链强度排序显示
6. **聊天频道**: 四通道切换是否正常，消息格式是否对齐
7. **小地图**: 玩家/尸体/任务点位置是否在地图上正确标注
8. **冷却 UI**: 击杀/能力/破坏冷却倒计时是否在 UI 上正确显示

---

## 三、建议修复顺序

1. **启动 Play Mode** → 观察 Console 中的 NullReferenceException
2. **按 T2 Checklist A 节** → 验证主菜单流程
3. **按 T2 Checklist B 节** → 验证 Bot 局完整循环
4. **记录所有 error + 体验异常** → 填入下方表格
5. **按严重度逐项修复**

---

## 四、发现的问题（Play Mode 后记录）

| # | 严重度 | 描述 | 文件位置 | 状态 |
|---|--------|------|----------|------|
| — | — | 待 Play Mode 测试后记录 | — | — |
