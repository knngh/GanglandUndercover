# Gangland Undercover — KNOWN_ISSUES.md

> **最后更新**: 2026-06-07 | **版本**: v0.1.0-pre

---

## 发布阻断 (P0) — 必须在首次发布前修复

### P0-1: BuildScript 场景路径不存在
- **影响**: macOS/Windows 构建直接失败，无法出包
- **原因**: `BuildScript.ScenePaths` 引用了 7 个不存在的场景（MainMenu/Lobby/HarbourDistrict/PoliceStation/KowloonWalledCity/GameOver/Tutorial），实际项目只有 `Prototype.unity` 和 `Stage1VerticalSlice.unity`
- **状态**: ✅ 已修复 — ScenePaths 改为 `Stage1VerticalSlice.unity` + `Prototype.unity`
- **等待**: 重新构建验证

---

## 高优先级 (P1) — 影响体验，建议发布前修复

### P1-1: AudioManager AudioClip 槽位未赋值
- **影响**: 所有音效（点击/击杀/报告/会议等）静默不播放
- **原因**: `AudioManager.cs` 使用 `[SerializeField] AudioClip`，未在 prefab 上拖入音频文件
- **复现**: 启动 Play Mode，触发任何音效事件，无声音
- **修复**: 在 Editor 中打开 AudioManager prefab，将 `Resources/Audio/SFX/*.wav` 和 `Resources/Audio/BGM/*.ogg` 拖入对应槽位
- **建议**: 同时在 `Awake()` 中添加 `Resources.Load` fallback

### P1-2: 角色动画控制器未验证
- **影响**: 角色移动时可能不显示 walk 帧动画（始终显示 idle 帧）
- **原因**: `Character2DDirectionIndicator` 依赖 Animator 组件切换 idle/walk sprite，但未验证 Animator Controller 是否配置
- **复现**: Play Mode 中移动角色，观察 sprite 是否切换
- **修复**: 检查 prefab 上 Animator 组件的 Controller 引用

### P1-3: 地图可能渲染纯色几何而非 CC0 tileset
- **影响**: 地图视觉为程序化圆形/矩形色块，而非像素 art tileset
- **原因**: `LoadCCTile()` 使用 `Resources.LoadAll<Texture2D>()` 加载第一个文件。若 Resources 路径不匹配，fallback 到 `DrawFloorWood()` 等程序化方法
- **复现**: Play Mode 中观察地图，若为纯色几何体则 CC0 未加载
- **修复**: 验证 `Sprites/Tilesets/*/floors/` 下的 PNG 文件 texture type 为 Sprite(2D and UI)

---

## 中优先级 (P2) — 可延至首次热更新

### P2-1: Resources 目录体积 832MB（冗余 3D 资产）
- **影响**: 构建速度慢、包体大（估计增加 500MB+）
- **原因**: Synty/Quaternius 3D 模型 + AssetStore Free Pack 大 WAV 文件仍在 Resources 中
- **修复**: 移动到 `Assets/_Project/Legacy3D/`，仅保留 `Resources/Sprites/` 和 `Resources/Audio/`
- **工作量**: 中（需验证无人引用被移动的资产）

### P2-2: 尸体显示使用几何图形而非角色精灵
- **影响**: 无法区分不同职业的尸体（全部一样）
- **原因**: `WorldBuilder.CreateBodyVisual()` 使用 `CreateProp()` 创建圆形/矩形，未使用角色对应的倒下精灵
- **修复**: 在 `CreateBodyVisual` 中根据 `body.VictimClientId` 查找职业并加载对应 sprite

### P2-3: Bot 不使用暗线通道
- **影响**: Bot 不会利用 vent/underworld 捷径，降低对局难度
- **修复**: 在 `OnlineBotController` 中添加通道寻路逻辑

---

## 低优先级 (P3) — 可延期

### P3-1: OnGUI 遗留代码未全量迁移到 uGUI Canvas
- **影响**: OnGUI 每帧调用，有轻微性能开销；某些 UI 元素不一致
- **状态**: 大部分已迁移到 `OnlineMatchController.OnGUI.cs` partial class
- **修复**: 逐项迁移剩余 OnGUI 绘制到 Canvas 预制件

### P3-2: 网络 Host 迁移未充分测试
- **影响**: 多人游戏中 Host 掉线时迁移可能失败
- **修复**: 需要多客户端环境测试

---

## 已修复 (归档)

> 无历史记录（首次建立 known issues 列表）

---

## 统计

| 优先级 | 数量 |
|--------|------|
| P0 阻断 | 1 (已修复，等验证) |
| P1 高 | 3 |
| P2 中 | 3 |
| P3 低 | 2 |
| **总计** | **9** |
