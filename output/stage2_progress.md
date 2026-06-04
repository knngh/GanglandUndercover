---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_0fa69d615da811f1a4f35254002afed2
    ReservedCode1: ypbBgKnYj6OtD5okFK5sHY5H0Pdsj+7W+/96zBDB+tJPzXxebzap0MSKAQdnTrqpdq+VMi6cekeUeAuDdQ4ktUeGCG/rGn+tfHmJ3DdtdllGlfY5262zgxRHlrqu3iBfRHUf8rj4E1lq4HFzJHDgO1VDEwmW0i0Fmn0MU90Cqqk4Qt8nZmdY8uCKAO0=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_0fa69d615da811f1a4f35254002afed2
    ReservedCode2: ypbBgKnYj6OtD5okFK5sHY5H0Pdsj+7W+/96zBDB+tJPzXxebzap0MSKAQdnTrqpdq+VMi6cekeUeAuDdQ4ktUeGCG/rGn+tfHmJ3DdtdllGlfY5262zgxRHlrqu3iBfRHUf8rj4E1lq4HFzJHDgO1VDEwmW0i0Fmn0MU90Cqqk4Qt8nZmdY8uCKAO0=
---

# GanglandUndercover 第2阶段开发进度

**日期**: 2026-06-01  
**阶段**: 第2阶段 — 角色与动画替换  
**状态**: 进行中（部分完成，需 Unity Editor 手动操作）

---

## 一、当前状态总览

### 1.1 Animator Controller 状态

| 文件 | 路径 | 状态 |
|------|------|------|
| GanglandCharacter.controller | `Assets/_Project/Art/Animators/` | ✅ 已通过脚本生成（需验证） |
| GanglandCharacter_Override.controller | `Assets/_Project/Art/Animators/` | ✅ 已通过脚本生成（需验证） |

> ⚠️ **重要**: 以上两个文件已通过外部脚本写入，但 Unity 的 .controller 文件包含复杂的内部 GUID 引用，建议**在 Unity Editor 中通过菜单 `Gangland → Setup Character Animator Controller` 重新生成**，以确保 GUID 引用正确。

### 1.2 角色资源盘点

#### DenysAlmaral CityPeople（主力角色，第1层）

**路径**: `Assets/_Project/Resources/AssetStore/DenysAlmaral/CityPeople/Prefabs/`

| 角色 Prefab | 性别 | 可用动画 | 用途建议 |
|-------------|------|----------|----------|
| `city/casual_Male_G` | 男 | idle/walk/jog/phoneTalk | 主力 NPC / 玩家 |
| `city/casual_Female_G` | 女 | idle/walk/jog | 主力 NPC |
| `downtown/casual_Male_K` | 男 | idle/walk/jog/phoneTalk | 主力 NPC / 卧底 |
| `downtown/casual_Female_K` | 女 | idle/walk/jog | 主力 NPC |
| `professions/Doctor_Male_B` | 男 | idle/walk/jog | 特殊职业 |
| `professions/police_Female_A` | 女 | idle/walk/jog | 警察角色 |
| `elder/elder_Female_A` | 女 | idle/walk/jog | 老年 NPC |
| `little_kids/little_boy_B` | 男(儿童) | idle/walk/jog | 儿童 NPC |
| `construction/worker_Male_constructor_B` | 男 | 7个施工动画 | 施工场景 NPC |
| `disabilities/prostheticLeg_girl` | 女 | 专用动画 | 特殊 NPC |

**动画文件 GUID 映射**（用于 Animator Controller 配置）:

| 动画 | 内部 GUID（男性） | 内部 GUID（女性） |
|------|-------------------|-------------------|
| Idle | `40d3a309e9945334284a3a33b46139e7` | `ea917c84da27c514c89479d48da89544` |
| Walk | `6599ecd64e0c14740acc7d5f8b82f1f5` | `a9ce97153f7113649bd17bbb3de7a81e` |
| Jog | `66fd1714b985d8c47806307c1442de96` | `89b462e93e068774491b0384208f1d84` |
| PhoneTalk | `3975160a0bfe0d94098f615c8283969c` | — |

#### Synty PolygonStarter（兜底角色，第2/3层）

**路径**: `Assets/_Project/Resources/AssetStore/Synty/PolygonStarter/Prefabs/Characters/`

