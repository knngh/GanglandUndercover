# Gangland Undercover 阶段 E7：音频资产与事件系统设计

> 日期：2026-06-05  
> 状态：权威设计文档  
> 依赖：阶段 B1（标准局）、E5（VFX 和状态反馈）  
> 目标：定义完整的音频事件列表、破坏音效、环境音、BGM 策略和文件格式规范

---

## 1. 音频系统架构

### 1.1 当前状态

| 组件 | 状态 | 说明 |
|------|------|------|
| `AudioManager.cs` | ✅ 已实现 | 单例，13 项 SoundEffect 枚举，3 个 AudioSource |
| AudioClip 占位符 | ⚠️ 空槽 | Inspector 13 个字段均为 null，运行时静默跳过 |
| 接入点 | ✅ 已接 | SocialPrototypeController、GameController、OnlineMatchController 等均已接入 |
| AudioMixer | ⬜ 待做 | 可选增强：Master/SFX/Music/Ambient 四组分离 |

### 1.2 音频源分配

| AudioSource | 用途 | 空间化 | 循环 | 音量控制 |
|-------------|------|--------|------|----------|
| `sfxSource` | 所有短音效（UI / 事件 / 反馈） | 2D（全局）/ 3D（`PlayClipAtPoint`） | 否 | SFXVolume |
| `musicSource` | BGM（背景音乐） | 2D | 是（循环播放） | MusicVolume |
| `ambientSource` | 环境音（地图氛围） | 2D | 是（循环播放） | AmbientVolume |

### 1.3 音频文件格式规范

| 类别 | 格式 | 声道 | 采样率 | 压缩方式 | 最大文件大小 |
|------|------|------|--------|----------|-------------|
| SFX（短音效） | `.ogg`（Vorbis） | Mono | 44100 Hz | 质量 0.5（50%） | 200 KB / 文件 |
| Ambience（环境音） | `.ogg`（Vorbis） | Stereo | 44100 Hz | 质量 0.6（60%） | 2 MB / 文件 |
| BGM（背景音乐） | `.ogg`（Vorbis） | Stereo | 44100 Hz | 质量 0.7（70%） | 5 MB / 文件 |
| UI 音效 | `.ogg`（Vorbis） | Mono | 22050 Hz | 质量 0.3（30%） | 50 KB / 文件 |

**Unity 导入设置**：
- SFX: `Load Type = Decompress On Load`（短音效，减少延迟）
- Ambience: `Load Type = Streaming`（长音频，节省内存）
- BGM: `Load Type = Streaming`
- Compression Format: `Vorbis`

### 1.4 命名规范

```
sfx_{category}_{event}.ogg
  例: sfx_ui_click.ogg
      sfx_task_complete.ogg

amb_{map}_{environment}.ogg
  例: amb_harbour_rain.ogg
      amb_police_interior.ogg

bgm_{mood}_{intensity}.ogg
  例: bgm_explore_low.ogg
      bgm_meeting_tension.ogg
```

---

## 2. 完整音效事件列表

### 2.1 UI 音效

| ID | 事件名 | 文件名 | 触发时机 | 实现 | 优先级 |
|----|--------|--------|----------|------|--------|
| UI_01 | UI 点击 | `sfx_ui_click.ogg` | 任意按钮点击（菜单/大厅/设置） | `PlaySFX(UIClick)` | P0 |
| UI_02 | UI 确认 | `sfx_ui_confirm.ogg` | 确认弹窗/提交操作（如开始游戏） | `PlaySFX(UIClick)` + 变体 | P1 |
| UI_03 | UI 取消/返回 | `sfx_ui_cancel.ogg` | 取消/返回/关闭面板 | `PlaySFX(UIClick)` + 变体 | P2 |
| UI_04 | UI 错误 | `sfx_ui_error.ogg` | 操作被拒绝（如满员加入失败） | `PlaySFXAtPoint` | P1 |
| UI_05 | UI 悬停 | `sfx_ui_hover.ogg` | 鼠标悬停按钮（可选） | 可选，OnPointerEnter | P3 |
| UI_06 | UI 通知/toast | `sfx_ui_notify.ogg` | 重要提示出现（破坏/击杀/投票） | 新枚举值 | P1 |

