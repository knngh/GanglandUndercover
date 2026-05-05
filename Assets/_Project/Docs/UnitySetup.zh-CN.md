# Unity 打开与验证步骤

## 现在的状态

本机已检测到 Unity Hub 和 Unity Editor。当前可用 Editor：

`/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app`

项目已按本机 Unity `6000.4.5f1` 打开和升级。继续开发时使用同一版本，避免反复升级/降级项目设置。

## 打开项目

1. 打开 Unity Hub。
2. 进入 `Projects` / `项目`。
3. 点击 `Add` / `添加`。
4. 选择项目目录：

`/Users/zhugehao/projects/GanglandUndercover`

5. 用 Unity `6000.4.5f1` 打开项目，等待 Unity 导入和编译脚本。

## 生成原型场景

编译完成后，顶部菜单会出现：

`Gangland > Create Prototype Scene`

点击后会生成：

`Assets/_Project/Scenes/Prototype.unity`

然后点击 `Play`。当前启动的是可玩的港区社交推理 Demo：`港区潜线 / Harbor Undercover`。

默认会直接以卧底身份开局。也可以在左侧 HUD 按钮切换为警察或黑帮重开。

## 当前操作

- `WASD`：移动。
- `E`：取证任务 / 黑帮破坏 / 紧急会议按钮。
- `R`：报告附近尸体。
- `Q`：黑帮身份击倒附近目标。

专案组胜利条件：完成所有证据任务，或在会议中投出黑帮。黑帮胜利条件：击倒足够多专案组成员、拖到倒计时结束，或让警方阵营失去人数优势。

## 验证烟测

Unity 授权激活正常后，可以在 Unity 菜单执行：

`Gangland > Run Smoke Tests`

也可以用命令行跑批处理烟测：

```bash
'/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity' -batchmode -quit -projectPath /Users/zhugehao/projects/GanglandUndercover -executeMethod GanglandUndercover.Editor.PrototypeSmokeTests.Run -logFile /Users/zhugehao/projects/GanglandUndercover/unity-smoke.log
```

如果批处理卡在 `Licensing initialization failed`，先在 Unity Hub 里登录并完成 Unity 个人版或专业版授权，再重新运行。

## 如果菜单没有出现

说明脚本还没有编译成功。先看 Unity Console 的红色报错。

备用方式：

1. 新建空场景。
2. 创建空 GameObject，命名为 `PrototypeBootstrap`。
3. 挂载脚本：

`Assets/_Project/Scripts/Gameplay/PrototypeBootstrap.cs`

4. 点击 `Play`。
