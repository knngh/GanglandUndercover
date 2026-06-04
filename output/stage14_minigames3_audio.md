---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_d96b7d305f0811f1bd025254006c9bbf
    ReservedCode1: F6sEnIRpOZdonfdhBTGC53+bamZtZs8dGIjaL9DQKRGWw6gIkB30KbdkETJGQFbOrcDsj2E//aCpKdw52/HJL0pGvj9P8sgm5e7K5Mjr8NrBrrGiXrjOnF+pzNKKpWxqyTUMwKHFQ44BVzq+5lbiV9EWbZWLcsLCyM333yFTzGpCYT+At2XNVPOGdOM=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_d96b7d305f0811f1bd025254006c9bbf
    ReservedCode2: F6sEnIRpOZdonfdhBTGC53+bamZtZs8dGIjaL9DQKRGWw6gIkB30KbdkETJGQFbOrcDsj2E//aCpKdw52/HJL0pGvj9P8sgm5e7K5Mjr8NrBrrGiXrjOnF+pzNKKpWxqyTUMwKHFQ44BVzq+5lbiV9EWbZWLcsLCyM333yFTzGpCYT+At2XNVPOGdOM=
---

# Stage 14: 小游戏补全 + 音效增强 — 完成报告

**日期**: 2026-06-03
**项目路径**: `/Users/zhugehao/projects/GanglandUndercover`

---

## 一、小游戏补全（3 种新增）

### 1.1 CalibrateTask.cs — 航向校准
- **路径**: `Assets/_Project/Scripts/SocialDeduction/MiniGames/CalibrateTask.cs` (276 行)
- **玩法**:
  - 十字准星从屏幕随机偏离位置自动向中心移动
  - 准星进入中心目标区域时玩家点击确认
  - 3 轮递增难度（速度 0.6 / 0.95 / 1.3）
  - 过早点击触发红色闪屏惩罚，重新开始当前轮
  - 通过时绿色闪屏反馈
- **UI**: 深色背景 + 红色十字线准星 + 脉冲目标环

### 1.2 AsteroidTask.cs — 清理陨石
- **路径**: `Assets/_Project/Scripts/SocialDeduction/MiniGames/AsteroidTask.cs` (331 行)
- **玩法**:
  - 屏幕随机位置生成 5 个陨石（带碎石纹理子对象模拟不规则外观）
  - 点击陨石触发碎片爆炸粒子效果 + 本体缩小消失
  - 限时 8 秒，最后 2 秒计时器变红
  - 全部击碎则成功，超时则失败
- **UI**: 深蓝太空背景 + 陨石持续自转动画

### 1.3 DownloadTask.cs — 下载数据
- **路径**: `Assets/_Project/Scripts/SocialDeduction/MiniGames/DownloadTask.cs` (326 行)
- **玩法**:
  - 进度条自动从 0% 增长到 100%（速度 16%/s）
  - 随机触发"信号干扰"（最多 4 次），进度暂停
  - 干扰时需连续点击修复（进度阈值 0.22/次，红色→绿色渐变反馈）
  - 干扰超时未修复则下载失败
- **UI**: 主进度条（蓝色）+ 干扰修复条（红→绿渐变）+ 状态文字

---

## 二、枚举与路由更新

### 2.1 MiniGameType.cs
增加 3 个新枚举值: `CalibrateTask`, `AsteroidTask`, `DownloadTask`
总枚举数: 7 → 10

### 2.2 SocialPrototypeController.PickMiniGameType()
- **新增关键词映射**:
  | 关键词 | 类型 |
  |--------|------|
  | 航向 / 校准 / 校准仪 | CalibrateTask |
  | 陨石 / 太空 / 碎片 | AsteroidTask |
  | 下载 / 上传 / 数据 | DownloadTask |
- **随机回退池**: `% 7` → `% 10`，switch 增加 case 7/8/9

---

## 三、音效增强

### 3.1 AudioManager.cs 修改清单
- **路径**: `Assets/_Project/Scripts/Audio/AudioManager.cs` (347 行，+138 行)

#### 新增 SoundEffect:
| 枚举值 | Inspector 字段 | 用途 |
|--------|---------------|------|
| `Report` | `reportClip` | 报告尸体（独立于 BodyReport） |
| `VentOpen` | `ventOpenClip` | 通风管打开 |
| `VentClose` | `ventCloseClip` | 通风管关闭 |
| `ButtonHover` | `buttonHoverClip` | 按钮悬停 |

#### 新增 MusicTrack 枚举 + BGM 系统:
| 枚举值 | Inspector 字段 | 用途 |
|--------|---------------|------|
| `MainMenu` | `mainMenuBGM` | 主菜单背景音乐 |
| `InGame` | `inGameBGM` | 游戏内背景音乐 |
| `Meeting` | `meetingBGM` | 会议阶段背景音乐 |

#### 新增 API:
- `PlayBGM(MusicTrack track)` — 播放 BGM，同名跳过，否则 crossfade
- `StopBGM()` — 淡出停止
- `PauseBGM()` / `ResumeBGM()` — 暂停/恢复
- `CrossfadeMusic(clip)` — 0.6s 淡出 + 0.6s 淡入
- `FadeOutMusic()` — 1.2s 渐弱

#### 已有功能确认：
- 三通道独立音量控制: `MasterVolume` / `SFXVolume` / `MusicVolume`（已存在，无需修改）
- `ApplyVolumes()` 公式: `sfxSource.volume = master * sfx`, `musicSource.volume = master * music`

---

## 四、文件变更汇总

| 文件 | 操作 | 行数 |
|------|------|------|
| `MiniGames/CalibrateTask.cs` | **新建** | 276 |
| `MiniGames/AsteroidTask.cs` | **新建** | 331 |
| `MiniGames/DownloadTask.cs` | **新建** | 326 |
| `MiniGames/MiniGameType.cs` | 修改 | +3 枚举值 |
| `SocialPrototypeController.cs` | 修改 | PickMiniGameType 新增映射 + 随机池 7→10 |
| `Audio/AudioManager.cs` | 修改 | +4 SFX + BGM 系统 + 交叉淡入淡出 |

**小游戏总数**: 7 → 10（Wire / Memory / SwipeCard / Keypad / Sort / Scan / Tap / Calibrate / Asteroid / Download）
*（内容由AI生成，仅供参考）*
