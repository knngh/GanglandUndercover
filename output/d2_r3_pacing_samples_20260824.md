# D-2-R3 真实节奏采样记录

> 日期：2026-08-24
> 项目：`/Users/zhugehao/projects/GanglandUndercover`

## 采样入口

`NaturalPacingSamplingPlayTests` 提供 4/6/8/10 人独立 PlayMode 样本。测试只在
`GANGLAND_RUN_NATURAL_PACING=1` 时执行，使用生产规则和 `Time.timeScale=1`，不调用
强制会议、强制结算或时间压缩钩子。对局自然进入 `Result` 后读取
`Application.persistentDataPath/match_logs/*.json`，输出实际局时、会议次数、击杀数、
任务完成率、胜方和结算文案。

## 当前证据

### 2026-08-24 本轮恢复与回归

- Unity Licensing IPC 曾因旧 Licensing Client 全局互斥锁和 macOS `/tmp` 权限异常反复失败；清理本轮残留 Client 并将 `/private/tmp` 恢复为标准 `1777` 后，最小 EditMode 与完整 EditMode 均可启动。
- 自然采样入口的双参数重载曾使既有反射测试触发 `AmbiguousMatchException`。现已将采样专用入口改为独立方法名，保留原 `StartOnlineMatchCore(bool)` 反射契约。
- 修复后的完整 EditMode：`241/241 PASS`，证据：`ci-logs/20260824_resume_full_editmode_final.xml`。
- 4 人自然采样在本轮尚未进入对局：受限环境下 UPM Unix socket 创建仍返回 `EPERM`；沙箱外重试随后卡在 Licensing Client 的 `CookieContainer.GetDomainName: -1`。因此不计为样本。

| 配置 | 结果 | 证据 |
| --- | --- | --- |
| 4 人（首跑） | BLOCKED：测试进程在用例开始前丢失 Unity Licensing IPC，反复重连；未进入对局，不计样本 | `ci-logs/20260824_r3_natural_4.log` |
| 4 人（重跑） | BLOCKED：许可证和 UPM 已恢复，真实对局进入 `Opening -> Action`；生产时钟运行至 `elapsed=814.4874s` 仍未自然进入 `Result`，测试按有界时间门禁失败。没有伪造 MatchStats，不计样本 | `ci-logs/20260824_r3_natural_4_retry2.xml`、`ci-logs/20260824_r3_natural_4_retry2.log` |
| 4 人（本轮复跑） | BLOCKED：Unity 在测试装配前连续遭遇 Licensing IPC 重连失败；一次受限环境启动还因 `/tmp/Unity-Upm-*.sock` 监听权限退出，未进入对局，不计样本 | `ci-logs/20260824_r3_natural_probe.log`、`ci-logs/20260824_r3_natural_skip_after_probe.log` |
| 4 人（本轮恢复探测） | BLOCKED：针对性 EditMode/编译启动仍在 `LicenseClient-zhugehao` channel 缺失后循环重连；Unity Licensing 日志同时记录旧 Client 全局互斥锁和 `CookieContainer.GetDomainName: -1`，未产生新的 XML 或 Result 样本 | `ci-logs/20260824_resume_role_45.log`、`ci-logs/20260824_resume_compile.log`、`~/Library/Logs/Unity/Unity.Licensing.Client.log` |
| 4 人（HostName 修复后） | PASS：真实生产时钟进入 `Result`；局时 20:00，会议 0，击杀 0，任务 3/28，任务率 0.107，平局 | `ci-logs/20260824_d2r3_natural_4_hostname.xml`、`ci-logs/20260824_d2r3_natural_4_hostname.log`、`match_20260824_140030_927f84.json` |
| 6 人（HostName 修复后） | PASS：真实生产时钟进入 `Result`；局时 06:38，会议 1，击杀 1，任务 0/28，任务率 0.000，黑帮胜利 | `ci-logs/20260824_d2r3_natural_6_hostname.xml`、`ci-logs/20260824_d2r3_natural_6_hostname.log`、`match_20260824_140811_65c784.json` |
| 8 人（HostName 修复后） | PASS：真实生产时钟进入 `Result`；局时 11:22，会议 4，击杀 2，任务 0/28，任务率 0.000，警方胜利 | `ci-logs/20260824_d2r3_natural_8_hostname.xml`、`ci-logs/20260824_d2r3_natural_8_hostname.log`、`match_20260825_013327_8a4934.json` |
| 10 人（HostName 修复后） | PASS：真实生产时钟进入 `Result`；局时 20:00，会议 5，击杀 3，任务 1/28，任务率 0.036，平局 | `ci-logs/20260825_d2r3_natural_10_hostname.xml`、`ci-logs/20260825_d2r3_natural_10_hostname.log`、`match_20260825_015429_ff5ade.json` |

### 采样入口保障与证据状态

为避免单真人 Host 在随机分配中拿到 4/6 人局唯一 `Gang` 身份后长期闲置，
`NaturalPacingSamplingPlayTests` 现在使用专用的 `EditorSimulateNaturalPacingSample` 入口。
该入口只在编辑器自然采样路径启用：保留生产角色数量和其他身份随机性；若没有 Bot `Gang`，
则将真人 `Gang` 与一个 Bot `Police` 对调，并在采样开始前记录完整 roster。普通本地试玩、
Relay 和线上开局仍走原有随机角色分配。HostName 修复后四个配置均已使用该入口真实进入
`Result`；采样结果列出的 MatchStats 与 roster 均来自对应对局日志。

### 重跑判定

历史 4 人重跑曾在有界等待内停留于 `Action`，原因是环境时钟推进和 Licensing/UPM
阻塞，不是测试发现或断言字段错误；该历史记录不覆盖最终样本结论。HostName 修复后
重新执行的 4/6/8/10 人样本均自然进入 `Result`，未缩短规则时长或注入强制结算。

默认跳过验证：4/4 用例按设计 `Ignored`，说明采样入口已发现且不会污染常规回归，证据见
`ci-logs/20260824_r3_natural_skip.xml`。

### D-2-R3 收口结论（2026-08-25）

HostName 修复为 `zhugehaodeAir.local` 后，Unity Licensing/UPM IPC 稳定，4/6/8/10 人四个配置均在生产时钟（`Time.timeScale=1`）下真实进入 `Result`，并生成带完整字段的 MatchStats。结果覆盖：4 人硬时限平局、6 人黑帮胜利、8 人警方胜利、10 人硬时限平局。未使用强制会议、强制结算、时间压缩或伪造统计。

## 统计字段

结算日志中的 `MeetingCount`、`KillCount` 已由控制器实际属性写入；`TaskCompletionRate`
由 `CompletedTasks / TotalTasks` 计算，`WinningFaction` 从结算状态解析。旧的
`[PLACEHOLDER]` 注释已清理，避免把真实字段误报为未接入。

## 复跑命令

```text
GANGLAND_RUN_NATURAL_PACING=1 Unity -batchmode -nographics -projectPath . \
  -runTests -testPlatform PlayMode \
  -testFilter "NaturalPacing_Sample4Players" \
  -testResults ci-logs/d2r3_natural_4.xml
```