| 角色 Prefab | 类型 | 骨骼 | 动画 | 用途 |
|-------------|------|------|------|------|
| `SM_Chr_Male_01` | Chr | ✅ 有 | ❌ 无 | 第2层兜底 |
| `SM_Chr_Female_01` | Chr | ✅ 有 | ❌ 无 | 第2层兜底 |
| `SM_Bean_Cop_01` | Bean | ❌ 无 | ❌ 无 | 第3层极简兜底 |
| `SM_Bean_Cowboy_01` | Bean | ❌ 无 | ❌ 无 | 第3层极简兜底 |
| `SM_Bean_Female_01` | Bean | ❌ 无 | ❌ 无 | 第3层极简兜底 |
| `SM_Bean_Town_Female_01` | Bean | ❌ 无 | ❌ 无 | 第3层极简兜底 |

#### Quaternius（不适用）

路径: `Assets/_Project/Resources/AssetStore/Quaternius/` — 内容为科幻超级英雄风格，与本项目都市写实风格不符，**不建议使用**。

---

## 二、现有角色系统分析

### 2.1 两套角色系统

项目中有两套独立的角色生成系统：

#### 系统A：社交推理模式（`SocialPrototypeController.cs`）

- **文件**: `Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs`
- **角色创建方式**: `CreateCharacter()` 方法直接通过 `GameObject.CreatePrimitive(PrimitiveType.Capsule)` 创建胶囊体
- **组件挂载**: 手动添加 `SocialCharacter` 组件、`TextMesh` 标签
- **动画驱动**: `SocialCharacter.Bind()` 中通过 `GetComponentInChildren<Animator>()` 查找 Animator — **当前胶囊体无 Animator，动画不生效**
- **颜色区分**: 通过 `SetColor()` 用纯色 Material 区分角色身份
- **当前问题**: 角色为胶囊体+方块，无真实模型，无动画表现

#### 系统B：联机模式（`OnlineMatchController.cs`）

- **文件**: `Assets/_Project/Scripts/Online/OnlineMatchController.cs`（主文件 + 分部文件）
- **角色创建方式**: `CreateFreeCharacterAdapter()` 实例化真实 Prefab
- **Prefab 映射**: `FreeCharacterPrefabPath()` 按 `OnlineProfession` 枚举分配资源：
  - `Inspector` / `Tech` → `SM_Bean_Cop_01`（Synty Bean）
  - `Forensics` → `casual_Male_G`（DenysAlmaral）
  - `UndercoverAgent` → `casual_Male_G`（DenysAlmaral）
  - `Enforcer` → `casual_Male_K`（DenysAlmaral）
  - `Fixer` → `casual_Female_G`（DenysAlmaral）
  - `Driver` → `SM_Chr_Male_01`（Synty Chr）
- **动画驱动**: `ConfigureCharacterAnimator()` 开启 Animator，`TickCharacterAnimators()` 逐帧设置 Speed / Dead 参数
- **当前状态**: 已使用真实 Prefab，但 Synty Bean 角色无骨骼无动画

### 2.2 Stage2 预制体（已生成但需完善）

**路径**: `Assets/_Project/Resources/Stage2/Characters/`

| 预制体 | sourcePrefabPath | 当前状态 |
|---------|-------------------|----------|
| `Stage2_Police.prefab` | `SM_Bean_Cop_01` | 占位几何体，需替换为真实模型 |
| `Stage2_Undercover.prefab` | `casual_Male_G` | 占位几何体，需挂载 DenysAlmaral 模型 |
| `Stage2_Gang.prefab` | `casual_Male_K` | 占位几何体，需挂载 DenysAlmaral 模型 |
| `Stage2_Civilian.prefab` | `SM_Chr_Female_01` | 占位几何体，需挂载 Synty Chr 模型 |

> 这些预制体由 `StageTwoCharacterAssetBuilder.cs` 生成，使用胶囊体/球体/方块作为占位几何体，并写入 `sourcePrefabPath` 记录来源，但**未实际挂载模型 Prefab**。

### 2.3 关键代码文件

