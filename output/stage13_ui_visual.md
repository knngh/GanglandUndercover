---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_14bde6105e5e11f1a4f35254002afed2
    ReservedCode1: bi0DL94TuIfqileq8FuCI1k7TSqcF86ExXalWPu9aDBfGVxPe5EaPFyAKEBo8wTB9v4Z6aUm984rBNXeSXDUzle8ia8201Onwa9bNa7yWS4JYMzMec6wZPFI7EVxE6iqyoo8gh2Z06OsQt3OnU6uB2mlE38BphEpXiOlv4GAPfOgpFCBE/bw0m183ao=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_14bde6105e5e11f1a4f35254002afed2
    ReservedCode2: bi0DL94TuIfqileq8FuCI1k7TSqcF86ExXalWPu9aDBfGVxPe5EaPFyAKEBo8wTB9v4Z6aUm984rBNXeSXDUzle8ia8201Onwa9bNa7yWS4JYMzMec6wZPFI7EVxE6iqyoo8gh2Z06OsQt3OnU6uB2mlE38BphEpXiOlv4GAPfOgpFCBE/bw0m183ao=
---

# Stage 13 — UI 深度美化（Among Us 太空主题）

> 完成日期：2026-06-02

---

## 产出清单

| 文件 | 路径 | 行数 | 说明 |
|------|------|------|------|
| ThemeManager.cs | `/Users/zhugehao/projects/GanglandUndercover/Assets/_Project/Scripts/UI/ThemeManager.cs` | 167 | 完整视觉体系（配色/字体/圆角/间距/动画常量） |
| UIParticleEffect.cs | `/Users/zhugehao/projects/GanglandUndercover/Assets/_Project/Scripts/UI/UIParticleEffect.cs` | ~200 | 代码生成星空粒子，Canvas 自适应分辨率 |
| MainMenuController.cs | `/Users/zhugehao/projects/GanglandUndercover/Assets/_Project/Scripts/UI/MainMenuController.cs` | ~600 | Among Us 风格主菜单，角色卡片+粒子星空 |
| GameOverController.cs | `/Users/zhugehao/projects/GanglandUndercover/Assets/_Project/Scripts/UI/GameOverController.cs` | ~700 | 结算动画面板，逐帧翻转揭示+进度条 |
| PrototypeHud.cs | `/Users/zhugehao/projects/GanglandUndercover/Assets/_Project/Scripts/UI/PrototypeHud.cs` | 480 | 游戏内 HUD，头像框+进度条+任务卡片+行动按钮 |

---

## 一、ThemeManager.cs — 完整视觉体系

### 配色方案
```
主背景       #0a0a1a  深空蓝黑      → BackgroundDark
面板背景     #1a1a3e  半透明深蓝    → PanelBackground
按钮主色     #3a7bd5  霓虹蓝        → ButtonPrimary
按钮悬停     #5a9bf5  亮霓虹蓝      → ButtonHover
按钮按下     #1a4b8a  暗霓虹蓝      → ButtonPressed
危险色       #ff4444  猩红          → DangerRed
安全色       #44ff44  翠绿          → SafeGreen
警告色       #ffaa00  琥珀          → WarningAmber
Mole 色      #00cccc  青色          → MoleTeal
霓虹青       #1aeeff  边框/描边     → NeonCyan
标题金       #f0e68c  尊贵金        → TitleGold
文字主色     #f5f2eb  暖白          → TextPrimary
文字次要     #7a7a8a                 → TextMuted
卡片背景     #12122e                 → CardBackground
输入框背景   #0d0d24                 → InputBackground
分隔线       #2a2a4e                 → Divider
遮罩         rgba(5,5,15,0.85)       → OverlayDark
```

