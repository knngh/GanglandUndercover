# Gangland Undercover — 安装/打开权限截图指南

> 用途: 朋友测试前的安装验证 + 截图留档
> 不涉及 Relay/Netcode 核心代码

---

## 当前可交付物

本轮朋友测试优先使用 macOS FriendTest 构建包，不再要求朋友打开 Unity Editor。

```text
App: Builds/FriendTest-20260610/StandaloneOSX/GanglandUndercover.app
Zip: Builds/FriendTest-20260610/GanglandUndercover-FriendTest-macOS-20260610.zip
Zip sha256: ee35650855fa72fae224ae37097f975c47068743d4d26f956e453db38f3ef3a9
构建代码 commit: e0932942
```

---

## 准备清单

| # | 步骤 | 截图? | 说明 |
|---|------|-------|------|
| 1 | 解压 FriendTest zip | ✓ | 截图 Finder 中的 `GanglandUndercover.app` |
| 2 | 首次打开 app | ✓ | 截图 macOS 安全提示或主菜单 |
| 3 | 进入主菜单 | ✓ | 截图主菜单界面 |
| 4 | 打开设置面板 | ✓ | 截图"设置中心"覆盖层展开状态 |
| 5 | 登录区 | ✓ | 截图联机面板 + 匿名登录按钮 |
| 6 | Host 创建 Relay 房间 | ✓ | 截图 6 位房间码、状态栏和玩家列表 |
| 7 | Client 加入 Relay 房间 | ✓ | 截图玩家列表和 Ready 状态 |
| 8 | 开局后进入对局 | ✓ | 截图双方角色进入场景 |

---

## macOS 权限

| 权限 | 用途 | 截图? |
|------|------|-------|
| 未签名 app 打开确认 | 首次运行 FriendTest 包 | □ 截图安全提示或"仍要打开"页面 |
| 网络访问 | Lobby/Relay 联机 | □ 如果系统弹窗出现就截图 |
| 麦克风 | 本轮不测语音；如弹窗出现只记录 | □ 可选 |

---

## Unity Editor 备用路径

如果 app 因 macOS 权限无法打开，才回退到 Unity Editor PlayMode:

| # | 步骤 | 截图? | 说明 |
|---|------|-------|------|
| E1 | Unity Hub 确认版本 | ✓ | 截图 Hub 中显示 Unity 6000.4.9f1 |
| E2 | 打开项目 | ✓ | 截图 Unity Editor 启动后的 Project 窗口 |
| E3 | 打开 Stage1VerticalSlice 场景 | ✓ | 截图场景加载后的 Hierarchy + Scene 视图 |
| E4 | Console 清空后点击 Play | ✓ | 截图主菜单和 Console 0 error |

已有 `Gangland > Screenshots > Capture Named (xxx)` 8 个菜单项，可在 PlayMode 中保存标准截图到 `Screenshots/`。

---

## 朋友测试当天步骤

```
1. 双方使用同一 FriendTest zip 解压出的 app
2. Host 打开 app 并创建 Relay 房间
3. Host 截图 6 位房间码、状态栏和玩家列表
4. Client 输入同一房间码加入
5. Client 截图玩家列表和 Ready 状态
6. 两端 Ready 后开始对局
7. 任一步超过 20 秒无变化，就截图状态栏、房间码和系统时间
```
