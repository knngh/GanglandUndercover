# D-4 Demo 发布记录（macOS 单平台，2026-10-17）

> **2026-10-18 更新**：migration 隔夜重试（run 5，09:11）**双端 PASS**（DJB688→BPMKPD，
> reconnected=true / replacementHost=true）——印证前夜定性为网络抖动，同代码清晨全绿。
> 里程碑门禁（每里程碑重跑双进程 migration）已满足；连续两绿关闭判定见文末补记。

> 里程碑：M-Demo.1 公开 Demo 的 macOS 部分（Windows 按 P0-1 用户拍板暂缓）。
> 本记录只含可自动化验证项；真人 30 分钟走查归 D-5 外部试玩。

## 发布产物

| 物 | 路径 | 说明 |
|----|------|------|
| App | `Builds/macOS/GanglandUndercover.app` | 282MB，universal（x86_64+arm64），版本 `0.3.0-demo` |
| DMG | `Builds/GanglandUndercover-0.3.0-demo-macOS.dmg` | 113MB，仅含 .app（无 Burst 调试符号） |

- 源码：`8766f30f`（聊天三修）+ `5a08790c`/`d918e112`（版本号+冒烟脚本）+ `c0b2c703`（走查指南）
- 构建入口：`GanglandUndercover.Editor.BuildScript.BuildMacOS`（batchmode -quit）
- **构建坑**：新构建会写进既有 .app 不清旧文件（6/4 旧可执行曾残留）；发布前先 `rm -rf Builds/macOS`
- **ProjectSettings 坑**：Unity batchmode 会用内存态回写覆盖外部对 ProjectSettings.asset 的编辑——改 bundleVersion 必须在无 Unity 实例运行时做

## 门禁证据（全部 2026-10-17 当日）

| 门禁 | 结果 | 证据 |
|------|------|------|
| EditMode 全量 | **245/245 PASS** | `ci-logs/chat3fix-editmode-results.xml` |
| PlayMode 全量 | **24 过 / 0 败 / 12 skip**（skip=opt-in 采样+Relay 双进程） | `ci-logs/chat3fix-playmode-results.xml` |
| Relay 双进程 normal | **双端 PASS**（房间码 W9MDGQ，防注入门禁全 true） | `ci-logs/d4-relay-normal-run.log` |
| Relay 双进程 migration | **✅ 已关闭**：10-18 run5+run6 连续双端 PASS（前夜 4 败定性网络抖动坐实） | `ci-logs/d4-relay-migration-run{1..6}.log` |
| 构建产物启动冒烟 | **PASS**（进程存活 20s、Player.log 零异常、主菜单渲染完整） | `ci-logs/d4-smoke-player.log` + `d4-smoke-screen.png` |

## migration 回归未过的定性（诚实记录）

4 次尝试三种失败样：
1. run 1（22:48）：client 侧**完整走通** join→接管→写新码（T6DD96→WWC7RB），仅旧 Host 侧重连后 60 帧稳定性断言失败——与 8 月 D-2-R1 已知"旧 Host 重连时序抖动"签名一致（当时同样先 1/2 失败后重跑过）。
2. run 2/3（22:54/22:59）：初 join 层就失败（client 发出加入请求但连接不建立/Host 会话早亡）——低于应用层，是到 Unity Relay 的网络抖动（同晚 22:46 normal 场景同一机制全绿）。
3. run 4（23:18）：走得最远——client 接管成功（R6QPTC），旧 Host 关旧连新时替代 Host 的 Relay 分配已失效（"房间码 R6QPTC 已失效"），仍为 join/UDP 层症状。

**非代码回归依据**：`HostMigrationManager.cs` 自 8 月三进程 PASS 后**零提交**；`git -S RequestRelayClient/RequestShutdown` 确认连接包装层同样零改动；今日失败前 client 侧已验证接管全链路健康。判定=本机到 Unity Relay 的网络不稳 + 已知时序抖动。**待网络稳定期重跑**：
`GANGLAND_RELAY_SCENARIO=migration bash run-relay-twoprocess.sh`（连续两次全绿即可关闭本项）。

## 对外分发注意

- 包**无开发者证书签名**（本地 ad-hoc）：测试者首次打开需右键→打开，或 `xattr -cr GanglandUndercover.app`
- macOS 首次启动可能慢（Gatekeeper 校验 universal binary）
- 联机走 Unity Relay（无需服务器），两台机器创建/加入同一房间码即可

## 遗留与边界

- **D-3 双人真人走查**：用户拍板跳过，清单已入库 `output/d3_two_player_walkthrough_checklist_20261017.md`（含摄像头远端画面、聊天三修回归、4 能力 VFX、5 分钟录像），随时可补
- Windows 构建：P0-1（缺 Build Support 模块），暂缓
- OnGUI 调试回退：发布版编译期早退（8/12 审查已验证）