**UI Click 当前覆盖范围**（已接入）：
- `MainMenuController.OnRoleSelected()`
- `MainMenuController.OnStartOffline()`
- `MainMenuController.OnEnterLobby()`
- `LobbyController.OnCreateRoom()`
- `LobbyController.OnJoinRoom()`
- `LobbyController.OnStartOnlineGame()`
- `LobbyController.OnBackToMenu()`
- `GameOverController.OnBackToMenu()`
- `GameOverController.OnReplay()`

### 2.2 任务音效

| ID | 事件名 | 文件名 | 触发时机 | 时长 | 优先级 |
|----|--------|--------|----------|------|--------|
| TASK_01 | 任务开始 | `sfx_task_start.ogg` | 玩家开始任意小游戏任务 | 0.5s | P0 |
| TASK_02 | 任务进行中（循环） | `sfx_task_progress.ogg` | 任务进行时低强度循环音（可选） | 循环 | P2 |
| TASK_03 | 任务完成 | `sfx_task_complete.ogg` | 任务成功完成（已接入 `TaskComplete`） | 1.0s | P0 |
| TASK_04 | 任务失败 | `sfx_task_fail.ogg` | 任务超时或错误次数用完 | 0.8s | P1 |
| TASK_05 | 连线正确 | `sfx_wire_connect.ogg` | Wire 任务：一根线连接成功 | 0.3s | P1 |
| TASK_06 | 按键按下 | `sfx_keypad_press.ogg` | Keypad 任务：按下数字键 | 0.1s | P2 |
| TASK_07 | 刷卡 | `sfx_swipecard_swipe.ogg` | SwipeCard 任务：刷卡动作 | 0.5s | P1 |
| TASK_08 | 扫描完成 | `sfx_scan_beep.ogg` | Scan 任务：扫描环停在绿色区域 | 0.3s | P1 |
| TASK_09 | 下载提示 | `sfx_download_ping.ogg` | Download 任务：下载进度 25%/50%/75% | 0.2s | P2 |
| TASK_10 | 文件夹放入 | `sfx_sort_place.ogg` | Sort 任务：文件夹正确放入槽位 | 0.3s | P2 |
| TASK_11 | 记忆灯亮 | `sfx_memory_blip.ogg` | Memory 任务：每个灯亮起音 | 0.1s | P1 |
| TASK_12 | 记忆正确/错误 | `sfx_memory_correct.ogg` / `sfx_memory_wrong.ogg` | Memory：点击结果反馈 | 0.3s | P1 |
| TASK_13 | 快速点击命中 | `sfx_tap_hit.ogg` | Tap 任务：点击目标成功 | 0.1s | P2 |
| TASK_14 | 快速点击错过 | `sfx_tap_miss.ogg` | Tap 任务：目标消失未被点击 | 0.2s | P2 |
| TASK_15 | 校准对齐 | `sfx_calibrate_lock.ogg` | Calibrate 任务：指针进入绿色区域 | 0.4s | P1 |
| TASK_16 | 雷达锁定 | `sfx_radar_lock.ogg` | Asteroid/Radar 任务：锁定目标 | 0.3s | P1 |
| TASK_17 | 证据归档 | `sfx_archive_file.ogg` | EvidenceArchive 任务：证物放入柜子 | 0.5s | P1 |

### 2.3 游戏事件音效

