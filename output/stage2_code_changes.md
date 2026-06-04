# Stage2 代码改造报告

> 日期：2026-06-01  
> 项目：GanglandUndercover 第2阶段  
> 产出类型：代码改造 + 分析文档

---

## 一、改造文件清单

| 文件 | 操作 | 状态 |
|------|------|------|
| `Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs` | 改造 | ✅ 已完成 |
| `Assets/_Project/Scripts/Online/OnlineMatchController.CharacterAdapters.cs` | 审查 | ✅ 无需改 |
| 产出文档 `output/stage2_code_changes.md` | 新建 | ✅ 已完成 |

---

## 二、任务1：SocialPrototypeController.CreateCharacter() 改造

### 改动位置
`Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs`

### 改动说明

#### 2.1 新增方法：GetPrefabPathForRole()

```csharp
private static string GetPrefabPathForRole(SocialRole role, bool isPlayer)
```

按 SocialRole 角色的 enum 值返回对应的 Asset Store 角色 Prefab 路径（相对于 Resources 目录），用于 `Resources.Load<GameObject>()`。

| SocialRole | 映射 Prefab | 来源资源包 |
|------------|-----------|-----------|
| `Police` | `AssetStore/Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Cop_01` | Synty PolygonStarter |
| `Undercover` | `AssetStore/DenysAlmaral/CityPeople/Prefabs/city/casual_Male_G` | DenysAlmaral CityPeople |
| `Gang` | `AssetStore/DenysAlmaral/CityPeople/Prefabs/downtown/casual_Male_K` | DenysAlmaral CityPeople |
| `default` | `AssetStore/Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01` | Synty PolygonStarter |

#### 2.2 修改方法：CreateCharacter()

**改造前**：
- 无条件使用 `GameObject.CreatePrimitive(PrimitiveType.Capsule)` 创建胶囊体
- 无真实模型、无动画

**改造后**：
- 先调用 `GetPrefabPathForRole()` 获取 Prefab 路径
- 通过 `Resources.Load<GameObject>()` 尝试加载真实角色 Prefab
- **成功加载 Prefab**：用 `Instantiate(prefab)` 实例化，设置位置/缩放/旋转
  - 检查 `GetComponentInChildren<SkinnedMeshRenderer>()` 
  - 有 SkinnedMeshRenderer 的真实模型：不再覆盖颜色（保留材质原色）
  - 无 SkinnedMeshRenderer：降级使用 SetColor 上色（颜色逻辑见下表）
- **Prefab 加载失败**：回退到原有胶囊体创建逻辑 + 角色颜色区分
- 阴影（Cylinder）和名字标签（TextMesh）保持原有逻辑不变

#### 2.3 降级颜色表

| SocialRole | 降级颜色 | 色值 |
|-----------|---------|------|
| Gang | 暗红色 | (0.72, 0.22, 0.16) |
| Police | 蓝色 | (0.22, 0.36, 0.72) |
| Undercover | 灰蓝色 | (0.5, 0.5, 0.55) |

#### 2.4 设计决策说明

`GetPrefabPathForRole` 参数使用 `SocialRole` 而非任务原始描述中的 `OnlineRole`，原因：
- `SocialPrototypeController` 属于 `GanglandUndercover.SocialDeduction` 命名空间，角色系统使用 `SocialRole` 枚举（Police / Undercover / Gang）
- `OnlineRole / OnlineProfession` 属于 `GanglandUndercover.Online` 命名空间，用于联机模式
- 两套系统的映射关系如下表：

| SocialRole（原型模式） | 对应 OnlineProfession | Prefab |
|----------------------|---------------------|--------|
| Police | Inspector / Tech | SM_Bean_Cop_01 |
| Undercover | UndercoverAgent | casual_Male_G |
| Gang | Enforcer | casual_Male_K |

---

## 三、任务2：OnlineMatchController.CharacterAdapters.cs 路径审查

### 文件
`Assets/_Project/Scripts/Online/OnlineMatchController.CharacterAdapters.cs`

### FreeCharacterPrefabPath() 审查结果

