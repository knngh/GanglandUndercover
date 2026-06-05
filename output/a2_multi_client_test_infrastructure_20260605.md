# A2 多客户端测试基础设施

日期：2026-06-05  
依赖：A1 NetworkPrefab 审计、阶段 0 编译基线

## 1. macOS 多进程启动脚本

### 1.1 快速双端测试

```bash
#!/bin/bash
# run_dual_client.sh — 启动 2 个 Unity 进程（Host + Client）

UNITY="/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity"
PROJECT="/Users/zhugehao/projects/GanglandUndercover"

# 终端 1: Host
osascript -e "tell app \"Terminal\" to do script \"$UNITY -projectPath $PROJECT -logFile /tmp/unity-host.log\""

sleep 5

# 终端 2: Client
osascript -e "tell app \"Terminal\" to do script \"$UNITY -projectPath $PROJECT -logFile /tmp/unity-client.log\""
```

### 1.2 四端压力测试脚本

```bash
#!/bin/bash
# run_quad_client.sh — 启动 4 个 Unity Editor 实例（Host + 3 Clients）

UNITY="/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity"
PROJECT="/Users/zhugehao/projects/GanglandUndercover"

echo "=== 启动 4 端联机测试 ==="

# Host
$UNITY -projectPath $PROJECT -logFile /tmp/unity-quad-host.log &
HOST_PID=$!
echo "Host PID: $HOST_PID"

sleep 8

# Client 1
$UNITY -projectPath $PROJECT -logFile /tmp/unity-quad-c1.log &
C1_PID=$!

sleep 3

# Client 2  
$UNITY -projectPath $PROJECT -logFile /tmp/unity-quad-c2.log &
C2_PID=$!

sleep 3

# Client 3
$UNITY -projectPath $PROJECT -logFile /tmp/unity-quad-c3.log &
C3_PID=$!

echo "Clients PID: $C1_PID $C2_PID $C3_PID"
echo ""
echo "=== 等待进程结束 ==="
wait $HOST_PID $C1_PID $C2_PID $C3_PID
echo "=== 所有进程已退出 ==="
```

## 2. 自动化测试场景清单

### 2.1 Smoke — 基础连接

| # | 场景 | Host 操作 | Client 验证 | 通过准则 |
|---|------|----------|-------------|---------|
| 1 | 直连入房 | 创建房间 | 输入 Host IP，加入成功 | Client 出现在大厅列表 |
| 2 | Relay 创建 | 点击 Relay 创建 | — | 生成房间码 |
| 3 | Relay 加入 | — | 输入房间码 | Client 进入大厅 |
| 4 | 准备同步 | 点击准备 | 看到 Host 准备状态 | 双端准备状态一致 |
| 5 | 踢人 | 踢出 Client | 看到被踢提示并返回 | Client 回到主菜单 |

### 2.2 A1 — NetworkPrefab 验证

| # | 场景 | Host 操作 | Client 验证 | 通过准则 |
|---|------|----------|-------------|---------|
| 6 | 摄像头同步 | 进入监控室 | 看到摄像头视野覆盖 | 摄像头 sprite 双端一致 |
| 7 | 小游戏桥 | Host 开始任务 | Client 看到任务站状态变化 | 任务进度同步 |
| 8 | 玩家角色 | 移动角色 | Client 看到角色移动 | 位置误差 < 0.5 单位 |
| 9 | 破坏对象 | Host 触发停电 | Client 看到遮罩+红灯 | VFX 双端一致 |

### 2.3 A2 — 完整局流程（4 端）

| # | 场景 | 操作 | 验证 |
|---|------|------|------|
| 10 | 开局身份 | 自动分配 | 4 端身份分配一致，无重复/遗漏 |
| 11 | 任务完成 | 各端完成至少 1 个任务 | 任务进度条同步 |
| 12 | 击倒/尸体 | Player A 击倒 Player B | 全部端看到尸体，可报案 |
| 13 | 报案/会议 | 任一玩家报案 | 4 端进入会议 UI |
| 14 | 会议/投票 | 每人投票 | 投票结果全部端一致 |
| 15 | 出局/结算 | 投票最高者出局 | 出局/存活状态一致 |
| 16 | 胜负判定 | 达成胜利条件 | 4 端胜负结果一致 |

### 2.4 A3 — 断线测试

| # | 场景 | 操作 | 验证 |
|---|------|------|------|
| 17 | Client 断线 | kill Client 进程 | Host 不崩溃，移除掉线玩家 |
| 18 | Client 重连 | 重新加入 | 状态同步恢复 |
| 19 | Host 断线 | kill Host 进程 | Client 显示"Host 断开"并返回 |

## 3. 运行时验证清单（来自 A1 审计）

```
□ [1] 日志搜索 "NetworkPrefab could not be found" 为 0
□ [2] 远端 Client 中 host-side-predicted NetworkVariable 与 Host 一致
□ [3] 所有 NetworkObject.Spawn 调用都有对应 AddNetworkPrefab
□ [4] Host 创建摄像头 → Client 看到
□ [5] Client 完成小游戏 → Host 收到结果
□ [6] 任务站完成状态双端一致
□ [7] 破坏状态 VFX 双端同步
□ [8] Snapshot 捕获/恢复双端测试
```

## 4. 测试执行记录模板

```markdown
## 测试报告 — A2 四端完整局

**日期**:
**Unity 版本**: 6000.4.5f1
**编译基线**: [commit hash]
**测试人员**:

### 连接方式
- [ ] 直连
- [ ] Relay

### 测试结果

| 场景 # | 描述 | Host | C1 | C2 | C3 | 备注 |
|--------|------|------|----|----|----|------|
| 1 | 直连入房 | | | | | |
| ... | | | | | | |
| 19 | Host 断线 | | | | | |

### 日志关键词搜索

```
NetworkPrefab could not be found: 0 次
globalObjectIdHash=0: 0 次  
NetworkVariable 不同步: 0 次
RPC 调用失败: 0 次
```

### 结论
- 通过场景: X/19
- 失败场景: Y/19
- 阻塞问题: 