| ID | 事件名 | 文件名 | 触发时机 | 时长 | 优先级 |
|----|--------|--------|----------|------|--------|
| GAME_01 | 击杀 | `sfx_kill.ogg` | 黑帮击杀目标（已接入 `Kill`） | 1.0s | P0 |
| GAME_02 | 被击倒 | `sfx_down.ogg` | 玩家被击杀的受害者视角反馈 | 0.5s | P1 |
| GAME_03 | 尸体发现 | `sfx_body_discover.ogg` | 其他玩家靠近尸体时（发现者音效） | 0.4s | P1 |
| GAME_04 | 报案 | `sfx_body_report.ogg` | 玩家报告尸体（已接入 `BodyReport`） | 1.5s | P0 |
| GAME_05 | 紧急会议 | `sfx_emergency.ogg` | 玩家按紧急按钮召集会议（已接入 `Emergency`） | 1.0s | P0 |

### 2.4 会议与投票音效

| ID | 事件名 | 文件名 | 触发时机 | 时长 | 优先级 |
|----|--------|--------|----------|------|--------|
| MEET_01 | 会议开始 | `sfx_meeting_start.ogg` | 会议召集成功，切入会议画面（已接入 `MeetingStart`） | 2.0s | P0 |
| MEET_02 | 讨论阶段 Tick | `sfx_meeting_tick.ogg` | 讨论倒计时最后 5 秒每秒 Tick | 0.1s | P1 |
| MEET_03 | 投票确认 | `sfx_vote_cast.ogg` | 玩家投出票（已接入 `VoteCast`） | 0.5s | P0 |
| MEET_04 | 跳过投票 | `sfx_vote_skip.ogg` | 玩家选择跳过 | 0.3s | P1 |
| MEET_05 | 玩家淘汰 | `sfx_player_ejected.ogg` | 玩家被投票淘汰（已接入 `PlayerEliminated`） | 1.5s | P0 |
| MEET_06 | 平票 | `sfx_vote_tie.ogg` | 无人得票最高或平票 | 0.8s | P1 |
| MEET_07 | 会议结束 | `sfx_meeting_end.ogg` | 会议回到行动阶段 | 1.0s | P1 |

### 2.5 结算音效

| ID | 事件名 | 文件名 | 触发时机 | 时长 | 优先级 |
|----|--------|--------|----------|------|--------|
| END_01 | 警方胜利 | `sfx_victory_police.ogg` | 警方阵营胜利（已接入 `Victory`） | 3.0s | P0 |
| END_02 | 黑帮胜利 | `sfx_victory_gang.ogg` | 黑帮阵营胜利（已接入 `Victory` + 阵营判定） | 3.0s | P0 |
| END_03 | 失败（通用） | `sfx_defeat.ogg` | 所在阵营失败（已接入 `Defeat`） | 2.0s | P0 |
| END_04 | 卧底存活胜利 | `sfx_undercover_survive.ogg` | 卧底存活且警方胜利 | 2.0s | P1 |
| END_05 | 内鬼翻盘 | `sfx_mole_reveal.ogg` | Mole 达成隐藏目标 | 1.5s | P2 |

### 2.6 角色音效

| ID | 事件名 | 文件名 | 触发时机 | 空间化 | 优先级 |
|----|--------|--------|----------|--------|--------|
| CHAR_01 | 脚步声（室内） | `sfx_footstep_indoor.ogg` | 玩家移动时 0.4s 间隔（已接入 `Footstep`） | 3D | P0 |
| CHAR_02 | 脚步声（室外） | `sfx_footstep_outdoor.ogg` | 室外地面（水泥/街道） | 3D | P1 |
| CHAR_03 | 脚步声（金属） | `sfx_footstep_metal.ogg` | 金属地板（电房/货柜） | 3D | P1 |
| CHAR_04 | 脚步声（木地板） | `sfx_footstep_wood.ogg` | 木地板（茶餐厅/麻将馆） | 3D | P2 |
| CHAR_05 | 通风管爬行 | `sfx_vent_crawl.ogg` | 玩家通过暗线/通风管 | 3D | P1 |
| CHAR_06 | 通风管出口 | `sfx_vent_exit.ogg` | 暗线传送落地 | 3D | P1 |

---

