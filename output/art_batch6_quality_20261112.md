# 美术批次 6：画质攻坚（2026-11-12）

> 项目恢复日（10-31 挂起后首次推进）。用户指令：全面推进，特别是画面画质。
> 起点：GitHub `fix/hud-readability`（用户 11-10 网页改的 HUD 可读性）已 fast-forward 合入本地 main。

## 触发依据：视觉审查（action-view-players.png 基线，AI 视觉分析）

三大痛点（按严重度）：
1. **像素素材糊化**：LimeZu 纹理已是 Point 过滤（Sprite2DAssetCache.cs:391/526），
   根因是**非整数倍缩放**——相机 ActionSize 3.0 + PPU 48 在 1080p 下 texel/px=3.75，
   像素行宽窄不均 → 发糊 + 移动闪烁。
2. **整体发闷**：中灰区间挤成一团（地面/地毯/桌面同灰蓝），无白场锚点，饱和度极低。
3. **落地感缺失**：角色脚下虽有 B1 阴影，但 0.32×0.14 被 0.28 宽基座完全盖住，
   截图不可见 → 角色悬浮感。

## 本批落地（三刀）

### 6a 像素锐利 —— 相机整数 texel 吸附
- `OnlineCameraRig.SnapOrthoSizeToTexelGrid(target, screenH, PPU)`：吸附到最近整数 texel/像素档
  （1080p：3.0→2.8125 即 k=4；900p→3.125 即 k=3；1440p 恰好 3.0=1440/(2×48×5)）
- 近景档（≤CloseupSnapMaxSize=6，Action/Task/Blackout）吸附；预览/战术图不吸附（保取景）
- 相机**位置**也吸附到整像素网格（非 preview 模式），消除跟随移动的亚像素抖动
- 视野影响：1080p 竖向视野 6.0→5.625（−6.25%），判定可接受（击杀/报案范围 1.0-1.5 单位）

### 6b 角色落地 —— 加强既有接触阴影
- `OnlineWorldBuilder.CreatePlayerVisual` 的 B1"阴影"子物体：0.32×0.14/α0.35 → **0.46×0.19/α0.40**，y −0.12→−0.13
- 死亡/鬼魂态沿用整根 visual 的既有处理（死亡隐藏/鬼魂 α0.42），零新增状态逻辑
- 曾在 `Create2DCharacterView` 加第二枚阴影 → 发现与 builder 路径重复，**已撤销**（生产根 = builder root + adapter 子层，阴影属 root）

### 6c 全屏调色 —— ScreenGrade（首个 shader）
- 新 `Assets/_Project/Shaders/ScreenGrade.shader`：暗角(vig 0.30/start 0.45) + 轻对比(0.10) + 轻饱和(0.14) + 微暖调(0.045)
- 新 `ScreenGradeController`：内置管线 **CommandBuffer**（AfterEverything，CameraTarget↔TempRT blit 一个来回），
  自动绑 Camera.main、相机切换重绑、shader 缺失静默降级；Awake 经 `EnsureScreenGrade()` 挂载（紧随 EnsureVFX）
- HUD（Overlay Canvas）/OnGUI 不受影响；Sabotage 全屏遮罩会被调色（预期内）
- 截图工具两入口都接了 `BindNow`（EditMode 无玩家循环，需显式绑定）+ action view 尺寸吸附

## 环境/管线事实（本批普查）
- 内置渲染管线（无 URP），**Gamma 色彩空间**（m_ActiveColorSpace: 0），默认质量档 Medium（MSAA 0）
- 线性空间切换 = 高风险（所有 pass 调参均按 gamma 校准）→ 本批不动，记录为远期项
- MSAA 对 alpha-blend 2D sprite 边缘收益≈0 → 不碰（避免质量剧场）

### 6d 在线局 HUD 文字描边（移植 fix/hud-readability）
- 用户 11-10 的 GitHub 分支只改了原型 HUD（SocialPrototypeHud.CreateText）；
  在线局 HUD（OnlineMatchHud.ConfigureText，3565 行，全部文字的统一出口）同样缺描边
- 同款处理移植：黑色 75% Outline / (1,-1)，一处覆盖全部在线 HUD 文字；
  UIStyle.StylizeText 只换字体不动 Outline 组件，安全

## 门禁与证据（2026-11-12 实测）

**门禁**：
- EditMode **264/264**（254 基线 + 新增 ScreenGradeAndPixelCrispTests 10 例）× 多轮
- PlayMode **24 / 0 failed / 15 skipped**（与 10-26 基线一致）
- XML: `ci-logs/batch6_*_editmode.xml` / `batch6_*_playmode.xml`

**signalstats 量化（before → after，1920×1080）**：
| 图 | YLOW(5%) | YAVG | YHIGH(95%) | YMAX |
|---|---|---|---|---|
| action-view | 20 → 7 | 58.5 → 48.4 | 104 → 91 | 248 → 252 |
| harbour 整图 | 22 → 12 | 32.6 → 22.2 | 44 → 35 | 251 → 255 |

解读：YAVG 下降=暗角+对比曲线起效（夜港氛围取向，非"变闷"——YLOW/YHIGH 两端拉开、YMAX 微升=高光保留）；灰阶两端展开即"发闷"（中灰挤一团）的对症指标。

**AI 视觉复检（改后 action view）**：
- ✓ 暗角合格不过度，关键信息（角色/任务标签）可读
- ✓ 气围"一体化电影感，比平板生硬的未处理像素风好"
- ✓ 角色接地阴影可见（红/紫/橙下确认；绿/蓝下的在暗地板上对比弱）
- ✗ 残留：中间调仍可再拉开（已把 contrast 0.10→0.13 微调）；全局糊感未根除

**残留糊感的根源分析（后续项，不在本批）**：
相机档位已整数对齐（1080p 下 3 texel/px），但**精灵自身的非整数局部缩放**（如角色帧 48px × 1.067 × root 1.12 ≈ 1.2 倍非整数）与**程序化软图层**（Bilinear 的光洗/微装饰/SoftCircle）天然无像素网格——像素锐利与软涂层的混搭是风格层面的取舍，需逐精灵缩放档位化才能根除（候选 6e）。

A/B 证据图：`output/art_batch6_evidence/`（before/ 对照组 + after- 改后）。

## 遗留/后续候选
- 霓虹光晕（additive halo）与暖光池补量——6c 调色后再评估是否还缺
- 线性色彩空间远期项（需全 pass 重校）
- 全屏调色参数（对比/饱和/暖调）试玩反馈后可热调（controller 公开字段）
