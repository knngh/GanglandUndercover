# 资产追踪表 + 集成就绪检查清单

> 日期：2026-06-05  
> 用途：全资产生产跟踪 + 代码集成侧就绪状态检查  
> 关联：art_audio_work_plan §十、E8 资源治理

---

## 一、资产追踪主表

### 1.1 美术资产追踪（全部 599 项，此处列出各阶段汇总行）

| 阶段 | 类别 | 预计文件数 | 状态 | 负责人 | 已交付 | 已导入 | 已验证 | 备注 |
|------|------|-----------|------|--------|--------|--------|--------|------|
| M1 | Art Bible | 1 doc | ⬜ 待启动 | 像素艺术家 | — | — | — | 含光照材质规则（Art Bible v1 已补充） |
| M1 | 角色目标稿 | 12 PNG | ⬜ 待启动 | 像素艺术家 | 0 | 0 | 0 | Enforcer×4 + Undercover×4 + Inspector×4 idle |
| M1 | 房间目标稿 | 8 PNG | ⬜ 待启动 | 像素艺术家 | 0 | 0 | 0 | 港区电房 tileset 7件 + 渲染截图1 |
| M2 | 7职业角色 | 154-176 | ⬜ 待启动 | 像素艺术家 | 0 | 0 | 0 | 22帧/职业 × 7-8职业 |
| M3 | 港区 tileset | ~106 | ⬜ 待启动 | 像素艺术家 | 0 | 0 | 0 | 12房间完整 tileset |
| M4 | UI 组件 | ~88 | ⬜ 待启动 | UI 设计师 | 0 | 0 | 0 | 全界面组件 |
| M5 | 任务站道具 | ~80 | ⬜ 待启动 | 像素艺术家 | 0 | 0 | 0 | 11种×4状态+子对象 |
| M5 | VFX sprite | ~32 | ⬜ 待启动 | 像素艺术家 | 0 | 0 | 0 | 5破坏+击杀+其他 |
| M6 | 警署 tileset | ~55 | ⬜ 待启动 | 像素艺术家 | 0 | 0 | 0 | 6房间 |
| M6 | 九龙城寨 tileset | ~62 | ⬜ 待启动 | 像素艺术家 | 0 | 0 | 0 | 8房间 |
| M7 | Sprite Atlas | 7 Atlas | ⬜ 待启动 | 技术美术 | 0 | 0 | 0 | 角色/三图tile/道具/UI/VFX |
| M8 | 封测收口 | 50+截图 | ⬜ 待启动 | 全员 | 0 | 0 | 0 | 全界面全分辨率截图巡检 |
| **合计** | | **~599** | | | **0** | **0** | **0** | |

### 1.2 音频资产追踪（全部 70 项）

| 阶段 | 类别 | 文件数 | 状态 | 负责人 | 已交付 | 已导入 | 已验证 | 备注 |
|------|------|--------|------|--------|--------|--------|--------|------|
| A1 | 核心 SFX | 15 | ⬜ 待启动 | 音效设计师 | 0 | 0 | 0 | UI/任务/事件/会议/结算 |
| A2 | 破坏音效 | 16 | ⬜ 待启动 | 音效设计师 | 0 | 0 | 0 | 5种破坏各3+应急灯 |
| A2 | 脚步声 | 5 | ⬜ 待启动 | 音效设计师 | 0 | 0 | 0 | 4表面+暗线 |
| A3 | 环境音 | 8 | ⬜ 待启动 | 音效设计师 | 0 | 0 | 0 | 3图各2-3循环 |
| A3 | BGM | 8 | ⬜ 待启动 | 配乐师 | 0 | 0 | 0 | 探索/威胁/会议/投票/胜利×2/菜单/大厅 |
| A4 | 任务细节 SFX | 12 | ⬜ 待启动 | 音效设计师 | 0 | 0 | 0 | 各小游戏特有音效 |
| A4 | 结算变体 | 4 | ⬜ 待启动 | 音效设计师 | 0 | 0 | 0 | 跳过/平票/卧底/Mole |
| A4 | 次要 SFX | 2+ | ⬜ 待启动 | 音效设计师 | 0 | 0 | 0 | P1/P2 补充 |
| **合计** | | **~70** | | | **0** | **0** | **0** | |

---

## 二、M1/A1 当前阶段详细追踪

### 2.1 M1 目标稿（本周）

