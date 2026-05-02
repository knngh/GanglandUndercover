# Unity 打开与验证步骤

## 现在的状态

本机已检测到 Unity Hub：

`C:\Program Files\Unity Hub\Unity Hub.exe`

但还没有检测到 Unity Editor：

`C:\Program Files\Unity\Hub\Editor\<版本号>\Editor\Unity.exe`

Unity Hub 是管理器，不是编辑器。需要在 Hub 里再安装一个 Editor 版本，项目才能编译和运行。

## 安装 Editor

1. 打开 Unity Hub。
2. 进入 `Installs` / `安装`。
3. 点击 `Install Editor` / `安装编辑器`。
4. 推荐安装 `Unity 2022.3 LTS`。如果 Hub 只推荐更新的 LTS 版本，也可以先安装。
5. 模块先选 `Windows Build Support`。如果以后要做安卓，再加 `Android Build Support`。

## 打开项目

1. 在 Unity Hub 里点击 `Projects`。
2. 点击 `Add` / `添加`。
3. 选择项目目录：

`C:\Users\Admin\GanglandUndercover`

4. 打开项目，等待 Unity 编译脚本。

## 生成原型场景

编译完成后，顶部菜单会出现：

`Gangland > Create Prototype Scene`

点击后会生成：

`Assets/_Project/Scenes/Prototype.unity`

然后点击 `Play`。

## 如果菜单没有出现

说明脚本还没有编译成功。先看 Unity Console 的红色报错。

备用方式：

1. 新建空场景。
2. 创建空 GameObject，命名为 `PrototypeBootstrap`。
3. 挂载脚本：

`Assets/_Project/Scripts/Gameplay/PrototypeBootstrap.cs`

4. 点击 `Play`。