已逐一验证 `AssetStoreResourceRoot` 下的所有 Prefab 路径，全部正确指向实际存在的 Prefab 文件：

| OnlineProfession | 路径 | 文件系统验证 |
|------------------|------|------------|
| Inspector | `Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Cop_01` | ✅ 存在 |
| Tech | `Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Cop_01` | ✅ 存在 |
| Forensics | `Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Female_01` | ✅ 存在 |
| UndercoverAgent | `DenysAlmaral/CityPeople/Prefabs/city/casual_Male_G` | ✅ 存在 |
| Enforcer | `DenysAlmaral/CityPeople/Prefabs/downtown/casual_Male_K` | ✅ 存在 |
| Fixer | `DenysAlmaral/CityPeople/Prefabs/city/casual_Female_G` | ✅ 存在 |
| Driver | `Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01` | ✅ 存在 |
| default | `Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Female_01` | ✅ 存在 |

**结论**：无需任何修改。

---

## 四、任务3：Stage2 预制体分析

### 4.1 预制体清单

`Assets/_Project/Resources/Stage2/Characters/` 下共 4 个预制体：

| 预制体 | rigKey | sourcePrefabPath | 预期角色模型 |
|--------|--------|------------------|------------|
| `Stage2_Civilian.prefab` | civilian | Synty/SM_Chr_Female_01 | 平民女性 |
| `Stage2_Gang.prefab` | gang | DenysAlmaral/casual_Male_K | 帮派男性 |
| `Stage2_Police.prefab` | police | Synty/SM_Bean_Cop_01 | 警察 |
| `Stage2_Undercover.prefab` | undercover | DenysAlmaral/casual_Male_G | 卧底男性 |

### 4.2 占位几何体结构（4个预制体完全一致）

所有 4 个预制体共享相同的层级结构和 Unity 内建几何体网格（Built-in Meshes），通过不同尺寸/位置的组合拼出人形：

```
Root (Transform + StageTwoCharacterRig)
├── BodyRoot         [Cylinder, 网格ID 10208]  旋转90°  位置(0, -0.04, 0.30)  比例(0.28, 0.28, 0.58)
│   └── RoleAccent   [Cube, 网格ID 10202]      位置(0, 0.22, 0.10)           比例(0.18, 0.035, 0.08)
├── HeadRoot         [Sphere, 网格ID 10207]    位置(0.03, 0.32, 0.58)       比例(0.28, 0.24, 0.24)
│   └── FaceStrip    [Cube, 网格ID 10202]      位置(0.08, 0.12, 0.04)       比例(0.22, 0.035, 0.08)
├── LeftArm          [Cylinder, 网格ID 10208]  旋转±12°  位置(-0.24, -0.04, 0.34)  比例(0.07, 0.07, 0.30)
├── RightArm         [Cylinder, 网格ID 10208]  旋转±12°  位置(0.24, -0.04, 0.34)   比例(0.07, 0.07, 0.30)
├── LeftFoot         [Cube, 网格ID 10202]      位置(-0.10, -0.46, 0.14)     比例(0.14, 0.16, 0.08)
├── RightFoot        [Cube, 网格ID 10202]      位置(0.10, -0.46, 0.14)      比例(0.14, 0.16, 0.08)
└── StateRoot        [空Transform]
    └── Prefab State Ring [Torus, 网格ID 10206]  旋转90°  位置(0, -0.42, -0.12)  比例(0.34, 0.025, 0.22)
```

### 4.3 几何体材质状态

所有 MeshRenderer 的 `m_Materials` 均为空数组 `[]`，渲染时显示为粉红色（Unity默认缺失材质色），无实际材质赋值。

### 4.4 StageTwoCharacterRig 组件

每个预制体根节点挂载 `StageTwoCharacterRig`（GUID: `ca98bb79cf1344967b3dc75569867514`）：

| 字段 | 说明 | 每个预制体的值 |
|------|------|-------------|
| `rigKey` | 角色标识 | civilian / gang / police / undercover |
| `sourcePrefabPath` | 源 Prefab 路径 | 见 4.1 表 |
| `currentState` | 当前状态 | 全部为 0 |
| `BodyRoot` / `HeadRoot` 等 | Transform 引用 | 指向各自预制体内的对应子节点 |

