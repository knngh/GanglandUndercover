# Gangland Undercover — 发布检查清单 (Launch Checklist)

> Phase 6: 发布工程  
> 状态: 代码就绪，待真机验证

---

## 6.1 多平台构建

| 平台 | 构建命令 | 状态 | 备注 |
|------|---------|------|------|
| macOS | `./build_macos.sh` | ✅ 脚本就绪 | 需签名+公证 |
| Windows | `./build_windows.sh` | ✅ 脚本就绪 | 需真机验证 |
| 双平台 | `./build_all.sh` | ✅ 脚本就绪 | 顺序构建 |

## 6.2 网络与服务器

| 项目 | 状态 | 操作 |
|------|------|------|
| Unity Lobby 服务 | ⏳ 待配 | Unity Dashboard 创建 Lobby |
| Unity Relay 服务 | ⏳ 待配 | Unity Dashboard 创建 Relay |
| 代码集成 | ✅ | Relay/Lobby API 已集成 |
| Host 迁移 | ✅ | HostMigrationManager 已实现 |
| 区域测试 | ⏳ | 需部署后亚洲/北美/欧洲 ping |

## 6.3 性能基线

| 指标 | 目标 | 当前状态 |
|------|------|---------|
| 帧率 | 60 FPS @ 1080p | ⏳ 待测 |
| 内存 | < 2GB | ⏳ 待测 |
| 网络 | < 50 KB/s per client | ⏳ 待测 |
| 加载时间 | < 15s 进大厅 | ⏳ 待测 |

## 6.4 合规与商店

| 项目 | 状态 |
|------|------|
| Steamworks SDK 集成 | ✅ 代码预留接口 (SteamIntegration.cs) |
| Steam 成就 (8个) | ✅ 已定义 |
| Steam 排行榜 | ✅ 已定义 |
| 商店页面 | ⏳ 待创建 |
| 预告片 | ⏳ 待制作 |
| 年龄分级 | ⏳ 待提交 ESRB/PEGI |
| EULA | ⏳ 待撰写 |
| 隐私政策 | ⏳ 待撰写 |

## 6.5 测试流程

| 阶段 | 人数 | 目标 | 状态 |
|------|------|------|------|
| 内部 Alpha | 4-6 人 | 崩溃/卡死 | ⏳ |
| 封闭 Beta | 10-20 人 | 平衡/留存 | ⏳ |
| 开放 Demo | 50+ 人 | 服务器压力 | ⏳ |
| EA 上线 | — | Steam 发布 | ⏳ |

## 6.6 CI/自动化

| 项目 | 状态 |
|------|------|
| batchmode 编译 | ✅ `ci_run.sh` |
| EditMode 测试 | ✅ CIRunner.cs |
| PlayMode 测试 | ✅ CIRunner.cs |
| 构建自动化 | ✅ BuildScript.cs |
| 崩溃上报 | ⏳ 待接入 Sentry/Crashlytics |

---

> 代码层已完成 100%。剩余均为运营/平台/测试类任务，需真机和第三方服务配合。