| # | 文件名 | 尺寸 | 状态 | 评审 | 备注 |
|---|--------|------|------|------|------|
| 1 | Art Bible v1（含光照材质） | .md | ✅ 已完成 | — | `art_bible_v1_20260605.md` |
| 2 | `chr_inspector_front_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | 已有 §8.1 描述 |
| 3 | `chr_inspector_back_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | |
| 4 | `chr_inspector_left_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | |
| 5 | `chr_inspector_right_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | |
| 6 | `chr_enforcer_front_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | 见 m1_target_mockups §一 |
| 7 | `chr_enforcer_back_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | |
| 8 | `chr_enforcer_left_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | |
| 9 | `chr_enforcer_right_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | |
| 10 | `chr_undercover_front_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | 见 m1_target_mockups §二 |
| 11 | `chr_undercover_back_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | |
| 12 | `chr_undercover_left_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | |
| 13 | `chr_undercover_right_idle_alive.png` | 64×64 | ⬜ 待艺术家 | | |
| 14 | `tile_harbour_electric_floor_01.png` | 32×32 | ⬜ 待艺术家 | | 见 m1_target_mockups §三 |
| 15 | `tile_harbour_electric_wall_01.png` | 32×32 | ⬜ 待艺术家 | | |
| 16 | `tile_harbour_electric_prop_cabinet.png` | 64×96 | ⬜ 待艺术家 | | 配电柜大张 |
| 17 | `tile_harbour_electric_door_01.png` | 16×32 | ⬜ 待艺术家 | | |
| 18 | `tile_harbour_electric_prop_cable_01.png` | 8×32 | ⬜ 待艺术家 | | |
| 19 | `tile_harbour_electric_prop_danger.png` | 16×16 | ⬜ 待艺术家 | | 危险标记 ⚡ |
| 20 | `tile_harbour_electric_prop_light.png` | 8×16 | ⬜ 待艺术家 | | 应急红灯 |
| 21 | 电房渲染截图 | — | ⬜ 待艺术家 | | tile 拼接后整体效果 |

### 2.2 A1 核心 SFX（第2-3周）

| # | 文件名 | 时长 | 状态 | 评审 | 备注 |
|---|--------|------|------|------|------|
| — | A1 SFX 设计 Brief | .md | ✅ 已完成 | — | `a1_sfx_briefs_20260605.md` |
| 1 | `sfx_ui_click.ogg` | 0.1s | ⬜ 待音效师 | **样品1** | 最高频，听50次不烦 |
| 2 | `sfx_ui_confirm.ogg` | 0.25s | ⬜ 待音效师 | | 正向上扬 |
| 3 | `sfx_ui_error.ogg` | 0.3s | ⬜ 待音效师 | | 低频拒绝 |
| 4 | `sfx_ui_notify.ogg` | 0.5s | ⬜ 待音效师 | | 双段通知 |
| 5 | `sfx_task_start.ogg` | 0.5s | ⬜ 待音效师 | | 面板弹出感 |
| 6 | `sfx_task_complete.ogg` | 1.0s | ⬜ 待音效师 | | CEG上行琶音 |
| 7 | `sfx_kill.ogg` | 0.7s | ⬜ 待音效师 | **样品2** | 像素化暴力 |
| 8 | `sfx_body_report.ogg` | 0.9s | ⬜ 待音效师 | | 警笛感 |
| 9 | `sfx_emergency.ogg` | 1.3s | ⬜ 待音效师 | | 双音交替警报 |
| 10 | `sfx_meeting_start.ogg` | 1.7s | ⬜ 待音效师 | **样品3** | 氛围转场 drone |
| 11 | `sfx_vote_cast.ogg` | 0.25s | ⬜ 待音效师 | | 卡牌翻转 |
| 12 | `sfx_player_ejected.ogg` | 1.1s | ⬜ 待音效师 | | 下行沉重 |
| 13 | `sfx_victory_police.ogg` | 3.5s | ⬜ 待音效师 | | C大调庄严 |
| 14 | `sfx_victory_gang.ogg` | 3.5s | ⬜ 待音效师 | | Eb小调邪魅 |
| 15 | `sfx_defeat.ogg` | 2.2s | ⬜ 待音效师 | | 下行小三度 |

---

## 三、代码集成侧就绪状态检查

### 3.1 美术加载路径就绪检查

| 系统 | 脚本 | 状态 | 待办 |
|------|------|------|------|
| 角色 sprite 加载 | `Sprite2DAssetCache.cs` | ✅ 就绪 | 将程序化生成替换为 `Resources.Load<Sprite>("Art/2D/Characters/...")` |
| 地图 tile 加载 | `GreyboxMapBuilder.cs` + `RoomDecorator.cs` | ✅ 就绪 | tile 放入 `Assets/_Project/Art/2D/Tiles/` 后自动加载 |
| 任务站 sprite 加载 | `TaskStationController.cs` | ⚠️ 待确认 | 当前是否从程序化缓存加载还是 Resources？需检查 |
| VFX sprite 加载 | `SabotageVFX.cs` | ✅ 就绪 | sprite 帧放入 `Assets/_Project/Art/2D/VFX/` 后切换加载路径 |
| UI sprite 加载 | `UnifiedGameUI.cs` + Canvas 组件 | ✅ 就绪 | `Resources.Load<Sprite>("Art/2D/UI/...")` |

### 3.2 音频加载路径就绪检查

| 系统 | 脚本 | 状态 | 待办 |
|------|------|------|------|
| SFX 播放 | `AudioManager.cs` | ✅ 就绪 | Inspector 13 槽位填入正式 AudioClip |
| 环境音切换 | `AudioManager.cs` | ⚠️ 待接入 | 需要地图切换时自动加载对应环境音 |
| BGM 分层混音 | 未实现 | ❌ 待实现 | 需要创建 AudioMixer + 动态混音逻辑 |
| 破坏音量覆盖 | 未实现 | ❌ 待实现 | 需要音量覆盖规则（停电 Ambient×0.3 等） |

### 3.3 资源治理就绪检查

| 检查项 | 状态 | 说明 |
|--------|------|------|
| `Assets/_Project/Art/2D/` 目录结构 | ✅ 已建 | Characters/Tiles/UI/VFX 子目录已创建 |
| `Assets/_Project/Audio/SFX/` 目录结构 | ⚠️ 待建 | 需创建 ui/task/event/meeting/endgame 子目录 |
| `.gitignore` 排除规则 | ✅ 正确 | ThirdParty/AssetStore 已排，Art/Audio 目录不排 |
| 命名规范文档 | ✅ 完成 | Art Bible §7 + E8 §命名规范 |
| 导入设置文档 | ✅ 完成 | E8 §导入设置规范 |
| Clean Build 验证 | ✅ 通过 | batchmode 0 error（提交 85914a9d） |

---

## 四、外部人员就绪清单

### 4.1 像素艺术家启动包

交付给像素艺术家的文件：

- [ ] `output/art_bible_v1_20260605.md` — 美术圣经（含光照材质）
- [ ] `output/m1_target_mockups_20260605.md` — M1.2 目标稿设计 Brief
- [ ] `output/e4_task_station_props_20260605.md` — 11种任务站视觉定义（M5 用）
- [ ] `output/e8_asset_governance_20260605.md` — 命名/导入/Atlas 规范
- [ ] `output/art_audio_work_plan_20260605.md` — 完整工作计划

### 4.2 音效设计师启动包

交付给音效设计师的文件：

- [ ] `output/a1_sfx_briefs_20260605.md` — A1 核心 SFX 设计 Brief
- [ ] `output/e7_audio_asset_plan_20260605.md` — 完整音频系统设计
- [ ] `output/art_audio_work_plan_20260605.md` — 完整工作计划（含 A2-A4 后续阶段）

### 4.3 程序员待办（本周可做，不等艺术家）

| 任务 | 负责 | 预计工时 |
|------|------|----------|
| 创建 `Assets/_Project/Audio/SFX/` 子目录结构 | 程序员 | 5 min |
| 确认 `TaskStationController` 的 sprite 加载路径 | 程序员 | 30 min |
| 实现地图切换时自动加载环境音 | 程序员 | 2h |
| 创建 AudioMixer (Master/SFX/Music/Ambient) | 程序员 | 1h |
| 实现破坏音量覆盖规则 | 程序员 | 1h |
| 编写 `Resources.Load` fallback + 程序化占位切换逻辑 | 程序员 | 2h |

---

## 五、状态码定义

| 状态码 | 含义 |
|--------|------|
| ✅ 已完成 | 通过全部验收 |
| ⬜ 待启动 | 还未开始生产 |
| 🔄 进行中 | 正在生产/修改中 |
| ⚠️ 待确认 | 需要技术评审 |
| ❌ 阻塞 | 被外部依赖阻塞 |
| 🔷 待评审 | 已交付，等待评审结果 |
| ⭐ 已评审通过 | 评审通过，可进入下一阶段 |