## 3. 破坏系统音效

### 3.1 停电 (Blackout)

| ID | 事件名 | 文件名 | 描述 | 时长 | 触发条件 |
|----|--------|--------|------|------|----------|
| SAB_01 | 停电激活 | `sfx_blackout_start.ogg` | 电流切断嗡鸣，低沉 woom + 电力中断滋声 | 1.5s | 黑帮触发停电 |
| SAB_02 | 停电持续（循环） | `sfx_blackout_loop.ogg` | 低沉的电流嗡鸣（50Hz 低音 + 轻微 crackle） | 循环 | 停电期间 |
| SAB_03 | 停电修复 | `sfx_blackout_fix.ogg` | 电力恢复：继电器咔嗒声 + 电流回升嗡鸣 | 1.0s | 警方修复停电 |
| SAB_04 | 应急灯提示 | `sfx_emergency_light.ogg` | 应急灯亮起的电子提示音（短促 beep） | 0.3s | 停电开始后 0.5s |

### 3.2 锁门 (Lockdown)

| ID | 事件名 | 文件名 | 描述 | 时长 | 触发条件 |
|----|--------|--------|------|------|----------|
| SAB_05 | 锁门激活 | `sfx_lockdown_start.ogg` | 机械锁闭——沉重金属门闩咔嗒声 + 电子锁蜂鸣 | 1.0s | 黑帮触发锁门 |
| SAB_06 | 锁门持续（循环） | `sfx_lockdown_loop.ogg` | 间歇性门锁提示音（每 3 秒一声电子拒绝音） | 循环 | 锁门期间 |
| SAB_07 | 锁门解除 | `sfx_lockdown_end.ogg` | 门锁释放——电磁锁释放嗡鸣 + 门自动打开 | 1.0s | 时间到或修复 |

### 3.3 通讯干扰 (Comms Jam)

| ID | 事件名 | 文件名 | 描述 | 时长 | 触发条件 |
|----|--------|--------|------|------|----------|
| SAB_08 | 通讯干扰激活 | `sfx_jam_start.ogg` | 白噪声爆发 + 无线电静态噪音突增 | 1.0s | 黑帮触发通讯干扰 |
| SAB_09 | 通讯干扰持续（循环） | `sfx_jam_loop.ogg` | 无线电白噪声（带低频抖动和偶尔信号切入） | 循环 | 通讯干扰期间 |
| SAB_10 | 通讯干扰修复 | `sfx_jam_fix.ogg` | 信号恢复——静态噪音消退 + 清脆连接提示音 | 1.0s | 警方修复通讯 |

### 3.4 证据泄露 (Evidence Leak)

| ID | 事件名 | 文件名 | 描述 | 时长 | 触发条件 |
|----|--------|--------|------|------|----------|
| SAB_11 | 证据泄露激活 | `sfx_leak_start.ogg` | 数据泄露警报——尖锐电子警报 + 数据擦除音效 | 1.5s | 黑帮触发证据泄露 |
| SAB_12 | 证据泄露持续（循环） | `sfx_leak_loop.ogg` | 文件撕毁/数据断裂音效 + 低频脉冲 | 循环 | 证据泄露期间 |
| SAB_13 | 证据泄露修复 | `sfx_leak_fix.ogg` | 恢复完成——系统重启音 + 确认提示 | 1.0s | 警方修复证据泄露 |

### 3.5 巡逻警报 (Patrol Alert)

| ID | 事件名 | 文件名 | 描述 | 时长 | 触发条件 |
|----|--------|--------|------|------|----------|
| SAB_14 | 巡逻警报激活 | `sfx_patrol_start.ogg` | 警笛起鸣——WeeWoo 警笛声渐进（低音量） | 1.5s | 黑帮触发巡逻警报 |
| SAB_15 | 巡逻警报持续（循环） | `sfx_patrol_loop.ogg` | 低频警笛循环（远距离感，不刺耳） | 循环 | 巡逻警报期间 |
| SAB_16 | 巡逻警报解除 | `sfx_patrol_end.ogg` | 警笛消退——警笛远去 fade out + 无线电"解除警报" | 2.0s | 时间到或修复 |

