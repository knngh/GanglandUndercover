# D-3 身份简报与视觉走查记录

> 日期：2026-08-25
> 项目：`/Users/zhugehao/projects/GanglandUndercover`

## 自动化图形验证证据

图形 PlayMode 用例 `DemoScreenshotPlayTests.DemoBaseline_CapturesAllKeyPhases` 已在真实
PlayMode 生命周期下通过，使用主摄像机渲染并写入 1920×1080 PNG。流程覆盖：

`Lobby -> Opening（身份简报） -> Action -> Meeting -> Voting -> Result`

最新证据：`ci-logs/20260825_d3_graphics_screenshot.xml`，用例 `1/1 PASS`；运行日志中的六条
`[DemoShots]` 记录确认每张图片均已写入。生成文件：

- `Screenshots/DemoBaseline/01_lobby_143913842.png`
- `Screenshots/DemoBaseline/02_opening_briefing_143913986.png`
- `Screenshots/DemoBaseline/03_action_hud_143914073.png`
- `Screenshots/DemoBaseline/04_meeting_143914181.png`
- `Screenshots/DemoBaseline/05_voting_143914289.png`
- `Screenshots/DemoBaseline/06_result_143914371.png`

测试同时断言 PNG 签名、分辨率、非空文件和可见像素范围；`file` 检查确认六张均为
`1920 x 1080` RGBA PNG。

## 当前人工走查状态

尚未完成两台真人窗口的手工确认：

- 双人 Relay 窗口中的远端摄像头画面可读性；
- 真人视角下身份简报 5 分钟端到端录制；
- 四个职业能力反馈的实际屏幕/音频感知。

这些项目已有自动化门禁或单元行为证据，但本记录不把自动化截图冒充人工视角，待有图形
窗口和真人操作时补录。

## 本轮图形收口

- LimeZu `*-16` atlas 现在裁剪为单个 16×16 cell，避免整图或多格区域被拉伸到世界对象。
- 程序化矩形、圆形、胶囊和菱形改回中性运行时 Sprite；LimeZu 仍用于明确的地板、墙体和房间道具，避免所有几何覆盖层出现墙体条纹。
- `CoreSystemTests.Sprite2DAssetCache_CropsLimeZu16AtlasesToOneCell` 和 `WorldBuilder_SortsPositiveZInFrontOfNegativeZ` 已通过，锁定 atlas rect/PPU 与正 Z 前景排序契约。

## 结论与剩余人工证据

自动化六阶段图形门禁已完成，覆盖身份简报可见性、Action HUD、会议、投票和结算渲染；
PNG 均为 1920×1080 RGBA 且通过非空像素断言。以下真人走查仍保持 `DEFERRED`，需要真实
图形窗口和真人输入，不能由 PlayMode 截图替代：

- 双人 Relay 窗口中的远端摄像头画面可读性；
- 身份简报 5 分钟端到端录像；
- `FootprintTrack`、`CorpseExamine`、`RemoteSurveillance`、`DarkVision` 四职业能力的屏幕/音频反馈。
