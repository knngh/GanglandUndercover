# D2 碰撞与通道优化标准文档

日期：2026-06-05
前置：D1 地图验证基础、M6.1 MapLayoutData 灰盒地图布局
目标：为所有地图建立统一的碰撞体、通道宽度、通风管入口和监控盲区的验证标准

---

## 1. 地图可步行区域验证标准

### 1.1 房间可步行验证

> 参考：`MapLayoutData.cs:157-177` RoomDefinition 结构体

每个房间必须满足以下验证条件：

| 验证项 | 标准 | 检查方法 |
|--------|------|---------|
| 最小内径 | 房间最短轴 >= 1.6 设计单位 | 检查 RoomDefinition.Size 最小分量 |
| 出入口数量 | 每房间至少 1 个出入口，枢纽房间至少 2 个 | 遍历 CorridorDefinition 关联 |
| 出入口宽度 | 每个出入口通道宽度 >= 1.0 设计单位 | CorridorDefinition.Size 最窄处 |
| 出生点可达性 | 所有 SpawnPoints 从会议点可达（走廊连通） | BFS/DFS 连通性检测 |
| 任务点可达性 | 所有 TaskAssignments 从任意 SpawnPoint 可达 | 同上 |

### 1.2 走廊可步行验证

> 参考：`MapLayoutData.cs:179-200` CorridorDefinition 结构体

| 验证项 | 标准 | 说明 |
|--------|------|------|
| 走廊类型 | Walkable == true | 不可步行走廊用于装饰/遮挡 |
| 最小宽度 | >= 1.0 设计单位 | 低于此值玩家可能穿墙 |
| 走廊间交叉 | 相邻走廊必须有重叠区域或节点连接 | IsRoundNode 节点作交叉枢纽 |
| 圆形节点半径 | NodeRadius >= 0.5 设计单位 | 用于走廊交叉点 |
| 连通性 | 无孤立走廊段 | BFS 从会议点遍历所有走廊 |

### 1.3 每张地图的验证清单

#### HarbourDistrict（港区）

| 验证项 | 预期值 | 验证状态 |
|--------|--------|---------|
| 房间数 | 12 | [ ] |
| 走廊数 | 8 | [ ] |
| 出生点数 | 10 | [ ] |
| 任务点数 | 12-16 | [ ] |
| 会议点位于中心走廊可达区域 | 是 | [ ] |
| 所有房间从会议点可达 | 是 | [ ] |
| 无穿模死角 | 无 | [ ] |

#### PoliceStation（警署）

| 验证项 | 预期值 | 验证状态 |
|--------|--------|---------|
| 房间数 | 6（参考 `PoliceStationMapLayout.cs:27-86`） | [ ] |
| 走廊数 | 7（含 1 个圆形节点，参考 `PoliceStationMapLayout.cs:91-166`） | [ ] |
| 出生点数 | 10（参考 `PoliceStationMapLayout.cs:312-327`） | [ ] |
| 任务点数 | 8（参考 `PoliceStationMapLayout.cs:171-192`） | [ ] |
| 所有房间从会议点（0, -0.5）可达 | 是 | [ ] |
| 走廊宽度 >= 0.9（紧凑地图） | 是 | [ ] |

---

## 2. 通道宽度标准

### 2.1 宽度分级定义

| 分级 | 宽度（设计单位） | 用途 | 说明 |
|------|-----------------|------|------|
| 2-wide 主通道 | >= 2.0 | 连接多个房间的主走廊 | 允许两人并行通过，是主要交通动线 |
| 1-wide 标准通道 | 1.0 - 1.9 | 连接房间与主走廊的支路 | 仅允许单人通过，提供隐蔽和策略空间 |
| 0.8-wide 窄通道 | 0.8 - 0.9 | 特殊用途（通风管入口附近、隐藏路径） | 最小通行宽度，谨慎使用 |

### 2.2 港区（HarbourDistrict）通道标准

| 走廊名称 | 当前宽度建议 | 最低要求 | 说明 |
|----------|-------------|---------|------|
| 主街（中心南北向） | 2.5 | 2.0 | 主通道，2-wide |
| 码头栈桥（东西向） | 2.2 | 2.0 | 主通道，2-wide |
| 夜市巷 | 1.5 | 1.0 | 标准通道 |
| 地下诊所走廊 | 1.2 | 1.0 | 标准通道 |
| 仓库侧道 | 1.0 | 1.0 | 标准通道下限 |
| 货柜间隙 | 1.2 | 1.0 | 标准通道 |

### 2.3 警署（PoliceStation）通道标准