### 阵营色映射
- `Faction.Gang` → `GangRed` (#ff4444)
- `Faction.Undercover` → `UndercoverBlue` (#3a7bd5)
- `Faction.Police` → `PoliceGray` (#8a8a9a)
- `Faction.Mole` → `MoleTeal` (#00cccc)

提供 `GetFactionColor()` / `GetRoleColor()` 静态方法。

### 字体大小体系
```
标题     FontSizeTitle    28
副标题   FontSizeSubtitle 22
头部     FontSizeHeader    20
正文     FontSizeBody      18
按钮     FontSizeButton    18
小字     FontSizeSmall     14
页脚     FontSizeFooter    12
```

### 圆角常量
```
面板  CornerRadiusPanel  12px
按钮  CornerRadiusButton 8px
卡片  CornerRadiusCard   6px
```

### 工具方法
- `WithAlpha(Color, float)` — 修改透明度
- `ScaleColor(Color, float)` — RGB 通道缩放
- `Hex(string)` — 十六进制字符串转 Color

---

## 二、UIParticleEffect.cs — 星空粒子

- 代码生成圆点模拟星空，无需贴图资源
- 支持配置粒子数量、大小范围、漂移速度、闪烁频率
- 边界循环：漂移超出屏幕后自动从对侧回环
- Canvas 自适应分辨率
- 用于主菜单背景和结算转场

---

## 三、MainMenuController.cs — Among Us 风格主菜单

### 布局结构
```
全屏深空背景 (#0a0a1a)
  ├─ UIParticleEffect 星空粒子层
  ├─ 标题区（居中靠上）
  │   ├─ "港区潜线" 大标题 (Outline + Shadow 发光效果)
  │   └─ "HARBOR UNDERCOVER" 英文副标题
  ├─ 离线模式面板
  │   ├─ 警察卡片（阵营色边框+盾牌图标）
  │   ├─ 卧底卡片（阵营色边框+眼图标）
  │   ├─ 黑帮卡片（阵营色边框+骷髅图标）
  │   └─ 线人卡片（阵营色边框+问号图标）
  ├─ 联机模式面板
  │   ├─ 创建房间按钮
  │   └─ 加入房间按钮
  └─ 底部版本号
```

### 交互
- 角色卡片带 hover 高亮、pressed 缩放动效
- 选中角色后显示 Begin 按钮（霓虹边框发光）
- 联机面板输入框 + 创建/加入按钮
- 语言切换按钮

---

## 四、GameOverController.cs — 结算动画

### 动画序列
1. **全屏遮罩渐入**：CanvasGroup alpha 0→1，时长 FadeInDuration
2. **胜利标题弹入**：从上方 -200px ease-out cubic 弹入中心位置
3. **身份卡片翻转揭示**：4 张卡片依次 scaleX 0→1（间隔 0.4s）
   - 每张卡片含角色头像、身份标签、阵营色边框
4. **统计面板进度条动画**：各项数据条宽度从 0→目标值（BarFillDuration 1.2s）
   - 嫌疑值、证据、掩护度、区域控制等

### 数据兼容
- 同时兼容 `SocialPrototypeController.PlayerRecord` 结构和 `GameState` 数据
- 未绑定 controller 时显示占位文本

---

## 五、PrototypeHud.cs — 游戏内 HUD

### 布局
```
┌────────────────────────────────────────────────────────────┐
│ [头像框] 卧底    你的回合     第 3 天  证据 ▓▓▓▓░░ 6/10 │
│ [阵营色边框/字母] 黑帮阵营          情报 ▓░░░░░ 2/10 │
├────┬─────────────────────────────────────┬─────────────────┤
│    │                                     │               │
│任务│          游戏主画面区域             │   案件板      │
│列表│          (3D 视图)                 │               │
│    │                                     │ · 阵营控制    │
│□□  收集证据 10/10                      │ · 警力热度    │
│=  降低嫌疑 100/100                     │ · 最近动态    │
│□  控制区域 3/5                         │               │
│□  线人情报 5/10                        │               │
│    │                                     │               │
├────┴─────────────────────────────────────┴───────────────┤
│ [通 风 管] [击  杀] [破  坏] [报  告] [会  议] [热 度 榜]│
└────────────────────────────────────────────────────────────┘
```

### 接口
```csharp
public void Bind(GameController controller)
```
- 订阅 `GameController.Changed` 事件自动刷新
- 兼容 `SocialPrototypeController.InitTurnHud()` 的调用链路

### 关键组件
- **左上角头像框**：圆形 Image mask 模拟，阵营色边框，内部字母标识
- **右上角进度条**：证据（蓝色）+ 情报（青色）双进度条，动态宽度
- **左侧任务列表**：卡片式布局，完成项显示 `=` 前缀 + 绿色划线
- **右侧案件板**：阵营控制 / 警力热度 / 货运进度 / 掩护度 / 最近日志
- **底部行动按钮**：统一霓虹风格，6 个按钮对应通风管/击杀/破坏/报告/会议/热度榜

---

## 依赖关系

```
ThemeManager (静态工具类)
    ├── UIParticleEffect (引用 BackgroundDark / WithAlpha)
    ├── MainMenuController (引用全部配色/字体/圆角)
    ├── GameOverController (引用动画时长/配色/阵营色)
    └── PrototypeHud (引用配色/字体/阵营色映射)

GameController (数据层)
    └── PrototypeHud.Bind(GameController)
```

---

## 未完成 / 注意事项

1. **圆角效果**：Unity uGUI 原生不支持 Image 圆角。ThemeManager 定义了常量但实际圆角需要通过 Shader 或 Mask 组件实现。当前各面板使用了硬边矩形，如需真正圆角后续可引入 RoundedImage 组件（自定义 Shader）。
2. **UIParticleEffect 脚本**：粒子在 Update 中逐帧更新位置，部分低端设备可能产生性能影响。建议调整粒子数量（默认 80 个）以适配目标平台。
3. **按钮交互回调**：PrototypeHud 的 6 个 Action 按钮目前仅创建了视觉样式，未连接点击回调。后续需要补全 `OnVent` / `OnKill` / `OnSabotage` 等方法并与 `GameController` 或 `SocialPrototypeController` 对接。
*（内容由AI生成，仅供参考）*