| 文件 | 职责 |
|------|------|
| `SocialCharacter.cs` | 角色身份组件，驱动 Animator 参数（Speed/Dead/Action），颜色区分 |
| `OnlineMatchController.CharacterAdapters.cs` | 联机角色适配器，Prefab 路径映射，Animator 配置 |
| `StageTwoCharacterRig.cs` | Stage2 骨骼绑定，7个 Transform 槽位 + 7种姿态 |
| `StageTwoCharacterRigCatalog.cs` | 姿态 Catalog ScriptableObject |
| `StageTwoCharacterAnimationSetup.cs` | Editor 菜单脚本，生成 Animator Controller |
| `StageTwoCharacterAssetBuilder.cs` | Editor 菜单脚本，生成 Stage2 预制体 |

---

## 三、分层替换方案

### 3.1 三层角色方案

```
第1层（主力）: DenysAlmaral CityPeople
  - 含完整骨骼 + walk/jog/idle/phoneTalk 动画
  - 用于: 玩家角色、主要 NPC、警察、卧底、黑帮
  - 数量: 10 个角色 Prefab 可选

第2层（兜底）: Synty SM_Chr
  - 含骨骼，无动画
  - 用于: 次要 NPC、群众角色
  - 需绑定 GanglandCharacter.controller（动画复用第1层）
  - 数量: 2 个角色 Prefab

第3层（极简兜底）: Synty SM_Bean
  - 无骨骼，无动画，仅用 Transform 驱动
  - 用于: 极低端设备 fallback
  - 数量: 4 个角色 Prefab
```

### 3.2 动画参数映射

`SocialCharacter.cs` 和 `OnlineMatchController.cs` 使用的 Animator 参数：

| 参数名 | 类型 | 用途 | 驱动代码 |
|--------|------|------|----------|
| `Speed` | Float | 0=Idle, 0.1~0.7=Walk, 0.7~1.0=Jog | `SetMoveSpeed()` / `TickCharacterAnimators()` |
| `Dead` | Bool | true=播放死亡状态 | `Kill()` / `TickCharacterAnimators()` |
| `Action` | Trigger | 触发特殊动作（交互/举报等） | `TriggerAction()` |

Animator Controller 状态机设计：

```
Entry → Idle (default)
Idle ──[Speed > 0.1]──→ Walk
Walk ──[Speed < 0.1]──→ Idle
Walk ──[Speed > 0.7]──→ Jog
Jog  ──[Speed < 0.7]──→ Walk
AnyState ──[Dead == true]──→ Dead
Dead ──[Dead == false]──→ Idle
AnyState ──[Action triggered]──→ Action
Action ──[Exit Time]──→ Idle
```

---

## 四、执行计划与进度

### ✅ 已完成

- [x] 代码改造：`SocialCharacter.cs` 支持 SkinnedMeshRenderer + Animator 驱动
- [x] 代码改造：`CharacterAdapters.cs` 新增 `ConfigureCharacterAnimator()`
- [x] 代码改造：`OnlinePlayerState` 新增 `CharacterAnimator` 字段
- [x] 代码改造：`OnlineMatchController` 新增 `TickCharacterAnimators()` 逐帧驱动
- [x] Editor 工具：`StageTwoCharacterAnimationSetup.cs` 菜单 `Gangland → Setup Character Animator Controller`
- [x] Editor 工具：`StageTwoCharacterAssetBuilder.cs` 菜单 `Gangland → Build Stage2 Character Prefabs`
- [x] Stage2 预制体：4个占位预制体已生成（Police/Undercover/Gang/Civilian）
- [x] Animator Controller YAML：已写入 `Assets/_Project/Art/Animators/`（需验证）

### ⏳ 待执行（需在 Unity Editor 中操作）

#### 步骤1：生成并验证 Animator Controller

1. 打开 Unity Editor（项目：`/Users/zhugehao/projects/GanglandUndercover`）
2. 菜单栏点击 `Gangland → Setup Character Animator Controller`
3. 验证生成文件：
   - `Assets/_Project/Art/Animators/GanglandCharacter.controller`
   - `Assets/_Project/Art/Animators/GanglandCharacter_Override.controller`
4. 在 Animator 窗口中打开 `GanglandCharacter.controller`，验证：
   - 参数：`Speed`(Float)、`Dead`(Bool)、`Action`(Trigger) ✅
   - 状态：Idle、Walk、Jog、Dead、Action ✅
   - 过渡条件正确 ✅

> ⚠️ 注意：外部脚本生成的 .controller 文件可能 GUID 引用不正确，建议通过 Unity Editor 菜单重新生成。

#### 步骤2：完善 DenysAlmaral 角色 Prefab