---

## 4. 地图环境音（Ambience）

### 4.1 港区 (HarbourDistrict)

| ID | 文件名 | 描述 | 音频内容要素 | 时长 | 音量 |
|----|--------|------|-------------|------|------|
| AMB_H_01 | `amb_harbour_rain.ogg` | 港区雨夜氛围 | 大雨声（20% vol）、远处海浪拍打码头（10% vol）、偶尔远方船笛（每 30s）、三角铁/金属碰撞微声 | 60s 循环 | 0.4 |
| AMB_H_02 | `amb_harbour_night.ogg` | 港区夜间环境 | 轻微风声、远处交通（低频轰鸣）、偶尔狗吠、间歇性货柜起重机操作声 | 60s 循环 | 0.3 |
| AMB_H_03 | `amb_harbour_warehouse.ogg` | 货柜场/仓库室内 | 金属回声、低频电流嗡鸣、水滴回声、偶尔铁皮嘎吱声 | 60s 循环 | 0.25 |

**港区环境音房间变体**：
- **货柜场**：金属、起重机、低频风 → `amb_harbour_warehouse.ogg`
- **茶餐厅/夜市**：轻微厨房声、油炸声、收银机叮叮（用 `amb_harbour_night.ogg` + 局部 2D 音源叠加）
- **地下诊所**：低冷光哼声、药瓶碰撞、心电图 Beep（小范围局部音源）
- **监控室**：屏幕切换音、风扇声、键盘敲击微声

### 4.2 警署 (PoliceStation)

| ID | 文件名 | 描述 | 音频内容要素 | 时长 | 音量 |
|----|--------|------|-------------|------|------|
| AMB_P_01 | `amb_police_interior.ogg` | 警署室内氛围 | 空调低频嗡鸣、荧光灯轻微哼声、电话铃声（偶尔）、对讲机断续语音（远处，听不清内容）、脚步声回声 | 60s 循环 | 0.3 |
| AMB_P_02 | `amb_police_night.ogg` | 警署夜间（人少） | 更安静的空调声、更远的对讲机、偶尔椅子挪动声、墙面时钟滴答 | 60s 循环 | 0.2 |

**警署房间变体**：
- **审讯室**：门关上的重击声回音、偶尔键盘敲击（小范围 2D 音源）
- **证物室**：文件翻阅、储藏柜开门关门的微声
- **拘留室**：水滴声、金属栏栅微震、偶尔咳嗽或叹气声
- **监控室**：多台显示器高频微声（比港区监控室现代化）

### 4.3 九龙城寨 (KowloonWalledCity)

| ID | 文件名 | 描述 | 音频内容要素 | 时长 | 音量 |
|----|--------|------|-------------|------|------|
| AMB_K_01 | `amb_kowloon_alley.ogg` | 九龙城寨巷道氛围 | 远处麻将声、空调滴水、渗水管滴落、偶尔猫叫、霓虹灯牌电流声 | 60s 循环 | 0.35 |
| AMB_K_02 | `amb_kowloon_interior.ogg` | 九龙城寨室内氛围 | 老旧风扇声、药材味库房微声（干货咀嚼声）、楼板微震、远处打牌声 | 60s 循环 | 0.3 |
| AMB_K_03 | `amb_kowloon_night.ogg` | 九龙城寨深夜宁静 | 更少人声、风吹铁栏微震、远处警笛微声（偶尔）、滴水声为主 | 60s 循环 | 0.2 |

**九龙城寨房间变体**：
- **麻将馆**：洗牌声、骰子掷出、牌碰撞、麻将桌布摩擦（局部 2D 音源）
- **药材铺**：捣药声、木抽屉拉开关闭、老式收银机
- **天井**：上方通风管风声共振、铁栏嘎吱、滴水声
- **地下钱庄**：点钞机声、铁门重击、低沉人声
- **后巷**：排风管嗡鸣、垃圾桶碰撞、猫穿梭微声

