# Gangland Undercover — Windows 测试步骤

> 版本: 2026-06-10 | 目标: 在 Windows 上验证构建可运行

---

## 环境准备

### 方式 A: 直接在 Windows 物理机上

```
1. 确保安装了 Visual C++ Redistributable (x64)
   → https://aka.ms/vs/17/release/vc_redist.x64.exe
2. 显卡驱动为最新版
3. Windows 10 64-bit 或更新
```

### 方式 B: macOS 上通过虚拟机（仅验证启动）

```
1. UTM / Parallels Desktop 安装 Windows 11 ARM
2. 安装 VC Redist (ARM64)
3. 复制构建到虚拟机
```

### 方式 C: CI 自动构建 + 远程桌面

```
1. 在 macOS 上构建 Windows 包（Unity batchmode: -buildTarget Win64）
2. scp/ftp 到 Windows 测试机
3. 远程桌面连接并手动测试
```

---

## 构建命令（macOS 上交叉构建）

```bash
/Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath /Users/zhugehao/projects/GanglandUndercover \
  -buildTarget Win64 \
  -executeMethod GanglandUndercover.Editor.BuildScript.BuildWindows \
  -logFile Builds/Logs/windows-build.log
```

输出: `Builds/Windows/GanglandUndercover.exe`

---

## 测试流程（总耗时 ~20 分钟）

### 第一阶段：启动验证（3 min）

```
1. 解压/复制构建文件夹到测试机
2. 双击 GanglandUndercover.exe
3. 等待 Unity 启动画面
4. 确认主菜单正常显示
5. 点 "匿 名 登 录" → 确认状态变 "匿名账号已就绪"
6. 打开设置 → 切换窗口模式 → 确认正常
7. 关闭 → 重开 → 确认设置保留
```

### 第二阶段：单机对局（10 min）

```
1. 进入大厅 → 创建房间
2. AI 补位 → 准备 → 开始
3. Opening 阶段 → 身份简报
4. Action 阶段 → WASD 移动
5. 走到任务点 → 交互 → 完成一个任务
6. Enter → 输入消息 → 发送 → 确认聊天
7. 按紧急铃 → 会议 → 投票
8. 跑完一局到结算
```

### 第三阶段：边界（5 min）

```
1. 无消息时确认 "举报最近" 灰色禁用
2. 发消息后确认按钮变亮
3. 点 "举报最近" + "屏蔽最近"
4. Alt+Tab 切出再切回
5. 切换分辨率 1280×720 / 1920×1080
6. 关闭游戏重开 → 确认设置保留
```

### 第四阶段：联机（如环境允许）

```
1. 构建一个 Windows 版 + 保持一个 macOS 版
2. Host (Win) 创建 Relay 房间
3. Client (Mac) 用房间码加入
4. 两端进入同一对局
5. 发送聊天消息确认两端互见
```

---

## 常见 Windows 问题

| 问题 | 可能性 | 解决 |
|------|--------|------|
| 双击 exe 没反应 | 缺 VC Redist | 安装 `vc_redist.x64.exe` |
| 启动后黑屏 | 显卡驱动 / DX11 问题 | 启动参数加 `-force-d3d11` |
| 启动后闪退 | IL2CPP 构建错误 | 切回 Mono 构建测试，然后查 Player.log |
| 中文显示乱码 | 系统区域语言设置 | 控制面板 → 区域 → 管理 → 非 Unicode 程序语言 → 中文(简体) |
| 联网失败 | Windows 防火墙 | 允许 GanglandUndercover.exe 通过防火墙 |

---

## 日志位置

```
Windows: %USERPROFILE%\AppData\LocalLow\[CompanyName]\GanglandUndercover\Player.log
macOS:   ~/Library/Logs/[CompanyName]/GanglandUndercover/Player.log
```

---

## 通过标准

```
□ 第一阶段 全部通过
□ 第二阶段 跑完完整对局
□ 第三阶段 无 Crash
□ Player.log 无 Exception 或 Error
□ 如测联机: Cross-play Win↔Mac 可用
```