为每个主力角色创建带 Animator 的完整 Prefab：

1. 在 Project 窗口找到 `Assets/_Project/Resources/AssetStore/DenysAlmaral/CityPeople/Prefabs/`
2. 对每个角色 Prefab（如 `casual_Male_G`）：
   - 确认 Prefab 上已有 `Animator` 组件
   - 将 `GanglandCharacter.controller` 赋值给 Animator 的 `Controller` 字段
   - 确认 `Avatar` 字段已正确设置（通常自动赋值）
   - Apply Prefab

**优先处理清单**（对应 `FreeCharacterPrefabPath()` 映射）：

| Prefab 路径 | 对应职业 | 操作 |
|-------------|----------|------|
| `city/casual_Male_G` | UndercoverAgent, Forensics | 挂载 GanglandCharacter.controller |
| `downtown/casual_Male_K` | Enforcer | 挂载 GanglandCharacter.controller |
| `city/casual_Female_G` | Fixer | 挂载 GanglandCharacter.controller |
| `professions/police_Female_A` | （警察角色） | 挂载 GanglandCharacter.controller |
| `downtown/casual_Female_K` | （女性 NPC） | 挂载 GanglandCharacter.controller |

#### 步骤3：配置 Synty SM_Chr 角色（第2层兜底）

1. 打开 `Assets/_Project/Resources/AssetStore/Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01.prefab`
2. 添加 `Animator` 组件（如不存在）
3. 赋值 `GanglandCharacter.controller` 到 Controller 字段
4. 配置 Avatar：
   - Synty 角色使用 Humanoid 骨骼
   - 在 Import Settings 中设置 `Rig → Animation Type = Humanoid`
   - Apply
5. 对 `SM_Chr_Female_01` 重复以上操作

#### 步骤4：更新 Stage2 预制体

1. 打开 `Assets/_Project/Resources/Stage2/Characters/Stage2_Undercover.prefab`
2. 删除占位胶囊体，拖入 `casual_Male_G` 作为子 Prefab
3. 确保 `SocialCharacter` 组件在根节点，`Animator` 在子节点（模型 Prefab 上）
4. 对以下预制体重复：
   - `Stage2_Gang.prefab` → `casual_Male_K`
   - `Stage2_Police.prefab` → `police_Female_A`（或 `SM_Bean_Cop_01` 暂代）
   - `Stage2_Civilian.prefab` → `casual_Female_G`（或 `SM_Chr_Female_01`）

#### 步骤5：修改 SocialPrototypeController 使用真实 Prefab

当前 `CreateCharacter()` 方法创建胶囊体，需改为实例化真实 Prefab：

```csharp
// 在 SocialPrototypeController.cs 的 CreateCharacter() 中
// 替换：
//   GameObject characterObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
// 改为：
string prefabPath = GetPrefabPathForRole(role, isPlayer);
GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
GameObject characterObject = Instantiate(prefab, position, Quaternion.identity);
```

需新增 `GetPrefabPathForRole()` 方法，按角色身份分配 Prefab 路径。

#### 步骤6：验证 VerticalSlice 场景

1. 打开 `Assets/_Project/Scenes/Stage1VerticalSlice.unity`
2. 运行场景，观察 `CreateVerticalSliceStreetLife()` 生成的 NPC 模型
3. 确认动画正常播放（Idle → Walk → Jog 过渡）
4. 确认 `SocialCharacter` 组件正确驱动 Animator 参数

---

## 五、文件 GUID 参考

### DenysAlmaral 动画文件 .meta GUID（用于 `AssetDatabase.GUIDToAssetPath`）

| 动画剪辑 | .meta GUID |
|----------|-----------|
| idle_m_2_220f.fbx | `40d3a309d10a3904db79282f9b6d90e3` |
| locom_m_basicWalk_30f.fbx | `6599ecd6d50f5cd488a2a18812b7174c` |
| locom_m_jogging_30f.fbx | `66fd17140cb1f434599cd0c4b33ab1aa` |
| idle_phoneTalking_180f.fbx | `3975160a5f0e2cd47b1bc61842d9c278` |
| idle_f_2_190f.fbx | `0cac3c50a88d6a74782b4161b4b42dc1` |
| locom_f_basicWalk_30f.fbx | `a9ce97153f7113649bd17bbb3de7a81e` |
| locom_f_jogging_30f.fbx | `89b462e947c35b8458ea34cd26887038` |

