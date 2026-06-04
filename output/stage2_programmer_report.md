---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_b19ef94e5d9e11f1a4f35254002afed2
    ReservedCode1: coQkRiQfDqHQTxaS1mnxNN3S5FapkJPLylQ/J8zteQGs/F9Xh26xpt148EQfD3pUq3hXYWVL7TexFnuzzjEWUuDcW+8h8S+cO4XyDBc8zo7qAksluodEPWREZtWVXPDOEoCDPTLWzcXXJfLFPLUpE7L+EYyz8Iv/w7PrGyTU6eBQUzd/qfoGXhdkgnA=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_b19ef94e5d9e11f1a4f35254002afed2
    ReservedCode2: coQkRiQfDqHQTxaS1mnxNN3S5FapkJPLylQ/J8zteQGs/F9Xh26xpt148EQfD3pUq3hXYWVL7TexFnuzzjEWUuDcW+8h8S+cO4XyDBc8zo7qAksluodEPWREZtWVXPDOEoCDPTLWzcXXJfLFPLUpE7L+EYyz8Iv/w7PrGyTU6eBQUzd/qfoGXhdkgnA=
---

# GanglandUndercover Stage 2 代码审查报告

**检查日期**: 2026-06-01 17:40
**编译日志**: `~/Library/Logs/Unity/Editor.log` (最后编译: 2026-06-01 17:36)

---

## 1. 编译状态

**结果: 通过 ✅**

```
*** Tundra build success (2.58 seconds), 6 items updated, 1076 evaluated
ExitCode: 0
```

最后一次编译 (17:36) 无 C# 编译错误，Assembly-CSharp.dll 正常生成。Domain Reload 成功，脚本总数 1578 个。

### 1.1 资源导入警告（非编译错误）

| 文件 | 问题 | 严重性 |
|------|------|--------|
| `GanglandCharacter_Override.controller` | Type mismatch: expected `PreviewAnimationClip`, found `AnimatorOverrideController`; GUID extraction failed at line 13 | ⚠️ 中 |
| `GanglandCharacter.controller` | FileID overflow: `-9990000000000000001` at line 286; Broken text PPtr | ⚠️ 中 |

---

## 2. SocialPrototypeController.CreateCharacter() 完整性检查

### 2.1 GetPrefabPathForRole() 覆盖度

| SocialRole 枚举值 | Prefab 路径 | 路径存在 | Resources.Load 可用 |
|-------------------|-------------|---------|---------------------|
| `SocialRole.Police` | `AssetStore/Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Cop_01` | ✅ | ✅ |
| `SocialRole.Undercover` | `AssetStore/DenysAlmaral/CityPeople/Prefabs/city/casual_Male_G` | ✅ | ✅ |
| `SocialRole.Gang` | `AssetStore/DenysAlmaral/CityPeople/Prefabs/downtown/casual_Male_K` | ✅ | ✅ |
| `default` | `AssetStore/Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01` | ✅ | ✅ |

**结论**: `SocialRole` 枚举含 3 个值 (`Gang`, `Police`, `Undercover`)，全部被 `GetPrefabPathForRole()` 覆盖。所有 Prefab 路径以 `AssetStore/` 开头，均位于 `Assets/_Project/Resources/AssetStore/` 下，符合 `Resources.Load` 要求。

### 2.2 Resources 路径验证

```
Assets/_Project/Resources/
  AssetStore/
    Synty/PolygonStarter/Prefabs/Characters/
      SM_Bean_Cop_01.prefab        ← Police 角色
      SM_Chr_Male_01.prefab        ← default 回退
    DenysAlmaral/CityPeople/Prefabs/
      city/casual_Male_G.prefab    ← Undercover 角色
      downtown/casual_Male_K.prefab ← Gang 角色
```

### 2.3 CreateCharacter() 方法完整性

| 步骤 | 状态 |
|------|------|
| `Resources.Load<GameObject>(prefabPath)` 加载 Prefab | ✅ |
| Prefab 为 null 时回退到 `Capsule` 基元体 | ✅ |
| 设置 `characterObject.name` | ✅ |
| 添加到 `generatedObjects` 列表 | ✅ |
| 设置位置 `(position.x, position.y, CharacterZ)` | ✅ |
| 设置缩放 `(0.42, 0.42, 0.82)` | ✅ |
| 设置旋转 `(90, 0, 0)` | ✅ |
| 无 SkinnedMeshRenderer 时着色 | ✅ |
| 创建阴影 Cylinder | ✅ |
| 创建 TextMesh 标签 | ✅ |
| `AddComponent<SocialCharacter>()` 并调用 `Bind()` | ✅ |
| 添加到 `characters` 列表 | ✅ |

**结论**: CreateCharacter() 完整，含完整的 fallback 逻辑。

---

## 3. SocialCharacter.cs 完整性检查

### 3.1 Animator Hash 常量

```csharp
private static readonly int AnimSpeedHash  = Animator.StringToHash("Speed");   // ✅
private static readonly int AnimDeadHash   = Animator.StringToHash("Dead");    // ✅
private static readonly int AnimActionHash = Animator.StringToHash("Action");  // ✅
```

### 3.2 方法完整性

| 方法 | 功能 | 状态 |
|------|------|------|
| `Bind()` | 绑定角色属性，获取 Animator/Renderers，初始化材质 + 着色 | ✅ |
| `SetMoveSpeed(float)` | `animator.SetFloat(AnimSpeedHash, speed)` | ✅ |
| `TriggerAction()` | `animator.SetTrigger(AnimActionHash)` | ✅ |
| `Kill()` | 设置 `IsAlive=false`，`animator.SetBool(AnimDeadHash, true)` | ✅ |
| `RefreshVisual()` | 刷新材质颜色和标签 | ✅ |
| `OnDestroy()` | 清理 Materials | ✅ |