> 参考 `PoliceStationMapLayout.cs:91-166` 中走廊尺寸定义

| 走廊名称 | 宽度（设计坐标） | 最低要求 | 实际值 |
|----------|-----------------|---------|--------|
| 大厅↔审讯室 | 3.5 x 1.0 | 0.9 | 沿长度方向 1.0 |
| 大厅↔证物室 | 3.2 x 1.0 | 0.9 | 沿宽度方向 1.0 |
| 大厅↔简报室 | 3.0 x 1.0 | 0.9 | 沿宽度方向 1.0 |
| 审讯室↔拘留室 | 2.6 x 0.9 | 0.9 | 沿宽度方向 0.9 |
| 证物室↔监控室 | 6.3 x 0.9 | 0.9 | 沿宽度方向 0.9 |
| 监控室↔简报室 | 0.9 x 3.3 | 0.9 | 沿长度方向 0.9 |
| 中央枢纽（圆形节点） | 半径 0.9 | 0.5 | NodeRadius=0.9 |

### 2.4 通道宽度验证流程

1. 加载 MapLayoutData ScriptableObject
2. 遍历所有 CorridorDefinition，检查 Size 最小分量
3. 标记低于最低要求的走廊（警告）
4. 检查相邻走廊的交叉区域是否存在（重叠检测）
5. 标记宽度突变 > 0.5 的位置（可能导致碰撞抖动）

---

## 3. 通风管入口/出口验证规则

> 参考：`VentSystem.cs:10-25` VentNode 结构体，`MapLayoutData.cs:217-228` VentNodeDefinition

### 3.1 通风管节点验证条件

| 验证项 | 标准 | 说明 |
|--------|------|------|
| 入口可访问性 | 每个通风管节点在房间或走廊内 | Position 必须在 Walkable 区域内 |
| 入口周围空间 | 节点位置 0.9m 范围内无可阻挡物体 | 匹配 `VentSystem.ventRange = 0.9f` |
| 连接数 | 每个节点至少连接 1 个其他节点 | ConnectedNodeIndices.Length >= 1 |
| 图连通性 | 通风管网必须是一个连通图 | 从任意节点 BFS 可达所有节点 |
| 不与出口重叠 | 同房间的通风管入口/出口不重叠 | 距离 >= 2.0 设计单位 |
| 无自环 | 节点不连接自身 | ConnectedNodeIndices 不含自身索引 |

### 3.2 通风管位置限制

| 限制项 | 规则 | 原因 |
|--------|------|------|
| 不放在会议点附近 | 距 MeetingCenter >= 3.0 | 防止开局通风管快速跳转 |
| 不放在出生点附近 | 距任意 SpawnPoint >= 1.5 | 防止开局通风管利用 |
| 入口必须在可视区域 | 通风管入口上方无视线遮挡 | 让 Police 能看到通风管使用 |
| 出口有遮挡更好 | 出口附近有 BlockerVolume | 让 Gang 使用后能隐蔽 |

### 3.3 警署通风管验证

> 参考 `PoliceStationMapLayout.cs:197-226`

| 节点 | 位置 | 连接 | 验证状态 |
|------|------|------|---------|
| 大厅通风管 | (0, 0) | 审讯室、证物室、监控室 | [ ] |
| 审讯室通风管 | (-3.0, 1.3) | 大厅、拘留室 | [ ] |
| 证物室通风管 | (-2.7, -1.3) | 大厅、监控室 | [ ] |
| 监控室通风管 | (2.8, -1.2) | 证物室、大厅 | [ ] |

验证要点：
- [ ] 4 个节点形成连通图
- [ ] 所有节点位置在对应房间范围内
- [ ] 通风管冷却 >= VentSystem.ventCooldown(10s)

---

## 4. 监控摄像头盲区设计原则

> 参考：`SecurityCamera.cs:11-383` 摄像头系统，`MapLayoutData.cs:230-245` SurveillanceZoneDefinition

### 4.1 摄像头技术参数

| 参数 | 值 | 来源 |
|------|-----|------|
| 检测距离 | 3.5 单位 | `SecurityCamera.DetectionRange` |
| 锥形半角 | 55 度 | `SecurityCamera.DetectionHalfAngle` |
| 总视野角度 | 110 度 | 2 x DetectionHalfAngle |
| 监控站交互距离 | 1.3 单位 | `SecurityCamera.MonitorInteractRange` |
| 摄像头数量（港区） | 4 | SecurityCamera.CreateCameraNodes |

### 4.2 盲区设计原则

#### 原则 1：必须存在盲区

每张地图必须有以下类型的盲区：