---

## 5. BGM 策略

### 5.1 BGM 分层理念

BGM 不是一首从头播到尾的曲目，而是 **分层动态混合**：

| 层 | 名称 | 内容 | 触发条件 |
|-----|------|------|----------|
| Layer 0 | 沉默 | 无音乐（仅环境音） | 游戏启动 / 菜单（可选静音乐） |
| Layer 1 | 低强度探索 BGM | 低音 Pad + 氛围合成器 + 轻微打击乐 | 行动阶段，无事发生 |
| Layer 2 | 紧张提示 | Layer 1 + 低音弦乐颤音 | 附近有击杀/尸体被发现 |
| Layer 3 | 会议紧张 | 紧张弦乐 + 心跳鼓点 | 会议讨论阶段 |
| Layer 4 | 投票高潮 | 打击乐加强 + 管乐短 motif | 投票结果揭晓前 |

### 5.2 BGM 曲目清单

| ID | 文件名 | 描述 | 场景 | 强度 | 时长 | 优先级 |
|----|--------|------|------|------|------|--------|
| BGM_01 | `bgm_explore_low.ogg` | 低强度探索 | 行动阶段无事发生时 | 低 | 120s 循环 | P0 |
| BGM_02 | `bgm_threat_rising.ogg` | 威胁提示 | 击杀/尸体附近/黑帮接近 | 中 | 60s 循环 | P1 |
| BGM_03 | `bgm_meeting_tension.ogg` | 会议紧张 | 会议讨论阶段（投票前） | 中 | 90s 循环 | P0 |
| BGM_04 | `bgm_vote_climax.ogg` | 投票高潮 | 投票结果揭晓前 5 秒 | 高 | 15s | P1 |
| BGM_05 | `bgm_victory.ogg` | 警方胜利 | 结算画面：警方胜利 | — | 10s | P1 |
| BGM_06 | `bgm_gang_victory.ogg` | 黑帮胜利 | 结算画面：黑帮胜利 | — | 10s | P1 |
| BGM_07 | `bgm_main_menu.ogg` | 主菜单 | 主菜单背景 | 低 | 120s 循环 | P1 |
| BGM_08 | `bgm_lobby.ogg` | 大厅等待 | 房间大厅等待玩家 | 低 | 60s 循环 | P2 |

### 5.3 BGM 过渡策略

- **淡入淡出**：BGM 切换使用 2 秒 crossfade（`MusicSource` 目标音量渐变）。
- **Layer 混音**（可选增强）：
  - Unity AudioMixer 支持 snapshot 过渡。
  - 不同 Layer 的 AudioSource 通过 AudioMixer 分别控制。
  - Layer 0（沉默）→ Layer 1（探索）→ Layer 3（会议）逐步叠加。

### 5.4 地图主题变体

为避免 BGM 过于重复，3 张地图各有轻微变体：

| 地图 | BGM 情绪 | 特色乐器/音色 |
|------|----------|---------------|
| 港区 | 阴郁、潮湿 | 低音 Pad + 雨水合成 + 船笛/金属工业音色点缀 |
| 警署 | 冷峻、秩序 | 电子无人机 + 键盘点击类 perc + 远距离对讲机破音 |
| 九龙城寨 | 混沌、压抑 | 传统中国打击乐微声 + 麻将碰撞类 perc + 管笛 |

---

## 6. 音量与静音设置集成

### 6.1 音量映射

| 设置项 | PlayerPrefs Key | 默认值 | 映射目标 |
|--------|-----------------|--------|----------|
| 主音量 | `AudioMasterVolume` | 0.8 | 所有 AudioSource 的全局 multiplier |
| 音效音量 | `AudioSFXVolume` | 0.8 | `sfxSource.volume` |
| 音乐音量 | `AudioMusicVolume` | 0.6 | `musicSource.volume` |
| 环境音音量 | `AudioAmbientVolume` | 0.5 | `ambientSource.volume` |
| 静音 | `AudioMute` | false | 所有 volume = 0 |

