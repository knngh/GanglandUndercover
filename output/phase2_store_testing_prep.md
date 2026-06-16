# Gangland Undercover / 港区潜线 -- 商店页与外部测试准备

> **产出日期**: 2026-06-16
> **版本基线**: v0.2.0-dev (commit fd784c71)
> **状态**: 阶段 2 商店页 + 外部测试准备
> **参考文档**: steam_store_copy_draft_20260610, steam_screenshot_checklist_20260610, friend_remote_test_runbook_20260610, playtest_feedback_form_20260610, art_bible_v1_20260605, KNOWN_ISSUES, full_qa_checklist_20260609

---

## 目录

1. [Steam 截图 Shot List](#1-steam-截图-shot-list)
2. [Steam 商店介绍文案](#2-steam-商店介绍文案)
3. [朋友远程测试表单](#3-朋友远程测试表单)

---

# 1. Steam 截图 Shot List

## 1.1 截图前画面准备清单

在拍摄任何截图之前，必须确认以下准备工作全部完成：

| # | 准备项 | 操作 | 确认 |
|---|--------|------|------|
| P1 | **关闭调试面板** | 隐藏所有 OnGUI 调试叠加面板，确认无 `[Debug]` 面板可见 | |
| P2 | **关闭 FPS 计数器** | 如 HUD 有 FPS 显示，设为不可见 | |
| P3 | **关闭色盲模式** | 设置中心 > 色盲模式 = 0（关闭） | |
| P4 | **画质设为高** | 设置中心 > 画质 = 高 | |
| P5 | **全屏模式** | 设置中心 > 窗口模式 = 全屏 | |
| P6 | **语言设为中文** | 设置中心 > 语言 = zh-CN（后续如需英文截图再切换） | |
| P7 | **音量正常** | 主音量 = 80%，确保音效正常（截图虽无声但确认 BGM 播放中可避免静音状态异常） | |
| P8 | **无 Unity Editor 窗口** | 使用独立构建包（macOS FriendTest 或 Windows Steam 包）截图，不使用 Editor PlayMode | |
| P9 | **聊天区清空** | 行动阶段截图前确保聊天区无系统调试消息 | |
| P10 | **AI 玩家就位** | 开启 AI 补位，确保画面中有足够角色（至少 4 人） | |

## 1.2 截图分辨率与标注要求

| 属性 | 要求 |
|------|------|
| **最低分辨率** | 1280x720 (Steam 硬性要求) |
| **推荐分辨率** | 1920x1080 (Full HD) |
| **4K 截图** | 3840x2160 (用于后期缩放和裁切) |
| **格式** | PNG (优先) 或 JPG |
| **宽高比** | 16:9 |
| **水印** | 禁止任何水印 |
| **调试信息** | 禁止 FPS 计数器、Unity Editor 边框、OnGUI 调试面板 |
| **命名规范** | `store_shot_XX_[场景]_[描述].png`，例：`store_shot_01_mainmenu_title.png` |

**拍摄工具**:

- 方式 A: Unity Editor PlayMode > `Gangland > Screenshots > Capture Current Screen (4K)` > 后期缩放到 1920x1080
- 方式 B: 独立构建包全屏运行 > 系统截图 (macOS: Cmd+Shift+3; Windows: PrintScreen)
- 方式 C: 4K 截图后期用 Photoshop/GIMP 缩放到 1920x1080，使用 Point filter 保持像素锐利

## 1.3 必须截图 (Steam 商店页最低要求: 5 张)

### Shot 01 -- 主菜单全景

| 属性 | 说明 |
|------|------|
| **场景** | 主菜单 |
| **构图** | 完整主菜单双面板布局：左侧离线模式面板（四个身份按钮：卧底/黑帮/警察/线人），右侧联机面板（匿名登录 + 玩家代号输入框），中央游戏标题"港区潜线"带发光/呼吸动画，底部版本号 |
| **UI 状态** | 设置状态行可见（"音量 80% / 画质 高 / 全屏 / 色盲 0"），"打开设置"按钮可见 |
| **玩法元素** | 展示游戏身份系统和联机入口 |
| **拍摄时机** | 主菜单完全加载后，标题动画处于呼吸态 |
| **参考美术** | 港区夜景背景 + 标题 neon 字体，主色调港夜深蓝 `#1a1c2c`，暖色高光 `#f4a236` |

### Shot 02 -- 联机大厅 (Lobby)

| 属性 | 说明 |
|------|------|
| **场景** | 联机大厅 |
| **构图** | "联机大厅"标题（橙黄色渐变发光字），房间码输入框（placeholder "输入 4-6 位房间码"），"创建房间"橙色按钮 + "加入房间"蓝色按钮，下方玩家列表（4 人满位，含房主 + 3 个"等待加入..."空位），底部"准备"/"开始游戏"/"离开房间"三按钮 |
| **UI 状态** | 网络状态栏显示 Cloud/Auth/Lobby/Relay 可用状态，已创建房间并显示 6 位房间码 |
| **玩法元素** | 展示联机匹配入口和房间管理 |
| **拍摄时机** | Host 创建房间后、房间码可见时 |
| **已有参考** | `Screenshots/gangland-online-demo.png`（当前 Lobby 界面截图） |

### Shot 03 -- 行动阶段地图全景

| 属性 | 说明 |
|------|------|
| **场景** | Action 阶段 / 港区地图 |
| **构图** | 大地图全景，展示 2D 像素风港区场景：货柜场、海关、夜市、茶餐厅、电房、后巷等区域可见。多个角色（至少 4 名不同职业）分布在地图不同位置，任务站有发光环标记（alpha=0.4, 脉冲 2Hz），霓虹灯牌提供暖色点光源，全局港夜深蓝底色 |
| **UI 状态** | HUD 可见：左下任务进度、右下手持能力、顶部计时/证据条、聊天输入提示"按 Enter 发言..." |
| **玩法元素** | 展示地图探索、任务系统、职业多样性 |
| **拍摄时机** | Action 阶段开局 10-15 秒后，AI 玩家分散到各区域时 |
| **美术要点** | 地面雨后反光条纹（蓝紫色 low-alpha），霓虹灯牌 `#f4a236` 暖黄光，各房间独特地板 tile 和颜色基调 |

### Shot 04 -- 会议投票界面

| 属性 | 说明 |
|------|------|
| **场景** | Meeting / 会议阶段 |
| **构图** | 圆桌会议背景（深色纹理桌面），周围玩家头像卡牌（4 上 4 下或按实际人数），上方"会议中 15s"倒计时和案件原因，下方投票面板（各玩家旁有投票按钮和跳过按钮），右侧证据墙/案件档案，背景暗蓝半透明遮罩 |
| **UI 状态** | 投票卡可见，玩家席位有高亮标识，聊天区显示"会议聊天"频道标签 |
| **玩法元素** | 展示核心推理/投票环节 |
| **拍摄时机** | 会议阶段投票进行中，至少 1-2 张投票卡已翻转 |
| **美术要点** | 港夜霓虹风格统一色调，嫌疑人高亮（暖黄），投票结果展示时更佳 |

### Shot 05 -- 文本聊天与社交互动

| 属性 | 说明 |
|------|------|
| **场景** | Action 阶段 / 聊天活跃状态 |
| **构图** | 地图背景虚化/暗化，前景聚焦聊天区域：多条聊天消息可见，带频道标签（`[近]` 近距离聊天 / `[全]` 全局），发送者名称显示，聊天输入框处于激活状态。HUD 行动快捷区可见"举报最近"/"屏蔽最近"按钮（高亮可点击状态） |
| **UI 状态** | 聊天框活跃，消息气泡可见，频道标签正确 |
| **玩法元素** | 展示纯文本沟通核心机制和聊天安全功能 |
| **拍摄时机** | 发送 2-3 条聊天消息后、频道标签可见时 |

## 1.4 加分截图 (额外 5-10 张)

### Shot 06 -- 身份选择 / 角色展示

| 属性 | 说明 |
|------|------|
| **场景** | 主菜单离线面板 / 身份选择 |
| **构图** | 四个身份按钮并排（卧底/黑帮/警察/线人），选中一个身份后描述文字展开。如角色 sprite 已就绪，可展示 5-7 个职业角色并排站立（Inspector 警蓝 / Tech 青 / Forensics 墨绿 / Enforcer 赤红 / Fixer 灰黑 / UndercoverAgent 暗紫 / Driver 金黄） |
| **拍摄时机** | 身份按钮选中高亮时 |
| **玩法元素** | 四方势力设定 |

### Shot 07 -- 破坏效果：停电 (Blackout)

| 属性 | 说明 |
|------|------|
| **场景** | Action 阶段 / 停电破坏触发 |
| **构图** | 全屏暗蓝遮罩（alpha=0.75），应急红灯闪烁（`#c0392b` 红色点光源，强度 0.7 脉冲），地图仅见红色应急灯和角色轮廓。HUD 显示停电状态和修复提示 |
| **拍摄时机** | 黑帮触发停电破坏后立即截取 |
| **美术要点** | VFX 层 `vfx_sabotage_blackout` 遮罩，不可遮挡核心 UI 和角色识别 |

### Shot 08 -- 任务交互特写

| 属性 | 说明 |
|------|------|
| **场景** | Action 阶段 / 任务站 |
| **构图** | 角色站在任务站旁，任务站发光环可见，交互提示 UI 出现。如任务小游戏（数字校验/证据扫描等）界面已就绪，展示任务 overlay |
| **拍摄时机** | 按 E 键触发任务交互的瞬间 |
| **玩法元素** | 展示证据收集系统 |

### Shot 09 -- 投票结果 / 驱逐 (Ejection)

| 属性 | 说明 |
|------|------|
| **场景** | Meeting 结束 / 投票结果 |
| **构图** | 投票统计结果展示，被驱逐玩家高亮，如开启了"出局公开身份"则显示身份揭晓。角色鬼魂态（灰色调色板 + alpha=0.35）可见 |
| **拍摄时机** | 投票结果揭晓瞬间 |
| **玩法元素** | 展示驱逐机制和身份揭露 |

### Shot 10 -- 结算画面 (Result)

| 属性 | 说明 |
|------|------|
| **场景** | Result 阶段 |
| **构图** | 胜负结果展示，各玩家职业揭晓，隐藏目标达成状态，"返回大厅"/"重新开始"按钮可见 |
| **拍摄时机** | 对局结束进入结算时 |
| **玩法元素** | 展示胜负条件和职业揭晓 |

### Shot 11 -- 设置中心

| 属性 | 说明 |
|------|------|
| **场景** | 主菜单 > 设置覆盖层 |
| **构图** | 半透明深色覆盖层弹出，标题"设置中心"，5 个滑块（主音量/音效/音乐/聊天音量/鼠标灵敏度）+ 8 个按钮（画质/窗口/色盲/自由发言/帧率/语言/重置/关闭），底部"设置会立即保存并应用" |
| **拍摄时机** | 覆盖层完全展开时 |
| **玩法元素** | 展示 PC 端设置功能完整度 |

### Shot 12 -- 尸体发现与举报

| 属性 | 说明 |
|------|------|
| **场景** | Action 阶段 / 发现尸体 |
| **构图** | 角色站在尸体旁，尸体显示对应角色倒下 sprite + 红色半透明 X 标记（FloorDecal 层），血迹溅射可见。HUD 显示"按 R 举报"提示 |
| **拍摄时机** | 黑帮击杀后、其他玩家发现尸体时 |
| **美术要点** | 击杀现场 VFX（血迹溅射 + 尸体红色 X），持久显示于 FloorDecal 层 |

### Shot 13 -- 封锁门 / 锁门破坏

| 属性 | 说明 |
|------|------|
| **场景** | Action 阶段 / 锁门破坏触发 |
| **构图** | 房间入口处显示红色 X 标记 + 倒计时数字，被困玩家视角 |
| **拍摄时机** | 黑帮触发锁门破坏后 |

### Shot 14 -- 通讯干扰效果

| 属性 | 说明 |
|------|------|
| **场景** | Action 阶段 / 通讯干扰破坏 |
| **构图** | 小地图区域显示雪花噪点，UI 出现 glitch 效果，聊天区显示干扰状态 |
| **拍摄时机** | 黑帮触发通讯干扰后 |

### Shot 15 -- 举报/屏蔽安全功能

| 属性 | 说明 |
|------|------|
| **场景** | Action 阶段 / Canvas HUD |
| **构图** | HUD 行动快捷区"举报最近"/"屏蔽最近"按钮高亮可点击状态，旁边显示"已屏蔽 N / 举报 N"状态 |
| **拍摄时机** | 发送消息后按钮激活时 |
| **玩法元素** | 展示聊天安全系统 |

## 1.5 胶囊图 (Header Capsule / Capsule) 设计建议

### Header Capsule (460x215 px)

| 属性 | 建议 |
|------|------|
| **构图** | 港区夜景背景（深蓝 `#1a1c2c`），标题"港区潜线"居中用霓虹暖黄 `#f4a236` 发光字体，下方副标题英文 "Gangland Undercover"，四角点缀 4 个职业角色剪影（警蓝/赤红/暗紫/金黄），底部雨后地面反光条纹 |
| **元素** | 2-3 个霓虹灯牌点缀、远处货柜吊臂剪影、近处茶餐厅招牌轮廓 |
| **禁忌** | 不放 HUD、不放聊天框、不放纯文字列表 |

### Small Capsule (231x87 px)

| 属性 | 建议 |
|------|------|
| **构图** | 简化版 Header Capsule：港区剪影 + 标题 + 1-2 个角色剪影 |

### Large Capsule (616x353 px)

| 属性 | 建议 |
|------|------|
| **构图** | 与 Header Capsule 同构但更宽，可加入更多场景细节（霓虹街景、角色群像） |

### Hero Capsule (1920x620 px)

| 属性 | 建议 |
|------|------|
| **构图** | 宽幅港区全景：左侧角色群像（4-5 名不同职业），右侧港区夜景（货柜场、霓虹灯、茶餐厅），标题居左或居中 |

## 1.6 Steam 截图拍摄执行顺序

```
1. 启动独立构建包（macOS FriendTest 或 Windows Steam 包）
2. 确认准备清单 P1-P10 全部就绪
3. Shot 01: 主菜单全景
4. Shot 11: 设置中心（点击"打开设置"）
5. 关闭设置 > 进入联机大厅
6. Shot 02: 联机大厅（创建房间后截取）
7. AI 补位 > 准备 > 开始对局
8. Shot 03: 行动阶段地图全景（开局 10-15 秒后）
9. Shot 05: 聊天互动（发 2-3 条消息后截取）
10. Shot 08: 任务交互（走到任务站按 E）
11. Shot 15: 举报/屏蔽按钮
12. Shot 12: 触发击杀后拍摄尸体
13. 触发黑帮破坏 > Shot 07: 停电效果
14. Shot 13: 锁门效果
15. Shot 14: 通讯干扰效果
16. 按 R 举报或按紧急铃 > Shot 04: 会议投票
17. Shot 09: 投票结果
18. 跑完结局 > Shot 10: 结算画面
19. 返回主菜单 > Shot 06: 身份选择
20. 所有截图复制到 output/ 并按命名规范重命名
```

---

# 2. Steam 商店介绍文案

## 2.1 基本信息

| 字段 | 内容 |
|------|------|
| 游戏名（中） | 港区潜线 |
| 游戏名（英） | Gangland Undercover |
| 类型 | 在线多人社交推理 |
| 发行日期 | TBD |
| 开发商 | TBD |
| 发行商 | TBD |
| 引擎 | Unity 6000.4 |
| 平台 | Windows / macOS (Steam) |
| 玩家数 | 4-10 人在线 (支持 AI 补位) |
| 单局时长 | 8-15 分钟 |

## 2.2 Steam 标签建议 (Tags)

按 Steam 推荐标签系统，建议以下 20 个标签（按优先级排列）：

```
Social Deduction    Online Co-op    Multiplayer
Investigation       Crime           2D
Strategy            Noir            Pixel Graphics
Party Game          Dark Humor      Psychological
PvP                 Mystery         Text-Based
Indie               Atmospheric     Top-Down
Casual              Detective       Co-op
```

## 2.3 短描述 (Short Description)

Steam 限制约 300 字符。

### 中文版

```
四方势力，九龙暗战。卧底在黑帮内部窃取证据，线人混入警局通风报信。文字聊天是唯一的武器——你的每句话都可能是谎言，也可能是致命证据。4-10 人联机推理，10 分钟一局。
```

### English

```
Four factions. One harbor district. No voice — only text. Go undercover to steal evidence, plant a mole inside the police, sabotage the investigation, or hunt the traitor. A 4-10 player online social deduction game set in the neon-drenched streets of 1990s Kowloon. Every message is a weapon. Every silence is a confession.
```

## 2.4 详细描述 (About This Game)

### 中文版

---

**关于这款游戏**

**【九龙港区，四方暗流】**

1990 年代，九龙港区。黑帮掌控街头，警方布下天罗地网。你可能是警察，带队收网清剿；你可能是黑帮，掌控地下秩序；你可能是卧底，潜入黑帮窃取证据——也可能是线人，混入警局替黑帮通风报信。身份是秘密，信任是筹码，文字是唯一的武器。

**【核心特色】**

**纯文字推理 -- 沉默、撒谎、话术**
没有语音频道。你的每一句话都可能暴露身份，也可能误导对手。近距离聊天只对身边人可见，全局频道向所有人广播，会议阶段所有存活玩家公开辩论。选择说什么、对谁说、什么时候沉默——这就是你的武器库。

**四方势力 -- 不是简单的警察抓小偷**
- **警察**（Inspector / Forensics / Tech）：完成证据任务、拼出证据链、在会议中锁定嫌疑人
- **黑帮**（Enforcer / Fixer / Driver）：破坏证据、暗杀调查员、制造停电和封锁扰乱警方
- **卧底**（Undercover Agent）：潜入黑帮内部窃取证据的双面间谍，既要获取黑帮信任，又不能被警方误杀
- **线人**（Mole）：混入警局的内应，外观与警察无异，暗中为黑帮传递情报、破坏证据链

**证据链系统 -- 拼图比碎片更重要**
散布在港区各处的不只是单个线索，而是需要串联的证据链。警察必须收集足够多的证据碎片并拼出完整链条，才能在会议中锁定罪犯。黑帮则要赶在证据链完成前将其破坏。

**破坏与反制 -- 黑灯、锁门、断讯**
黑帮可以触发多种破坏事件：全场停电（应急红灯闪烁）、锁门封锁（困住调查员）、通讯干扰（小地图雪花噪点）、证据泄露。警方必须分工抢修，否则局面将彻底失控。

**7 种职业，各有绝活**
Inspector（警探）、Forensics（法医）、Tech（技术员）、Undercover Agent（卧底）、Enforcer（打手）、Fixer（善后者）、Driver（车手）。每个职业都有独特的对局能力（按 F 键触发），从监控扫描到紧急修复，从追踪定位到快速撤离。

**九龙港风美术 -- 霓虹灯下的像素世界**
2D 像素风格打造的九龙港区：货柜场、海关、夜市、茶餐厅、指挥车、证物室、地下诊所、电房、天台路线、监控室、金融巷、后巷大排档、警车、路障、霓虹招牌、紧急警铃——每一步都是港片场景。

**【游戏模式】**

- **在线联机**：4-10 人在线对局，支持 Relay 房间码一键开房，AI 自动补位确保满员开局
- **离线练习**：单人沙盒模式，AI 扮演所有对手，用于熟悉操作和规则
- **匿名登录**：无需注册账号，一键进入大厅即可开玩

**【无障碍功能】**

- 色盲辅助：4 套配色方案（正常/红绿色盲/蓝黄色盲/全色盲），阵营识别不依赖颜色
- 文字大小可调：HUD 字号支持设置调节
- 举报与屏蔽：内置聊天安全系统，支持举报不当消息和屏蔽特定玩家

**【当前状态】**

抢先体验阶段。核心 4-10 人对局完整可玩。后续计划追加更多地图（警署、九龙城寨）、更多职业和更大规模对局。

---

### English Version

---

**About This Game**

**【KOWLOON HARBOR, 1990s】**

The gangs run the streets. The police are closing in. You might be a cop building a case. You might be a gangster covering your tracks. You might be an undercover agent stealing evidence from within the gang -- or a mole inside the police force, feeding intel back to the criminals. Identity is secret. Trust is currency. Every message is a weapon.

**【CORE FEATURES】**

**Pure Text Deduction -- Silence, Lies, Persuasion**
No voice channels. Your words determine who lives and who gets voted out. Proximity chat is only visible to nearby players. Global broadcast reaches everyone. Meeting phases turn into open debate. Choose what to say, who to say it to, and when to stay silent -- that is your arsenal.

**Four Factions -- Not Just Cops and Robbers**
- **Police** (Inspector / Forensics / Tech): Complete evidence tasks, assemble evidence chains, identify suspects in meetings
- **Gang** (Enforcer / Fixer / Driver): Sabotage evidence, eliminate investigators, trigger blackouts and lockdowns
- **Undercover Agent**: A double agent embedded in the gang -- steal evidence while maintaining cover, without being killed by your own side
- **Mole**: A police informant who looks identical to real officers but secretly feeds intel to the gang and sabotages evidence chains

**Evidence Chain System -- The Puzzle Matters More Than the Pieces**
Scattered across the harbor district are not just individual clues, but interconnected evidence chains. Police must collect enough fragments and assemble the complete chain to lock down suspects in meetings. Gang must destroy the chain before it is completed.

**Sabotage & Counter-Play -- Blackouts, Lockdowns, Comms Jams**
Gang can trigger multiple sabotage events: district-wide blackouts (emergency red lights flickering), door lockdowns (trapping investigators), communications interference (minimap static), and evidence leaks. Police must coordinate repairs or watch the situation spiral out of control.

**7 Professions, Each With Unique Abilities**
Inspector, Forensics, Tech, Undercover Agent, Enforcer, Fixer, Driver. Each profession has a unique match ability (press F to activate), from surveillance scans to emergency repairs, from tracking to rapid extraction.

**Kowloon Noir Aesthetic -- A Pixel World Under Neon Lights**
2D pixel art brings the Kowloon harbor district to life: container yards, customs gates, night markets, cha chaan teng tea houses, command vans, evidence rooms, underground clinics, power rooms, rooftop routes, CCTV rooms, finance alleys, back-lane food stalls, police vans, roadblocks, neon signage, and emergency bells -- every step feels like a Hong Kong crime film.

**【GAME MODES】**

- **Online Multiplayer**: 4-10 players online, one-click Relay room codes, AI auto-fill ensures full lobbies
- **Offline Practice**: Single-player sandbox with AI opponents for learning controls and rules
- **Anonymous Login**: No account required, one click to enter the lobby and start playing

**【ACCESSIBILITY】**

- Color-blind support: 4 palette presets (normal / red-green / blue-yellow / monochrome), faction identification does not rely on color alone
- Adjustable text size: HUD font size configurable in settings
- Report & block: Built-in chat safety with message reporting and player blocking

**【CURRENT STATE】**

Early Access. Core 4-10 player loop is fully playable. More maps (Police Precinct, Kowloon Walled City), additional professions, and larger lobby sizes planned.

---

## 2.5 系统需求

### 中文版

| | 最低配置 | 推荐配置 |
|------|---------|---------|
| **操作系统** | Windows 10 64-bit / macOS 12+ | Windows 11 / macOS 14+ |
| **处理器** | Intel i3 / Apple M1 | Intel i5 / Apple M2 |
| **内存** | 4 GB RAM | 8 GB RAM |
| **显卡** | 集成显卡 (Intel HD 620+) | 独立显卡 |
| **存储空间** | 600 MB | 1 GB |
| **网络** | 宽带互联网连接（联机游戏需要） | 宽带互联网连接 |
| **DirectX** | 版本 11 | 版本 12 |

### English

| | Minimum | Recommended |
|------|---------|------------|
| **OS** | Windows 10 64-bit / macOS 12+ | Windows 11 / macOS 14+ |
| **Processor** | Intel i3 / Apple M1 | Intel i5 / Apple M2 |
| **Memory** | 4 GB RAM | 8 GB RAM |
| **Graphics** | Integrated (Intel HD 620+) | Dedicated GPU |
| **Storage** | 600 MB | 1 GB |
| **Network** | Broadband Internet (required for online play) | Broadband Internet |
| **DirectX** | Version 11 | Version 12 |

## 2.6 发布前检查清单

- [ ] 截图 10+ 张（按本 Shot List 拍摄）
- [ ] Header Capsule (460x215) 完成
- [ ] Small Capsule (231x87) 完成
- [ ] Large Capsule (616x353) 完成
- [ ] Hero Capsule (1920x620) 完成
- [ ] 宣传片/预告片（30-60 秒）
- [ ] 游戏内 Credits 面板完成
- [ ] EULA / Privacy Policy 链接
- [ ] Steamworks 后台语言设好 zh-CN + en-US
- [ ] 社区中心开启（讨论区 + 截图区）
- [ ] Windows x64 .exe 构建成功（需安装 Unity Windows Build Support）
- [ ] 构建包体积 >= 600 MB（含 SteamVisualArchive）
- [ ] macOS 构建签名（如适用）

---

# 3. 朋友远程测试表单

## 3.1 测试前问卷 -- 玩家背景与设备信息

> **填写人**: ____________
> **测试日期**: ____________
> **构建包**: `GanglandUndercover-FriendTest-macOS-20260610.zip` (84 MB)
> **构建 commit**: `fd784c71`
> **sha256**: `a3ec614cee330acce902b3286430c8ba0b80f00a794347cf2e71289b34207806`

### A. 设备信息

| # | 问题 | 填写 |
|---|------|------|
| A1 | 操作系统 | [ ] macOS [ ] Windows |
| A2 | 系统版本（如 macOS 15.0 / Windows 11） | ____________ |
| A3 | CPU 型号 | ____________ |
| A4 | 内存大小 | ____________ GB |
| A5 | 显卡型号 | ____________ |
| A6 | 屏幕分辨率 | ____________ |
| A7 | 网络环境（WiFi / 有线 / 4G / 5G） | ____________ |
| A8 | 是否在防火墙或公司网络内 | [ ] 是 [ ] 否 |

### B. 游戏经验

| # | 问题 | 填写 |
|---|------|------|
| B1 | 你平时玩游戏吗？ | [ ] 经常（每周 3+ 次） [ ] 偶尔（每月几次） [ ] 很少 [ ] 不玩 |
| B2 | 你玩过社交推理类游戏吗？（如 Among Us / 狼人杀 / Mafia） | [ ] 经常玩 [ ] 玩过几次 [ ] 听说过但没玩过 [ ] 完全不了解 |
| B3 | 你玩过 Among Us 吗？ | [ ] 经常玩 [ ] 玩过 [ ] 没玩过 |
| B4 | 你玩过线上多人游戏吗？ | [ ] 经常 [ ] 偶尔 [ ] 很少 |
| B5 | 你对像素风格游戏的接受度？ | [ ] 非常喜欢 [ ] 可以接受 [ ] 不太喜欢 [ ] 无所谓 |
| B6 | 你更偏好中文还是英文界面？ | [ ] 中文 [ ] 英文 [ ] 都行 |

### C. 第一印象（启动前）

| # | 问题 | 填写 |
|---|------|------|
| C1 | 从收到 zip 到解压完成，花了多久？ | ____________ 分钟 |
| C2 | 解压过程有问题吗？ | [ ] 没有问题 [ ] 有问题：____________ |
| C3 | 打开 app 时 macOS 有安全提示吗？ | [ ] 没有提示 [ ] 有提示但成功打开 [ ] 卡住了 |
| C4 | 如果 macOS 阻止打开，你卡在哪一步？ | [ ] 没卡住 [ ] 安全提示 [ ] 找不到"仍要打开" [ ] 其他：____________ |

## 3.2 测试中观察清单

> 按游戏阶段列出观察项目。测试者按顺序执行，任一步失败就停下记录。

### 阶段 1: 启动与主菜单 (预计 2 分钟)

| # | 操作 | 预期 | 通过 | 备注 |
|---|------|------|------|------|
| 1.1 | 启动 app | 主菜单正常显示，标题"港区潜线"带发光动画 | [ ] | |
| 1.2 | 观察主菜单布局 | 左侧离线面板（身份选择）、右侧联机面板（登录入口）、底部版本号 | [ ] | |
| 1.3 | 第一印象评分 | 1-5 分：哪里像"正经游戏"？哪里像"半成品"？ | ____分 | |

### 阶段 2: 登录 (预计 2 分钟)

| # | 操作 | 预期 | 通过 | 备注 |
|---|------|------|------|------|
| 2.1 | 找到"匿名登录"按钮 | 在联机面板右上角 | [ ] | |
| 2.2 | 点击匿名登录 | 等待几秒后状态变为"匿名账号已就绪" | [ ] | |
| 2.3 | 等待时间 | ____秒 | | |

### 阶段 3: 设置 (预计 3 分钟)

| # | 操作 | 预期 | 通过 | 备注 |
|---|------|------|------|------|
| 3.1 | 找到"打开设置"按钮 | 主菜单可见 | [ ] | |
| 3.2 | 点击打开设置 | 半透明覆盖层弹出，标题"设置中心" | [ ] | |
| 3.3 | 5 个滑块 + 8 个按钮 | 全部可见 | [ ] | |
| 3.4 | 改一下音量 | 音量立即变化 | [ ] | |
| 3.5 | 改窗口模式 | 窗口状态改变 | [ ] | |
| 3.6 | 点"重置设置" | 恢复默认 | [ ] | |
| 3.7 | 关闭设置 | 覆盖层消失，主菜单恢复 | [ ] | |

### 阶段 4: 联机大厅 (预计 5 分钟)

| # | 操作 | 预期 | 通过 | 备注 |
|---|------|------|------|------|
| 4.1 | 从主菜单进入大厅 | "联机大厅"标题，网络状态栏正常 | [ ] | |
| 4.2 | **Host**: 创建房间 | 3-10 秒内生成 6 位房间码 | [ ] | |
| 4.3 | **Host**: 截图房间码 | 房间码、状态栏、玩家列表均可见 | [ ] | |
| 4.4 | **Client**: 输入房间码加入 | 状态变为已加入，玩家列表出现 2 人 | [ ] | |
| 4.5 | **Client**: 截图玩家列表 | Host/Client 均可见 | [ ] | |
| 4.6 | 两端点击"准备" | 状态同步，显示"[就绪]" | [ ] | |
| 4.7 | **Host**: 点击"开始游戏" | 两端进入对局 | [ ] | |

### 阶段 5: 对局核心闭环 (预计 10-15 分钟)

| # | 操作 | 预期 | 通过 | 备注 |
|---|------|------|------|------|
| 5.1 | 两端分别 WASD 移动 10 秒 | 位置同步，无明显卡死 | [ ] | |
| 5.2 | 移动到任务站，按 E 交互 | 任务进度变化，另一端不报错 | [ ] | |
| 5.3 | 按 Enter 打开聊天框 | 聊天输入框弹出 | [ ] | |
| 5.4 | 输入消息并按 Enter 发送 | 消息出现在聊天区，带频道标签 | [ ] | |
| 5.5 | 5 秒内再次发言 | 出现"发言冷却中"提示 | [ ] | |
| 5.6 | 按 Tab 切换频道 | 频道在"近距离聊天"和"全局频道"间切换 | [ ] | |
| 5.7 | 触发会议（按 R 举报或按紧急铃） | 两端进入会议阶段 | [ ] | |
| 5.8 | 会议中发送聊天 | 频道自动切换为"会议聊天" | [ ] | |
| 5.9 | 投票或跳过 | 投票阶段正常结束，返回行动阶段 | [ ] | |
| 5.10 | **Client**: 退出游戏 | Host 不崩溃，玩家离开状态可解释 | [ ] | |
| 5.11 | **Host**: 退出游戏 | Client 显示"Host 已断开"，旧房间码失效提示，可点"离开房间"返回 | [ ] | |

### 阶段 6: 安全功能 (预计 3 分钟)

| # | 操作 | 预期 | 通过 | 备注 |
|---|------|------|------|------|
| 6.1 | 无消息时观察"举报最近"按钮 | 灰色不可点击 | [ ] | |
| 6.2 | 发送一条消息后 | 按钮变亮可点击 | [ ] | |
| 6.3 | 点击"举报最近" | 有反馈提示 | [ ] | |
| 6.4 | 点击"屏蔽最近" | 该发送者后续消息不再显示 | [ ] | |

## 3.3 测试后反馈问卷

### D. 维度评分 (1-5 分)

> 1 = 非常差, 2 = 较差, 3 = 一般, 4 = 较好, 5 = 非常好

| # | 维度 | 评分 | 补充说明 |
|---|------|------|---------|
| D1 | **上手难度** -- 从零到能玩，花了多久？操作指引是否清楚？ | ____分 | |
| D2 | **规则理解** -- 四方势力的目标和差异是否清楚？胜负条件是否明白？ | ____分 | |
| D3 | **操作手感** -- WASD 移动是否流畅？按键响应是否及时？ | ____分 | |
| D4 | **视觉清晰度** -- 地图上能否区分不同区域？角色能否辨识？UI 是否看得清？ | ____分 | |
| D5 | **音效体验** -- BGM 是否合适？操作反馈音效是否足够？ | ____分 | |
| D6 | **社交体验** -- 聊天功能是否好用？频道切换是否直观？冷却时间是否合理？ | ____分 | |
| D7 | **重复游玩意愿** -- 你还想再玩一局吗？ | ____分 | |
| D8 | **整体印象** -- 作为一个整体产品，你的感受？ | ____分 | |
| D9 | **推荐意愿** -- 你会把这个游戏推荐给朋友吗？ (1=绝不可能, 5=马上下载) | ____分 | |

### E. 开放问题

```
E1. 最让你困惑的是什么？
___________________________________________________________
___________________________________________________________

E2. 最喜欢的一点是什么？
___________________________________________________________
___________________________________________________________

E3. 最不喜欢的一点是什么？
___________________________________________________________
___________________________________________________________

E4. 如果只能改一个东西，你改什么？
___________________________________________________________
___________________________________________________________

E5. 你觉得这个游戏和 Among Us / 狼人杀 比，最大的不同是什么？
___________________________________________________________
___________________________________________________________

E6. 你愿意花多少钱买这个游戏？
    [ ] 免费才玩 [ ] 10-20 元 [ ] 20-40 元 [ ] 40-60 元 [ ] 60+ 元

E7. 有没有卡住/闪退/点不动的情况？（描述当时在做什么）
___________________________________________________________
___________________________________________________________

E8. 其他想说的？
___________________________________________________________
___________________________________________________________
```

### F. Bug 报告模板

> 每个 Bug 单独填写一份。可以复制多份。

```
Bug ID (自编号): ____
时间: ____________
机器/系统: ____________
角色: Host / Client
所在游戏阶段: 主菜单 / 登录 / 大厅 / 行动 / 会议 / 结算
步骤编号 (对应观察清单): ____________

复现步骤:
  1. ____________
  2. ____________
  3. ____________

预期行为:
  ____________

实际行为:
  ____________

房间码是否已生成: [ ] 是 [ ] 否
是否已加入房间: [ ] 是 [ ] 否
是否有红色报错 (Console): [ ] 有 [ ] 无
  如有，报错内容: ____________

截图/录屏: [ ] 有 [ ] 无
  如有，文件名: ____________

能否稳定复现: [ ] 每次都能 [ ] 偶尔 [ ] 只出现一次
严重程度: [ ] 崩溃/卡死 [ ] 功能不可用 [ ] 体验差但能用 [ ] 美观问题

补充:
  ____________
```

## 3.4 测试环境搭建指南

### 获取构建包

```
文件: GanglandUndercover-FriendTest-macOS-20260610.zip
大小: 84 MB
sha256: a3ec614cee330acce902b3286430c8ba0b80f00a794347cf2e71289b34207806
```

获取方式（按优先级）：

1. **直接传输**: Host 通过网盘/AirDrop/微信文件发送 zip 给测试者
2. **网盘链接**: （待补充具体链接）
3. **Unity Editor 回退**: 如果 app 无法打开，测试者需要安装 Unity Hub + Unity 6000.4.9f1，打开项目运行 PlayMode（不推荐，仅作为备选）

### 安装步骤

```
1. 下载 zip 文件
2. 双击解压 (macOS 自动解压)
3. 将 GanglandUndercover.app 拖入"应用程序"文件夹（或直接放在桌面）
4. 首次打开:
   - macOS 可能弹出安全提示"无法验证开发者"
   - 右键点击 app > "打开" > 选择"仍要打开"
   - 或前往"系统设置 > 隐私与安全性"，点击"仍要打开"
5. 如果系统弹出网络访问提示，选择"允许"
```

### 连接步骤

```
Host 端:
  1. 启动 app > 进入主菜单
  2. 点击"匿名登录"，等待"匿名账号已就绪"
  3. 点击"进入大厅"
  4. 点击"创建房间"
  5. 等待 6 位房间码出现（3-10 秒）
  6. 把房间码发给所有测试者
  7. 等待大家加入并 Ready 后，点击"开始游戏"

Client 端:
  1. 启动同一个 app > 进入主菜单
  2. 点击"匿名登录"
  3. 点击"进入大厅"
  4. 在房间码输入框输入 Host 发来的 6 位大写字母数字
  5. 点击"加入房间"
  6. 确认玩家列表中出现 Host 和自己的名字
  7. 点击"准备"
  8. 等待 Host 开始游戏
```

### 常见问题

| 问题 | 原因 | 解决方法 |
|------|------|---------|
| macOS 提示"无法验证开发者" | 构建包未签名 | 右键 app > 打开 > "仍要打开" |
| macOS 提示"应用已损坏" | Gatekeeper 限制 | 系统设置 > 隐私与安全性 > "仍要打开" |
| 匿名登录超过 30 秒无反应 | 网络问题 | 检查网络连接，确认可以访问 Unity 云服务 |
| 创建房间超过 20 秒无反应 | Relay 服务延迟 | 截图状态栏和 Console，返回主菜单重试 |
| 输入房间码后超过 20 秒无反应 | 房间可能已过期 | 让 Host 重新创建房间，使用新的房间码 |
| Host 退出后房间码失效 | 设计行为（无 Host 迁移） | Host 需要重新创建房间，Client 点击"离开房间"返回主菜单 |
| 对局中闪退 | 未知 Bug | 填写 Bug 报告模板，截图 Console 报错 |
| 聊天消息发不出去 | 发言冷却中 | 等待冷却结束（约 5 秒） |
| 看不到其他人的聊天 | 频道不匹配 | 确认在同一频道（近距离/全局/会议） |

## 3.5 已知问题速查表

> 从 `output/KNOWN_ISSUES.md` (v0.2.0-dev, 2026-06-09) 提取的玩家可能遇到的问题。

### 已清零

- P0 发布阻断级问题已全部修复并验证

### 已知问题

| 优先级 | 问题 | 玩家感知 | 临时应对 |
|--------|------|---------|---------|
| **P1-3** | 地图美术资产化仍需继续扩展 | 部分区域的视觉细节（角色动画帧、会议/任务 UI、战术地图）仍为程序化兜底，不够精细 | 不影响功能，请忽略视觉上的临时占位 |
| **P2-2** | Bot 不使用暗线通道 | AI 玩家不会走通风管/暗道，行为可能显得不够聪明 | 本轮测试重点是联机功能，AI 行为将在后续优化 |
| **P2-3** | 恶意 Client 防护测试不足 | 极端情况下（如使用修改版客户端），可能出现异常行为 | 正常游玩不受影响 |
| **P3-1** | OnGUI 遗留代码 | 极少数情况下可能出现旧式 UI 残留 | 不影响核心功能 |
| **P3-2** | Host 迁移未实现 | **Host 退出后 Client 会断开连接**，旧房间码失效 | Host 退出后所有人返回主菜单，Host 重新开房 |
| **P3-3** | 角色自定义转发需双端确认 | 极少数情况下角色外观同步可能异常 | 不影响游戏核心玩法 |

### 重要提示

- **Host 退出 = 房间解散**: 当前版本没有 Host 迁移功能。如果 Host 退出或断线，所有 Client 会收到"Host 已断开"提示，旧房间码失效。需要 Host 重新创建房间。
- **发言冷却**: 聊天有冷却机制（约 5 秒），这是设计行为，不是 Bug。
- **构建包未签名**: macOS 首次打开需要在安全设置中允许。
- **AI 补位**: 不足 5 人时会自动补 AI，AI 行为可能不够智能。

---

## 附录: 测试反馈汇总表模板

> 测试组织者用。汇总所有测试者反馈后填写。

| 测试者 | 系统 | 启动 | 登录 | 设置 | Relay | 开局 | 聊天 | 举报/屏蔽 | 第一印象(1-5) | 推荐意愿(1-5) |
|--------|------|------|------|------|-------|------|------|----------|--------------|--------------|
| P1 | | | | | | | | | | |
| P2 | | | | | | | | | | |
| P3 | | | | | | | | | | |
| P4 | | | | | | | | | | |

> 通过 = ✓, 失败 = ✗, 未测 = --

### Triage 分类参考

| 等级 | 定义 | 行动 |
|------|------|------|
| Blocker | 游戏崩溃、无法进入、核心功能完全不可用 | 必须修，否则无法继续测试 |
| Critical | 功能能用但体验极差，或多数测试者卡在同一处 | 不能带到下一轮 |
| Major | 明显问题但测试者能找到绕过方式 | 应该修，但不阻塞测试 |
| Minor | 文案/对齐/美观类，不影响功能 | 锦上添花 |
| Idea | 测试者提出的改进想法，不是 Bug | 评估后决定是否采纳 |

### 通过门槛

- 0 Blocker
- < 3 Critical
- 第一印象均分 >= 3
- Relay 房间创建 + 加入 + 开局 + 聊天至少各 1 次通过

---

*文档结束。基于项目 output/ 目录中以下文档整合生成:*
- *steam_screenshot_checklist_20260610.md*
- *steam_store_copy_draft_20260610.md*
- *screenshot_plan_20260609.md*
- *friend_remote_test_runbook_20260610.md*
- *playtest_feedback_form_20260610.md*
- *playtest_triage_template_20260610.md*
- *install_launch_screenshot_guide_20260610.md*
- *full_qa_checklist_20260609.md*
- *qa_runbook_20260609.md*
- *KNOWN_ISSUES.md*
- *art_bible_v1_20260605.md*
- *steam_pc_art_ui_visual_optimization_20260610.md*
- *remote_test_closure_20260610.md*
