# Gangland Undercover — 朋友远程测试执行脚本

> 日期: 2026-06-10
> 目标: 用两台真实机器验证公网 Relay 房间码联机，形成可复盘的问题记录。
> 当前状态: Relay 双进程自动化已通过；FriendTest macOS 构建和 zip 分发包已生成；Lobby 状态栏已加入晚测截图/超时提示。

---

## 0. 测试口径

本轮只验证“朋友能远程进房并跑完核心闭环”，不做平衡评价。

通过标准:

- 两端都能启动并匿名登录。
- Host 能创建 Relay 房间并得到 6 位房间码。
- Client 能用房间码加入同一房间。
- 两端能 Ready，并能从 Lobby 进入对局。
- 移动、任务、会议/举报、文本聊天至少各验证一次。
- 任一失败点都有截图、时间、机器、步骤记录。

---

## 1. 发包前置

Host 和朋友都使用同一构建包。

| 项目 | 要求 | 状态 |
|------|------|------|
| 当前代码自动化 | EditMode 84/84 passed；PlayMode 8/10 passed, 2 ignored；Relay 双进程 PASS | 已满足 |
| macOS FriendTest 新构建 | `Builds/FriendTest-20260610/StandaloneOSX/GanglandUndercover.app` | 已生成 |
| 发给朋友的 zip | `Builds/FriendTest-20260610/GanglandUndercover-FriendTest-macOS-20260610.zip` | 已生成，96 MB |
| 构建代码提交 | `e0932942` | 已写入包内 build info |
| 旧构建 | `Builds/macOS/GanglandUndercover.app` 存在，但为 Unity 6000.4.5f1 旧包 | 不作为今天新验证包 |

分发包校验:

```text
sha256: ee35650855fa72fae224ae37097f975c47068743d4d26f956e453db38f3ef3a9
```

如需重建，使用命令:

```bash
/Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode \
  -projectPath /Users/zhugehao/projects/GanglandUndercover \
  -executeMethod GanglandUndercover.Editor.BuildScript.Build \
  -buildTarget StandaloneOSX \
  -outputDir /Users/zhugehao/projects/GanglandUndercover/Builds/FriendTest-20260610 \
  -logFile /Users/zhugehao/projects/GanglandUndercover/Logs/build-friendtest-macos-20260610-e0932942.log \
  -accept-apiupdate
```

本轮 Lobby 状态栏新增晚测提示:

- Host 创建房间后会提示截图房间码、已连接人数和玩家列表。
- Client 加入房间后会提示截图玩家列表、Ready 状态和等待 Host 开局。
- 输入房间码但未加入时会提示确认 6 位大写字母数字。
- 创建/加入超过 20 秒无变化时，截图状态栏、房间码和 Console。

---

## 2. Host 操作

| 步骤 | 操作 | 预期 |
|------|------|------|
| H1 | 启动游戏，进入主菜单 | 主菜单正常显示 |
| H2 | 点击匿名登录或等待自动登录 | 登录状态显示匿名账号已就绪 |
| H3 | 进入联机大厅 | 网络状态栏显示 Cloud/Auth/Lobby/Relay 可用 |
| H4 | 点击创建/Relay 开房 | 3-10 秒内生成 6 位房间码 |
| H5 | 把房间码发给朋友，并截图状态栏 | 保持游戏在 Lobby，不要切后台太久 |
| H6 | 朋友加入后观察玩家列表并截图 | 至少 2 名玩家可见 |
| H7 | 两端 Ready 后点击开始 | 两端进入 Opening/Action 阶段 |

---

## 3. Client 操作

| 步骤 | 操作 | 预期 |
|------|------|------|
| C1 | 启动同一构建包 | 主菜单正常显示 |
| C2 | 匿名登录 | 登录状态正常 |
| C3 | 进入联机大厅 | 能看到输入房间码入口 |
| C4 | 输入 Host 发来的 6 位大写房间码并加入 | 状态变为已加入，玩家列表出现 Host/Client |
| C5 | 截图玩家列表后点击 Ready | Host 端也能看到你的就绪状态 |
| C6 | 进入对局后移动 | 两端能看到彼此位置变化 |

---

## 4. 对局内最短闭环

按下面顺序跑，任一步失败就停下记录。

| 步骤 | 操作 | 预期 |
|------|------|------|
| M1 | 两端分别移动 10 秒 | 位置同步，无明显卡死 |
| M2 | 任意一端打开一个任务并完成 | 任务进度变化，另一端不报错 |
| M3 | 按 Enter 发送一条文本聊天 | 聊天区出现消息，频道标签正确 |
| M4 | 5 秒内再次发言 | 出现冷却提示，不刷屏 |
| M5 | 触发会议/举报入口 | 两端进入会议或看到对应状态 |
| M6 | 会议中发送聊天 | 会议聊天可见 |
| M7 | 投票或等待会议结束 | 能返回行动阶段或进入结果 |
| M8 | Client 退出游戏 | Host 不崩溃，玩家离开状态可解释 |
| M9 | Host 退出游戏 | Client 返回主菜单或显示断开提示 |

---

## 5. 问题记录模板

朋友反馈时只需要复制下面这段。

```text
时间:
机器/系统:
角色: Host / Client
步骤编号:
房间码是否已生成:
是否已加入房间:
现象:
截图/录屏:
是否有红色报错:
能否稳定复现:
补充:
```

---

## 6. 已知风险

- 构建包未签名；macOS 首次打开可能需要在系统安全设置中允许打开。
- 当前无 Host 迁移；Host 退出后 Client 应断开或回主菜单，不要求自动接管。
- Lobby 列表刷新不是本轮必须项；房间码加入优先。
- 第三客户端锁房/满员/密码场景可后置，今天先测两人远程闭环。