### 现有 Animator Controller 内部 GUID（用于 YAML 手动编辑）

| 动画状态 | 内部 GUID（男性） |
|----------|-------------------|
| idle_m_2_220f | `40d3a309e9945334284a3a33b46139e7` |
| locom_m_basicWalk_30f | `6599ecd64e0c14740acc7d5f8b82f1f5` |
| locom_m_jogging_30f | `66fd1714b985d8c47806307c1442de96` |
| idle_phoneTalking_180f | `3975160a0bfe0d94098f615c8283969c` |

---

## 六、已知问题与风险

1. **Animator Controller 需 Unity Editor 生成**：外部脚本写入的 .controller YAML 可能内部 GUID 引用不正确，建议通过 Editor 菜单重新生成。

2. **Synty 角色无动画**：SM_Chr 系列有骨骼但无动画剪辑，需复用 DenysAlmaral 的动画（通过 Animator Override Controller 或重定向）。

3. **SocialPrototypeController 仍使用胶囊体**：`CreateCharacter()` 方法需改造以支持真实 Prefab 实例化。

4. **Stage2 预制体未挂载真实模型**：`StageTwoCharacterAssetBuilder.cs` 生成的是占位几何体，需手动替换。

5. **女性动画资源**：部分 DenysAlmaral 角色只有男性动画，需确认 `City F Animator.controller` 中的女性动画 GUID。

---

## 七、下一步行动

1. **立即**: 在 Unity Editor 中执行 `Gangland → Setup Character Animator Controller`，生成正确的 Animator Controller
2. **立即**: 验证 `GanglandCharacter.controller` 的状态和参数配置
3. **后续**: 按「执行计划」步骤2~6逐步替换角色资源
4. **后续**: 改造 `SocialPrototypeController.CreateCharacter()` 支持 Prefab 实例化
5. **后续**: 测试 VerticalSlice 场景中的角色动画表现

---

*文档生成时间: 2026-06-01*  
*生成工具: File Agent (Marvis)*  
*项目路径: `/Users/zhugehao/projects/GanglandUndercover`*

---

## 八、阶段完成标记（2026-06-01）

### 8.1 已完成任务

| # | 任务 | 状态 |
|---|------|------|
| 1 | DenysAlmaral 5个主力角色 Animator Controller 挂载 | ✅ 通过 YAML 编辑完成 |
| 2 | Synty SM_Chr_Male_01 / Female_01 添加 Animator | ✅ 已添加，挂载 GanglandCharacter.controller |
| 2b | Characters.fbx Rig 设置 | ✅ 已是 Humanoid（animationType: 3），无需修改 |
| 3 | Stage2 4个预制体挂载真实模型 | ✅ PrefabInstance 已添加，sourcePrefabPath 已更新 |
| 4 | YAML 格式验证 | ✅ 11个文件全部通过 |

### 8.2 修改文件清单

**Animator Controller 挂载（5个）**:
- `city/casual_Male_G.prefab`
- `city/casual_Female_G.prefab`
- `downtown/casual_Male_K.prefab`
- `downtown/casual_Female_K.prefab`
- `professions/police_Female_A.prefab`

**Synty Animator 添加（2个）**:
- `SM_Chr_Male_01.prefab` — 新增 Animator 组件 (fileID: 9900000000000001)
- `SM_Chr_Female_01.prefab` — 新增 Animator 组件 (fileID: 9900000000000002)

**Stage2 预制体更新（4个）**:
- `Stage2_Undercover.prefab` → casual_Male_G (PrefabInstance fileID: 8000000000000001)
- `Stage2_Gang.prefab` → casual_Male_K (PrefabInstance fileID: 8000000000000003)
- `Stage2_Police.prefab` → police_Female_A + sourcePrefabPath 已更正 (8000000000000005)
- `Stage2_Civilian.prefab` → casual_Female_G + sourcePrefabPath 已更正 (8000000000000007)

### 8.3 遗留事项

- Unity Editor 编译日志未生成（项目尚未在 Editor 中打开编译验证）
- Characters.fbx Rig 已是 Humanoid 无需修改
- Stage2 占位几何体保留以维护 StageTwoCharacterRig 的内部引用，真实模型通过 PrefabInstance 嵌套挂载
*（内容由AI生成，仅供参考）*