| 盲区类型 | 定义 | 设计意图 |
|----------|------|---------|
| 角落盲区 | 房间角落，超出摄像头锥形视野 | Gang 的击杀/通风管利用空间 |
| 走廊盲区 | 相邻两个摄像头视野之间的间隙 | 玩家移动中的隐蔽窗口 |
| 通风管出口盲区 | 通风管出口不在任何摄像头视野内 | 允许通风管使用不被监控记录 |

#### 原则 2：关键区域必须有覆盖

| 必须覆盖区域 | 原因 |
|-------------|------|
| 会议点附近 | 防止会议前连续击杀不被发现 |
| 主走廊交叉点 | 高流量区域需要监控 |
| 至少 1 个任务站 | 让监控有信息价值 |

#### 原则 3：盲区面积占比

| 指标 | 目标 | 说明 |
|------|------|------|
| 监控覆盖率（面积） | 50-70% | 太低=摄像头无用，太高=Gang 无操作空间 |
| 监控盲区（面积） | 30-50% | 必须有足够空间供 Gang 活动 |
| 全局可见时间比 | 40-60% | 一个玩家走完一圈时处于监控视野的时间占比 |

### 4.3 摄像头布局验证

对每个 SurveillanceZoneDefinition 检查：

| 检查项 | 方法 |
|--------|------|
| 摄像头视野不重叠过多 | 两个摄像头视野重叠面积 < 单个视野面积的 30% |
| 无完全未覆盖的房间 | 每个房间至少被 1 个摄像头部分覆盖 |
| 盲区可达性 | 所有盲区从走廊可达（不创建死区） |
| 摄像头位置合理性 | 摄像头不能放在通风管出口正上方 |

### 4.4 港区摄像头验证

> 参考 `SecurityCamera.cs:70-79`

| 摄像头 | 位置 | 朝向 | 覆盖区域 | 验证状态 |
|--------|------|------|---------|---------|
| 码头监控 | (-4.05, 1.55) | (0.9, -0.45) | 货柜码头区 | [ ] |
| 夜市监控 | (0.65, 3.05) | (-0.55, -0.85) | 夜市巷区域 | [ ] |
| 办公室监控 | (4.55, 1.5) | (-0.95, -0.3) | 专案办公室区域 | [ ] |
| 走廊监控 | (-0.55, 0.85) | (0, -1) | 主街/竖巷交汇 | [ ] |

### 4.5 警署摄像头验证

> 参考 `PoliceStationMapLayout.cs:231-264`

| 摄像头 | 覆盖区域 | 验证状态 |
|--------|---------|---------|
| 大厅监控 | (0, 0) 覆盖 4.0x3.0 | [ ] |
| 审讯室监控 | (-3.2, 1.6) 覆盖 2.8x2.2 | [ ] |
| 证物室监控 | (-3.0, -1.8) 覆盖 3.0x2.2 | [ ] |
| 监控室监控 | (3.1, -1.6) 覆盖 2.6x2.0 | [ ] |

---

## 5. AI Bot 导航验证清单

> 参考：`OpponentAi.cs:18-529` AI 决策引擎

### 5.1 导航基础验证

| 验证项 | 标准 | 检查方法 |
|--------|------|---------|
| 出生点可达所有任务点 | BFS 连通 | 从每个 SpawnPoint 到每个 TaskAssignment.Position |
| 出生点可达所有房间 | BFS 连通 | 从每个 SpawnPoint 到每个 RoomDefinition.Center |
| 通风管路径对 Bot 可见 | Bot 能检测 VentNode 位置 | Bot NavMesh 标记通风管区域为可通行（Gang Bot） |
| 摄像头区域对 Bot 可见 | Bot 能识别监控覆盖区域 | Bot AI 在监控视野内降低可疑行为概率 |

### 5.2 AI 行为验证

| AI 角色 | 验证项 | 预期行为 | 参考 |
|---------|--------|---------|------|
| Gang Bot | 路径选择 | 优先去高风险区（Dockyard, NightMarket） | `OpponentAi.cs:22` HighRiskDistricts |
| Gang Bot | 击杀位置 | 在摄像头盲区或通风管附近执行 | 避免被监控记录 |
| Undercover Bot | 任务执行 | 去信息区（PolicePrecinct, Clinic, WarehouseRow） | `OpponentAi.cs:23` IntelDistricts |
| Undercover Bot | 伪装行为 | 低嫌疑时偶尔去非信息区降低嫌疑 | `OpponentAi.cs:188-194` |
| Police Bot | 巡逻路线 | 去黑帮影响力最高的区域 | `OpponentAi.cs:229-230` |
| Police Bot | 封锁行为 | PoliceHeat >= 7 时封锁区域 | `OpponentAi.cs:235-243` |
| Mole Bot | 混合行为 | 25% 去高风险区，30% 去信息区，45% 随机巡逻 | `OpponentAi.cs:281-305` |

