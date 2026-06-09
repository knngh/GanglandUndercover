# Gangland Undercover — KNOWN_ISSUES.md

> **最后更新**: 2026-06-09 12:15 | **版本**: v0.2.0-dev

---

## 发布阻断 (P0) — 已清零

> ✅ 全部 P0 已修复并验证

---

## 高优先级 (P1) — 阶段1 剩余扩展

### P1-3: 地图美术资产化仍需继续扩展 🔁
- **已完成切片**: 关键地标可读性 + 任务事件反馈层已补齐；关键地标主体已替换为 Modern Exteriors 真实 sprite；每个任务点加入 LimeZu 破坏设备盖板和现场证物包；金融/电房/证物库/诊所/夜市/后巷已加入 Exteriors room prop sprite；监控室、指挥点、证物库、诊所和舰内房间已加入 Modern Office room prop sprite；海关、茶餐厅、夜市、金融、电房、天台、后巷、诊所、舰内各房间和舰桥继续加入 Modern Interiors 医疗/安防/生活类 room prop sprite；击杀现场和黑灯场景已加入命名 VFX 层，并纳入 EditMode + PrototypeSmoke 覆盖
- **剩余影响**: 角色动画帧、会议/任务/战术地图 UI、关键截图基线和更细粒度动画 VFX 仍需继续资产化扩展
- **下一步**: 刷新关键截图基线，继续替换会议/任务/战术地图 UI 与角色动画帧，并把剩余程序化兜底件迁移到精选 2D sprite

---

## 中优先级 (P2)

### P2-2: Bot 不使用暗线通道
- **修复**: OnlineBotController 添加 vent 寻路逻辑

### P2-3: 双端恶意 Client 覆盖不足 ⏳
- **影响**: EditMode 已覆盖 Task/Repair 越权拒绝、Chat/CharacterCustom payload 安全，但 Chat/Camera/CharacterCustom 仍缺真实双进程恶意消息注入测试
- **修复**: 增加双进程或测试 harness，伪造 Chat/Camera/CharacterCustom 入站消息并断言服务端拒绝或安全转发

---

## 低优先级 (P3)

### P3-1: OnGUI 遗留代码
### P3-2: 网络 Host 迁移测试不足
### P3-3: CharacterCustom 转发行为仍需双端实测确认

---

## 已修复 (v0.1.0 → v0.2.0)

- ✅ P0-1: BuildScript 场景路径修复 + macOS 构建 417MB
- ✅ P1-1: AudioManager Resources 自动加载 fallback
- ✅ 地图基准切片: 瓦片铺贴 + 墙壁边框 + 低饱和行动照明层
- ✅ EditMode 测试程序集引用修复: `GanglandUndercover.Tests.asmdef` 补 TestRunner/NUnit 引用
- ✅ P2-1: Resources 832MB → 104MB
- ✅ NGO deprecated warning: 全部清零
- ✅ Bot 卡住检测: 5秒位移<0.03m 自动重寻路
- ✅ 证据系统统一: 会议 UI 改用 MeetingEvidenceDossier
- ✅ UI 行动 HUD 基准: 低饱和警署行动风、CJK 字体优先、状态卡 / 命令条 / 身份卡三段式
- ✅ BGM 淡入淡出 + SFX 随机音调
- ✅ 尸体显示角色对应倒下 sprite
- ✅ MapSelect 改为广播给所有客户端
- ✅ Chat payload 改为结构化字段，内容包含 `|` 不再破坏解析
- ✅ Task/Repair 直接伪造完成会被服务器 active lock / range 校验拒绝
- ✅ 监控摄像头开始观看增加 Action/alive/技能或距离校验
- ✅ CharacterCustom 改为服务端校验 owner 后定向转发，payload 增加长度上限
- ✅ P1-2: 新玩家目标引导补齐；控制器暴露身份简报/目标/操作提示，Canvas HUD 接入，并由 EditMode + PrototypeSmoke 覆盖
- ✅ P1-1: 角色 2D Animator / walk 帧 PlayMode 验证完成；本地与远端移动输入会驱动方向指示和 walk frame 计数
- ✅ P1-3 切片A: 地图关键地标 + 任务事件反馈层补齐；12 个关键地标、每任务 4 个事件反馈标记，并由 EditMode + PrototypeSmoke 覆盖
- ✅ P1-3 切片B: Modern Exteriors 真实地标 PNG 精选导入；关键地标主体与任务事件反馈改用 LimeZu sprite，并由 EditMode + PrototypeSmoke + PlayMode 覆盖
- ✅ UI 按钮音效: 在线 HUD 动态按钮挂接 `UiButtonSfx`，使用 Kenney `SFX_ButtonHover.ogg` 悬停/选中反馈，点击继续使用 `SFX_UIClick.ogg`
- ✅ P1-3 切片C: Modern Exteriors room-props 精选导入；金融/电房/证物库/诊所/夜市/后巷加入真实房间道具 sprite，尸体击杀现场与黑灯场景加入命名 VFX 门槛
- ✅ P1-3 切片D: Modern Office room-props 精选导入；监控室、指挥点、证物库、诊所和舰内房间新增 16 个真实室内办公/医疗道具 sprite，room prop 门槛提升到 30
- ✅ P1-3 切片E: Modern Interiors room-props 精选导入；海关、茶餐厅、夜市、金融、电房、天台、后巷、诊所、舰内各房间和舰桥新增医疗/安防/生活类真实室内道具 sprite，room prop 门槛提升到 75