### 3.3 Animator 驱动情况

| 场景 | Speed | Dead | Action |
|------|-------|------|--------|
| SocialPrototypeController | **不使用 Animator**（直接 transform.position 移动） | 不使用 | 不使用 |
| OnlineMatchController | `TickCharacterAnimators()` 驱动 | `TickCharacterAnimators()` 驱动 | **未调用** ⚠️ |

**发现**: `SocialCharacter.SetMoveSpeed()` 和 `TriggerAction()` 方法定义完整，但**在整个项目中未被任何调用方使用**。OnlineMatchController 的 `TickCharacterAnimators()` 直接操作 `state.CharacterAnimator`（通过 `CharacterAdapters.ConfigureCharacterAnimator` 绑定的 Animator），而非通过 `SocialCharacter` 的方法。

**建议**: 这可能是为后续 Stage 预留的接口。当前不影响运行时正确性，但如需统一驱动方式，应在 OnlineMatchController 中将直接操作 Animator 改为调用 `SocialCharacter` 方法。

---

## 4. OnlineMatchController.TickCharacterAnimators() 调用位置

### 4.1 调用链

```
LateUpdate()                          (line 789)
  └─ TickCharacterAnimators()         (line 796)
       ├─ SetBool("Dead", !Alive)     (line 812)
       └─ SetFloat("Speed", input)    (line 816)
```

### 4.2 Animator 绑定链

```
CreateFreeCharacterAdapter()                                      (CharacterAdapters.cs:7)
  └─ ConfigureCharacterAnimator(model, state)                     (CharacterAdapters.cs:63)
       └─ animator = model.GetComponentInChildren<Animator>()      (CharacterAdapters.cs:65)
       └─ state.CharacterAnimator = animator                      (CharacterAdapters.cs:75)
```

### 4.3 评估

- `TickCharacterAnimators()` 在 `LateUpdate()` 中调用，位置正确 ✅
- 每帧遍历所有玩家状态，检查 `CharacterAnimator != null` ✅
- 仅处理 Dead 和 Speed 两个参数 ✅
- **缺失**: 没有处理 `Action` 触发器。当角色执行特殊动作（如击杀、破坏）时，应触发 `SetTrigger("Action")`。搜索整个 Online 目录未找到对 `Action` trigger 的调用。

---

## 5. Animator Controller 资源问题

### 5.1 GanglandCharacter.controller

该文件包含 5 个动画状态（Idle, Walk, Jog, Dead, Action），但使用的人工 FileID 超出 Unity 解析范围：

| 状态 | FileID | Unity 解析 |
|------|--------|-----------|
| Idle | `-8880000000000000000` | ✅ |
| Walk | `-7770000000000000000` | ✅ |
| Jog | `-6660000000000000000` | ✅ |
| Dead | `-5550000000000000000` | ✅ |
| Action | `-4440000000000000000` | ✅ |
| **StateMachine** | **`-9990000000000000001`** | **❌ Overflow** |

动画剪辑引用使用内部 GUID（如 `40d3a309e9945334284a3a33b46139e7`），这些 GUID 与 Unity .meta 文件中的 GUID 不同，属于 `.controller` 内部的动画引用。外部脚本写入时无法保证 GUID 正确性。

### 5.2 GanglandCharacter_Override.controller

```yaml
m_RuntimeAnimatorController: {fileID: 9100000, guid: 00000000000000000000000000000000, type: 2}
```

`guid` 字段为全零值（无效），且 `m_Clips: []` 为空，表明这是一个未配置的 Override Controller 骨架。

### 5.3 修复建议

两个 Animator Controller 均**必须通过 Unity Editor 菜单脚本重新生成**，无法通过外部文本编辑修复：

1. **GanglandCharacter.controller**: 在 Unity Editor 中创建 AnimatorController，拖入动画剪辑，由 Unity 自动分配正确的 FileID 和内部 GUID。
2. **GanglandCharacter_Override.controller**: 在 Unity Editor 中创建 AnimatorOverrideController，设置正确的 RuntimeAnimatorController 引用。

---

## 6. 总结

| 检查项 | 结果 | 备注 |
|--------|------|------|
| C# 编译 | ✅ 通过 | ExitCode 0, 无编译错误 |
| GetPrefabPathForRole 覆盖度 | ✅ 完整 | 3/3 SocialRole 值均覆盖 |
| Prefab 路径 Resources 兼容 | ✅ | 均在 `_Project/Resources/` 下 |
| CreateCharacter 完整性 | ✅ | 含完整 fallback |
| SocialCharacter.SetMoveSpeed | ✅ 方法定义完整 | ⚠️ 未被调用 |
| SocialCharacter.TriggerAction | ✅ 方法定义完整 | ⚠️ 未被调用 |
| TickCharacterAnimators 位置 | ✅ | LateUpdate 中正确 |
| Action Trigger 调用 | ⚠️ 缺失 | 无代码触发 "Action" 参数 |
| GanglandCharacter.controller | ❌ 需重生 | FileID 溢出 + GUID 不确定 |
| GanglandCharacter_Override.controller | ❌ 需重生 | 无效 GUID + 空配置 |

### 待修复项（优先级排序）

1. **[P0]** 在 Unity Editor 中重新生成 `GanglandCharacter.controller`，正确配置 Idle/Walk/Jog/Dead/Action 状态和过渡。
2. **[P0]** 在 Unity Editor 中重新生成 `GanglandCharacter_Override.controller`，绑定正确的 RuntimeAnimatorController。
3. **[P1]** 在 OnlineMatchController 中补充 "Action" trigger 的调用（角色执行击杀/破坏等特殊动作时触发 `SetTrigger("Action")`）。
*（内容由AI生成，仅供参考）*
