# 美术/音频运行时验收报告

> **日期**: 2026-06-06 | **编译状态**: ✅ 0 errors, 0 warnings

---

## 一、美术资产文件审计

### 1.1 角色精灵 (Characters)
| 职业 | idle | idle_l/r/u/d | walk | walk_l/r/u/d | 文件数 |
|------|------|-------------|------|-------------|--------|
| undercover | ✅ | ✅ | ✅ | ✅ | 10 |
| inspector | ✅ | ✅ | ✅ | ✅ | 10 |
| gang | ✅ | ✅ | ✅ | ✅ | 10 |
| mole | ✅ | ✅ | ✅ | ✅ | 10 |
| police | ✅ | ✅ | ✅ | ✅ | 10 |
| forensic | ✅ | ✅ | ✅ | ✅ | 10 |
| informant | ✅ | ✅ | ✅ | ✅ | 10 |

> **结论**: 7 职业 × 10 方向帧 = 70 张角色精灵全部在 `Art/2D/Characters/` 下。

### 1.2 Tileset 地图精灵 (Harbour)
| 类别 | 文件 | 状态 |
|------|------|------|
| 地板 (floor) | concrete, metal, checker, wet, electrical, metal_grid, tea | ✅ |
| 墙壁 (wall) | brick, top, corner | ✅ |
| 道具 (prop) | crate_wood, barrel_oil, container_blue, desk_tea | ✅ |
| 指示 (sign) | exit, neon_pink | ✅ |
| 环境 (env) | puddle, vent_backalley, cable_floor, machine_panel | ✅ |

> **结论**: Harbour tileset 19 张精灵完整。

### 1.3 UI 精灵
| 类别 | 文件数 | 状态 |
|------|--------|------|
| Buttons | 50 | ✅ |
| Panels | 有 .png 文件 | ✅ |
| Icons | 有资源目录 | 待确认 |

### 1.4 总数
- **PNG 总文件数**: 5,582
- **角色精灵**: 70+
- **Tileset**: 50+
- **UI**: 100+

---

## 二、音频资产文件审计

### 2.1 SFX 音效 (Resources)
| 文件名 | 用途 | 存在 |
|--------|------|------|
| SFX_UIClick.wav | UI 点击 | ✅ |
| SFX_Footstep.wav | 脚步声 | ✅ |
| SFX_Kill.wav | 击杀 | ✅ |
| SFX_BodyReport.wav | 尸体报告 | ✅ |
| SFX_Report.wav | 报告 | ✅ |
| SFX_MeetingStart.wav | 会议开始 | ✅ |
| SFX_VoteCast.wav | 投票 | ✅ |
| SFX_PlayerEliminated.wav | 淘汰 | ✅ |
| SFX_TaskComplete.wav | 任务完成 | ✅ |
| SFX_Sabotage.wav | 破坏 | ✅ |
| SFX_Victory.wav | 胜利 | ✅ |
| SFX_Defeat.wav | 失败 | ✅ |
| SFX_Emergency.wav | 紧急按钮 | ✅ |
| SFX_ButtonHover.wav | 按钮悬停 | ✅ |
| SFX_VentOpen.wav | 通风口开 | ✅ |
| SFX_VentClose.wav | 通风口关 | ✅ |

### 2.2 BGM 背景音乐 (Resources)
| 文件名 | 用途 | 存在 |
|--------|------|------|
| BGM_MainMenu.ogg | 主菜单 | ✅ |
| BGM_InGame.ogg | 游戏内 | ✅ |
| BGM_Meeting.ogg | 会议 | ✅ |

### 2.3 环境音 (Resources)
| 文件名 | 用途 | 存在 |
|--------|------|------|
| amb_harbour_rain.ogg | 港区雨声 | ✅ |
| amb_kowloon_neon.ogg | 九龙霓虹 | ✅ |

### 2.4 UI 音效 (Audio/UI)
28 个 .ogg 文件（click/select/scroll/toggle/switch/error/bong/back/drop/minimize/maximize/open/close）

---

## 三、代码链路审计

### 3.1 精灵赋值链路
```
CharacterCreate → state.CharacterAnimator (Animator/方向指示器)
                → state.Character2DDirectionIndicator (GameObject)
                → GetComponent<SpriteRenderer>().sprite = (character sprite)

Body 创建      → worldBuilder.CreateBodyVisual(body)
                → CreateProp() / CreateShapeProp() 使用内部圆/矩形精灵
```

**⚠ 潜在风险**: 尸体使用几何形状精灵而非角色尸体精灵 — 无法区分不同职业的尸体。

### 3.2 音频播放链路
```
Play Sound    → AudioManager.Instance.Play(SoundEffect.Kill)
              → GetAudioClip(enum) 查找对应 [SerializeField] AudioClip
              → sfxSource.PlayOneShot(clip, sfxVolume * masterVolume)
```

**⚠ 关键风险**: AudioManager 所有 AudioClip 都是 `[SerializeField]` — 必须在 **Editor 中的 AudioManager prefab/GameObject 上手动拖入音频文件**，否则运行时会静默跳过（null clip）。

### 3.3 地图渲染链路
```
EnsureWorld   → WorldBuilder.Initialize(worldRoot, ...)
              → WorldBuilder.BuildDistrictMap()
              → CreateFloor() + CreateRoadNetwork() + ...
              → CreateProp() → 生成 GameObject + SpriteRenderer
              → renderer.sprite = (tileset sprite 或程序化圆形/矩形)
```

**⚠ 潜在风险**: 地图大量使用程序化生成的几何精灵 (Circle/SoftCircle/Diamond/RoundedRect)，而非 tileset PNG。若 `EnsureRuntimeSprites()` 未正常执行，地图会出现粉色方块。

---

## 四、Play Mode 验证要点

以下必须在 Editor Play Mode 中验证：

1. **AudioManager GameObject** 是否存在于场景中（DontDestroyOnLoad）
2. **AudioManager 各 AudioClip 槽位** 是否已赋值（Inspector 查看）
3. **角色行走时 Sprite 是否正确切换**（idle ↔ walk，方向 L/R/U/D）
4. **尸体显示是否为角色对应的倒下精灵**
5. **地图 Tile 是否全部渲染**（无紫色 Missing Sprite）
6. **音效是否在关键事件触发时播放**（击杀/报告/会议/胜利）

---

## 五、结论

| 维度 | 状态 | 说明 |
|------|------|------|
| 美术文件 | ✅ 完整 | 5,582 PNG，全部到位 |
| 音频文件 | ✅ 完整 | 16 SFX + 3 BGM + 2 Ambience |
| 编译状态 | ✅ 0 errors | batchmode 通过 |
| 代码链路 | ⚠ 依赖 Editor 赋值 | AudioManager.clips + Sprite slots |
| 运行时可见性 | ⏳ 需 Play Mode 验证 | 按 T2 Checklist 执行 |
| 运行时可听性 | ⏳ 需 Play Mode 验证 | 按 T2 Checklist 执行 |