### 5.3 Bot 导航性能验证

| 验证项 | 标准 |
|--------|------|
| Bot 到最近任务点移动时间 | <= 15 秒（任何出生点） |
| Bot 到最近通风管移动时间 | <= 10 秒（Gang Bot 专用） |
| Bot 到监控站移动时间 | <= 12 秒（Police Bot 专用） |
| Bot 路径无死循环 | Bot NavMesh 不存在重复往返路径 |
| Bot 不穿墙 | 碰撞体完全覆盖非 Walkable 区域 |

### 5.4 Bot 思考时间验证

> 参考 `m8_balance_tuning_guide.md` 中 BotThink 参数

| 参数 | 当前值 | 建议范围 | 说明 |
|------|--------|---------|------|
| BotThinkMinSeconds | 1.2 | 0.8-2.5 | Bot 最短决策间隔 |
| BotThinkMaxSeconds | 3.4 | 2.0-5.0 | Bot 最长决策间隔 |
| BotTaskSpeedMultiplier | 1.0 | 0.6-1.5 | Bot 任务完成速度倍率 |

---

## 6. 碰撞体层级规范

### 6.1 Unity Layer 分配建议

| Layer | 名称 | 用途 |
|-------|------|------|
| Default | 建筑/墙壁 | 标准碰撞体，阻止所有角色移动 |
| Player | 玩家角色 | 玩家间碰撞分离（可选） |
| Bot | AI Bot | Bot 与玩家同层碰撞 |
| Vent | 通风管入口 | 特殊交互触发区域 |
| Camera | 摄像头 | 视锥检测（非物理碰撞） |
| Task | 任务站 | 交互触发区域 |
| SightBlocker | 视线遮挡 | 光线投射检测 |

### 6.2 碰撞体类型

| 区域 | 碰撞体类型 | Layer | 说明 |
|------|-----------|-------|------|
| 房间墙壁 | BoxCollider2D | Default | 房间边界 |
| 走廊墙壁 | BoxCollider2D | Default | 走廊边界 |
| 走廊交叉点 | CircleCollider2D | Default | 圆形节点（IsRoundNode） |
| 通风管入口 | CircleCollider2D (trigger) | Vent | VentSystem.ventRange(0.9m) |
| 监控站 | CircleCollider2D (trigger) | Task | SecurityCamera.MonitorInteractRange(1.3m) |
| 任务站 | CircleCollider2D (trigger) | Task | 任务交互范围 |
| 视线遮挡体 | BoxCollider2D | SightBlocker | MapLayoutData.BlockerVolume |
| 会议按钮 | CircleCollider2D (trigger) | Task | EmergencyButton 交互 |

### 6.3 碰撞验证自动化

```csharp
// 伪代码：MapValidator 碰撞验证
foreach (var room in layout.Rooms) {
    // 验证房间碰撞体完整覆盖
    Assert(RoomColliderCovers(room), $"房间 {room.Name} 碰撞体不完整");
}
foreach (var corridor in layout.Corridors) {
    // 验证走廊碰撞体连续性
    Assert(CorridorCollidersConnected(corridor), $"走廊 {corridor.Name} 碰撞体不连续");
}
// 验证通风管入口 trigger 覆盖 VentRange
foreach (var vent in layout.VentNodes) {
    Assert(VentTriggerExists(vent), $"通风管 {vent.Name} 入口 trigger 缺失");
}
```

---

## 7. 实施优先级

| 优先级 | 项目 | 影响 | 工作量 |
|--------|------|------|--------|
| P0 | 走廊宽度验证 + 连通性检查 | 阻塞 — 不修则穿墙/死路 | 中 |
| P1 | 通风管节点验证 | 高 — 通风管是核心机制 | 低 |
| P1 | 摄像头盲区设计验证 | 高 — 监控影响平衡 | 中 |
| P2 | Bot 导航验证 | 中 — 影响 AI 对局质量 | 中 |
| P3 | 碰撞 Layer 规范化 | 低 — 优化阶段处理 | 低 |

---

## 8. 版本记录

| 日期 | 修改内容 |
|------|---------|
| 2026-06-05 | 初版，基于 MapLayoutData + VentSystem + SecurityCamera + OpponentAi 代码审计建立标准 |