### 6.2 AudioManager 音量控制接口

```csharp
// AudioManager.cs — 现有接口（已实现）
public float MasterVolume  { get; set; }  // 0.0 ~ 1.0
public float SFXVolume     { get; set; }  // 0.0 ~ 1.0
public float MusicVolume   { get; set; }  // 0.0 ~ 1.0
public bool  IsMuted       { get; set; }  // true/false

// 建议新增
public float AmbientVolume { get; set; }  // 0.0 ~ 1.0
```

- 所有 volume setter 包含 `Clamp(0, 1)` 保护。
- `MasterVolume` 作为所有子音量 multiplier：`source.volume = subVolume * masterVolume * (isMuted ? 0 : 1)`。
- `PlayerPrefs` 持久化：每次修改立即 `PlayerPrefs.Save()`。

### 6.3 破坏系统音量覆盖

破坏激活时，环境音和 BGM 音量短暂衰减，以突出破坏报警音效：

| 破坏 | 音量处理 |
|------|----------|
| 停电 | AmbientVolume × 0.3（电力消失，环境变暗），BGM 不变 |
| 锁门 | 无特殊覆盖 |
| 通讯干扰 | SFXVolume × 0.4（模拟通讯故障，其他音效变远） |
| 证据泄露 | BGM × 0.3（紧张氛围由警报音效代替） |
| 巡逻警报 | 所有 volume × 0.5，巡逻警报音效不受影响（最高优先级） |

---

## 7. 音频实现优先级

### 7.1 分批交付计划

| 批次 | 内容 | 文件数量 | 优先级 |
|------|------|----------|--------|
| 第 1 批 | 核心 SFX：UI 点击、任务完成、击杀、报案、会议开始/投票/淘汰、胜利/失败 | 15 文件 | P0 |
| 第 2 批 | 破坏音效：5 种破坏各 3-4 个音效 | 16 文件 | P0 |
| 第 3 批 | 地图环境音：3 张图各 2-3 个循环 | 8 文件 | P1 |
| 第 4 批 | BGM：探索、会议紧张、胜利、主菜单、大厅 | 6 文件 | P1 |
| 第 5 批 | 任务细节音效：各小游戏特有音效 | 17 文件 | P1-P2 |
| 第 6 批 | 角色脚步声（多表面）+ 通风管 | 5 文件 | P1 |
| 第 7 批 | 结算变体（卧底存活/内鬼翻盘等）+ 辅助音效 | 7 文件 | P2 |

### 7.2 音频资产总清单

| 类别 | 文件数 | 总预估大小 |
|------|--------|-----------|
| UI 音效 | 6 | ~150 KB |
| 任务音效 | 17 | ~800 KB |
| 游戏事件 | 5 | ~400 KB |
| 会议/投票 | 7 | ~500 KB |
| 结算 | 5 | ~800 KB |
| 角色音效 | 6 | ~600 KB |
| 破坏音效 | 16 | ~1.5 MB |
| 环境音 | 8 | ~12 MB |
| BGM | 8 | ~20 MB |
| **合计** | **78** | **~37 MB** |

---

## 8. 音频管线检查清单

### 8.1 开发检查

- [ ] 所有 78 个音频文件格式为 `.ogg` Vorbis。
- [ ] SFX 均为 Mono 44100 Hz，环境音和 BGM 为 Stereo 44100 Hz。
- [ ] 每个 AudioClip 在 `AudioManager` Inspector 中有对应占位槽（或动态加载）。
- [ ] 所有 PlaySFX 调用被 null-safe（已有保护）。
- [ ] 音量滑块在设置 UI 中实时生效。
- [ ] 静音开关正确作用于所有 AudioSource。