### 4.5 改造方向

Stage2 预制体当前为**纯占位几何体**（零真实模型），但已有 `sourcePrefabPath` 指向正确的 Asset Store Prefab。后续改造方向：

1. **运行时替换**：Stage2 系统应在运行时通过 `sourcePrefabPath` 加载真实 Prefab，替换占位几何体子节点
2. **Editor 工具**：可编写 Editor 脚本，在 Unity Editor 中自动将 `sourcePrefabPath` 指向的 Prefab 的 SkinnedMeshRenderer / Animator 组件复制到 Stage2 预制体中
3. **动画系统**：真实 Prefab（Synty/DenysAlmaral）均自带 Humanoid Avatar 和 Animator，可与 Mecanim 动画状态机集成

---

## 五、角色映射总表

| 系统 | 角色 | Prefab | 资源包 |
|------|------|--------|--------|
| SocialDeduction | Police | SM_Bean_Cop_01 | Synty PolygonStarter |
| SocialDeduction | Undercover | casual_Male_G | DenysAlmaral CityPeople |
| SocialDeduction | Gang | casual_Male_K | DenysAlmaral CityPeople |
| Online | Inspector | SM_Bean_Cop_01 | Synty PolygonStarter |
| Online | Tech | SM_Bean_Cop_01 | Synty PolygonStarter |
| Online | Forensics | SM_Chr_Female_01 | Synty PolygonStarter |
| Online | UndercoverAgent | casual_Male_G | DenysAlmaral CityPeople |
| Online | Enforcer | casual_Male_K | DenysAlmaral CityPeople |
| Online | Fixer | casual_Female_G | DenysAlmaral CityPeople |
| Online | Driver | SM_Chr_Male_01 | Synty PolygonStarter |
| Stage2 Rigs | civilian | SM_Chr_Female_01 | Synty PolygonStarter |
| Stage2 Rigs | gang | casual_Male_K | DenysAlmaral CityPeople |
| Stage2 Rigs | police | SM_Bean_Cop_01 | Synty PolygonStarter |
| Stage2 Rigs | undercover | casual_Male_G | DenysAlmaral CityPeople |

---

## 六、待手动处理清单

| # | 描述 | 优先级 | 需 Editor |
|---|------|--------|----------|
| 1 | **Stage2 预制体运行时替换逻辑**：实现 Stage2 系统在运行时通过 StageTwoCharacterRig.sourcePrefabPath 加载真实 Prefab 并替换占位几何体子节点的代码 | 🔴 高 | 否 |
| 2 | **Stage2 角色动画挂接**：加载真实 Prefab 后，将 Animator 组件连接到角色的动画状态机（Idle/Walk/Run/Death 等），配置 Avatar Mask 和 AnimatorController | 🔴 高 | 需 AnimatorController |
| 3 | **角色材质颜色调整**：DenysAlmaral 角色加载后需要像 Online 模式一样通过 TintCharacterAdapter 做颜色区分（帮派红色、警察蓝色等），目前 SocialPrototypeController 中对有 SkinnedMeshRenderer 的模型跳过了颜色处理 | 🟡 中 | 否 |
| 4 | **SocialPrototypeController 角色缩放适配**：真实 Prefab 实例化后使用固定 scale(0.42, 0.42, 0.82)，可能需要像 Online 模式的 FitCharacterAdapterToPlayer 一样做自适应缩放 | 🟡 中 | 否 |
| 5 | **Stage2 预制体材质赋值**：当前 4 个 Stage2 预制体的 MeshRenderer 材质为空，如果降级展示需要给占位几何体赋默认材质（如 Standard shader + 角色颜色） | 🟢 低 | 是 |
| 6 | **Online 模式角色差异**：Forensics 当前使用 SM_Chr_Female_01（女性），与任务建议的 casual_Male_G（男性）不一致；若需统一为男性请在 FreeCharacterPrefabPath 中修改 | 🟢 低 | 否 |