### 8.2 运行时检查

- [ ] 无音频时游戏不崩溃、不弹错误弹窗。
- [ ] 同时最多 3 个 AudioSource 不会互相覆盖。
- [ ] 环境音在场景切换时自动切换（Ambience 随地图变化）。
- [ ] BGM 在会议开始/结束时正确 crossfade。
- [ ] 破坏音效循环正确播放和停止（不泄漏 AudioSource）。

### 8.3 构建检查

- [ ] 音频文件正常导入并被构建包含（非 StreamingAssets 缺失）。
- [ ] `StreamingAssets` 目录仅包含 Streaming 加载的 ambience/BGM。
- [ ] 音频总大小 ≤ 40 MB（构建包可接受范围）。
- [ ] 音频延迟：SFX 播放延迟 < 50ms（Decompress On Load 保证）。

---

## 附录 A：AudioManager 枚举扩展建议

当前 `SoundEffect` 枚举共 13 项，建议扩展为更细分的版本：

```csharp
public enum SoundEffect
{
    // UI
    UIClick,
    UIConfirm,
    UICancel,
    UIError,
    UINotify,

    // Game Events
    Kill,
    BodyReport,
    BodyDiscovery,
    Emergency,

    // Meeting
    MeetingStart,
    MeetingTick,
    MeetingEnd,
    VoteCast,
    VoteSkip,
    VoteTie,
    PlayerEliminated,

    // Tasks
    TaskStart,
    TaskProgress,
    TaskComplete,
    TaskFail,

    // Sabotage (global trigger)
    Sabotage,
    SabotageFixed,

    // End
    Victory,
    Defeat,

    // Ambience / BGM control (non-sound)
    Ambient,
}
```

> **注意**：扩展枚举可能影响现有 `switch` 语句。建议新增枚举值追加在末尾，不影响旧索引。

## 附录 B：第三方音频资源来源建议

| 来源 | 许可 | 适用内容 |
|------|------|----------|
| Kenney 音效包 | CC0 | UI 点击、简单任务反馈 |
| freesound.org | CC0 / CC-BY | 环境音、脚步声 |
| OpenGameArt | CC0 / CC-BY | BGM、ambience |
| `AudioCraft` (Meta) | Research/Limited | AI 生成暂不考虑 |

> 所有使用的音频资源必须在 `Assets/_Project/Audio/ThirdParty/LICENSE.md` 中记录来源和许可。

## 附录 C：文件结构建议

```
Assets/_Project/
├── Audio/
│   ├── SFX/
│   │   ├── ui/
│   │   │   ├── sfx_ui_click.ogg
│   │   │   ├── sfx_ui_confirm.ogg
│   │   │   └── ...
│   │   ├── game/
│   │   │   ├── sfx_kill.ogg
│   │   │   ├── sfx_body_report.ogg
│   │   │   └── ...
│   │   ├── tasks/
│   │   │   ├── sfx_task_complete.ogg
│   │   │   └── ...
│   │   ├── meeting/
│   │   │   ├── sfx_meeting_start.ogg
│   │   │   └── ...
│   │   ├── sabotage/
│   │   │   ├── sfx_blackout_start.ogg
│   │   │   └── ...
│   │   ├── character/
│   │   │   ├── sfx_footstep_indoor.ogg
│   │   │   └── ...
│   │   └── endgame/
│   │       ├── sfx_victory_police.ogg
│   │       └── ...
│   ├── Ambience/
│   │   ├── amb_harbour_rain.ogg
│   │   ├── amb_police_interior.ogg
│   │   └── amb_kowloon_alley.ogg
│   ├── BGM/
│   │   ├── bgm_explore_low.ogg
│   │   ├── bgm_meeting_tension.ogg
│   │   └── ...
│   ├── ThirdParty/
│   │   └── LICENSE.md
│   └── AudioManager.cs                     # 已有
└── Scripts/
    └── Audio/
        └── AudioManager.cs                 # 已有
```
