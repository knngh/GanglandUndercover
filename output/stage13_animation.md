---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_16b84b265e5e11f1bd025254006c9bbf
    ReservedCode1: 8aWRKLYQ/EJZ0Wua6Xr34lTcfXAGmK6QdPJ/qkTGvlLaaGRMs2xAONG2eT2kSc99nvqqX6b5gcsJGducXwvU334V9SroNbZcWd6w42lt7wlnv9b12OiXx2YGFmGOtgOyId7cFtIiDdmSiomBNgG2NmPLLSQGrwrpglzNZAirPf6hDQqEJT37bLtZ0jo=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_16b84b265e5e11f1bd025254006c9bbf
    ReservedCode2: 8aWRKLYQ/EJZ0Wua6Xr34lTcfXAGmK6QdPJ/qkTGvlLaaGRMs2xAONG2eT2kSc99nvqqX6b5gcsJGducXwvU334V9SroNbZcWd6w42lt7wlnv9b12OiXx2YGFmGOtgOyId7cFtIiDdmSiomBNgG2NmPLLSQGrwrpglzNZAirPf6hDQqEJT37bLtZ0jo=
---

# Stage 13 — 角色动画系统完善

## 执行时间
2026-06-02

## 任务目标
完善角色动画系统，使 Idle / Walk / Jog / Dead / Action 五个动画状态被充分利用，并补全击杀动画、尸体视觉效果。

---

## 一、GanglandCharacter.controller 分析

**文件路径**：`Assets/_Project/Art/Animators/GanglandCharacter.controller`

**Animator 参数**：
| 参数名 | 类型 | 默认值 | 用途 |
|---------|------|--------|------|
| Speed | Float | 0 | 驱动 Idle→Walk→Jog 混合树 |
| Dead | Bool | false | AnyState→Dead 过渡条件 |
| Action | Trigger | — | AnyState→Action 触发条件 |

**状态转换规则**：
- `Idle → Walk`：Speed > 0.1
- `Walk → Idle`：Speed < 0.1
- `Walk → Jog`：Speed > 0.7
- `Jog → Walk`：Speed < 0.7
- `AnyState → Dead`：Dead = true
- `AnyState → Action`：Action Trigger 触发（瞬时，持续时长 0）
- `Action → Idle`：ExitTime = 1.0（约 1 秒后自动退出）
- `Dead → Idle`：Dead = false（复活时）

---

## 二、AnimationController.cs（新建）

**文件路径**：`Assets/_Project/Scripts/SocialDeduction/AnimationController.cs`

**职责**：作为角色动画的底层驱动组件，挂载在每个角色 GameObject 上。

**核心功能**：

### 1. 移动速度自动追踪
- `Update()` 中通过 `Vector3.Distance(transform.position, lastPosition)` 计算帧间位移
- 按阈值映射为 Speed 参数：
  - `< 0.1 m/s` → `Speed = 0`（Idle）
  - `0.1 ~ 2.0 m/s` → `Speed = 0.5`（Walk）
  - `> 2.0 m/s` → `Speed = 1`（Jog）
- 覆盖 Online 模式下 `OnlineMatchController.TickCharacterAnimators()` 已设置的 Speed 值（每帧覆盖，无冲突）

### 2. Action 触发
- `TriggerAction()`：设置 `Action` Trigger，播放约 1 秒的击杀动作后自动回到 Idle

### 3. 死亡倒地动画序列
- `PlayDeathSequence(duration, onComplete)`：协程
  - 设置 `Dead = true` 进入 Dead 状态
  - 等待 `duration` 秒（默认 1.5 秒，覆盖倒地动画时长）
  - 回调 `onComplete`（用于生成尸体 BodyVisual / BodyMarker）

### 4. 通风管瞬移黑屏过渡
- `PlayVentTransition(destination, onMidTeleport, totalDuration)`：协程
  - 前 50% 时间：alpha 从 0 → 1（渐入黑屏）
  - 中点：执行 `onMidTeleport` 回调（实际传送逻辑）
  - 后 50% 时间：alpha 从 1 → 0（淡出黑屏）
- 通过 `SetBlackoutCallback(Action<float>)` 注入黑屏控制委托

---

## 三、SocialCharacter.cs（修改）

**文件路径**：`Assets/_Project/Scripts/SocialDeduction/SocialCharacter.cs`

### 修改内容

#### 1. 新增字段
```csharp
private AnimationController animController;
public event Action<SocialCharacter> DeathAnimationComplete;
```

#### 2. Bind / BindForPrefab / BindAnimator 中初始化 AnimationController
三个初始化入口均增加：
```csharp
animController = GetComponent<AnimationController>();
if (animController == null)
    animController = gameObject.AddComponent<AnimationController>();
animController.Bind(animator);
```

#### 3. Kill() 方法增强
原逻辑：直接设置 `Dead = true` + `RefreshVisual()`

新逻辑：
```csharp
IsAlive = false;
animator.SetBool(AnimDeadHash, true);
// 通过 AnimationController 播放死亡倒地动画序列
animController.PlayDeathSequence(1.5f, () => DeathAnimationComplete?.Invoke(this));
RefreshVisual();
```
死亡动画播放 1.5 秒后，才触发 `DeathAnimationComplete` 事件（由 `SocialPrototypeController` 或 `OnlineMatchController` 订阅，生成尸体）。

#### 4. 新增通风管过渡接口
```csharp
public void SetBlackoutCallback(Action<float> callback)
public void PlayVentTransition(Vector3 destination, Action onMidTeleport)
```
委托给 `animController` 实现，供 `VentSystem` 或外部调用。

---

## 四、KillSystem.cs（修改）

**文件路径**：`Assets/_Project/Scripts/Online/KillSystem.cs`

### 修改内容

#### 1. PlayKillEffects 方法签名变更
```csharp
// 修改前
private void PlayKillEffects(Vector3 victimPos)

// 修改后
private void PlayKillEffects(Vector3 victimPos, ulong victimClientId)
```

#### 2. 击杀时触发双方动画
在 `PlayKillEffects` 开头新增：
```csharp
// 攻击者：触发 Action 动画（击杀动作）
ulong localId = controller.LocalClientIdValue;
if (controller.Players.TryGetValue(localId, out var attackerState))
    attackerState.SocialChar?.TriggerAction();

// 受害者：触发死亡动画序列
if (controller.Players.TryGetValue(victimClientId, out var victimState))
    victimState.SocialChar?.Kill();
```

#### 3. OnKillButtonClicked 调用更新
```csharp
// 修改前
PlayKillEffects(victimPos);

// 修改后
PlayKillEffects(victimPos, victimClientId);
```

---

## 五、BodyVisual.cs（新建）

**文件路径**：`Assets/_Project/Scripts/SocialDeduction/BodyVisual.cs`

**职责**：替代 `SocialPrototypeController.KillCharacter()` 中简陋的立方体尸体，提供完整的尸体视觉效果。

### 核心功能

#### 1. 半透明轮廓（LineRenderer 圆环）
- 在尸体位置生成 `outlineRadius = 0.6m` 的彩色圆环
- 使用阵营色（`factionColor`，alpha = 0.55）
- 脉冲呼吸动画：`alpha` 在 `0.25 ~ baseAlpha` 之间正弦波动

#### 2. 阵营色点光源标记
- `Light`（Point 类型），颜色同阵营色，强度 0.6，范围 2.5m
- 同样参与脉冲呼吸动画（强度 0.3 ~ 0.9）

#### 3. 3D 世界空间报告按钮
- `World Space Canvas`，悬浮于尸体上方 `buttonHeight = 1.4m`
- 按钮颜色：`buttonColor = (0.72, 0.12, 0.08, 0.88)`（暗红半透明）
- 按钮文字：`"报告尸体：{受害者名称}"`
- 点击触发 `onReport` 回调（由 `KillSystem.CreateReportButton` 或等效逻辑注入）

#### 4. 阵营色映射
| 阵营 | SocialRole | 颜色 |
|------|-------------|------|
| 黑帮 | Gang | 红 `(0.85, 0.15, 0.12, 0.55)` |
| 警察 | Police | 蓝 `(0.15, 0.35, 0.82, 0.55)` |
| 内鬼 | Mole | 青 `(0.18, 0.58, 0.52, 0.55)` |
| 卧底 | Undercover | 黄 `(0.88, 0.66, 0.22, 0.55)` |

---

## 六、集成说明

### Online 模式
`OnlineMatchController.TickCharacterAnimators()` 每帧调用 `SocialCharacter.SetMoveSpeed(speed)`，同时 `AnimationController.Update()` 也计算速度并设置 Speed 参数——两者每帧互相覆盖，无冲突。

击杀流程：
1. 玩家点击击杀按钮 → `KillSystem.OnKillButtonClicked()`
2. `PlayKillEffects` 中触发攻击者 `TriggerAction()` + 受害者 `Kill()`
3. 受害者 `SocialCharacter.Kill()` → `AnimationController.PlayDeathSequence(1.5f)` → 1.5 秒后回调 `DeathAnimationComplete`
4. `OnlineMatchController` 订阅该事件，生成 `BodyVisual` + `BodyMarker`

### Offline 模式（SocialPrototypeController）
`MovePlayer()` / `MoveBots()` 直接设置 `transform.position`，未调用 `SetMoveSpeed`。`AnimationController.Update()` 通过位置差自动计算速度，驱动 Walk / Jog 动画。

击杀流程：
1. `SocialPrototypeController.KillCharacter()` 调用 `SocialChar.Kill()`
2. 死亡动画 1.5 秒后回调，生成 `BodyVisual`

---

## 七、文件清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `Assets/_Project/Scripts/SocialDeduction/AnimationController.cs` | 新建 | 角色动画驱动组件 |
| `Assets/_Project/Scripts/SocialDeduction/SocialCharacter.cs` | 修改 | 集成 AnimationController，增强 Kill() |
| `Assets/_Project/Scripts/Online/KillSystem.cs` | 修改 | 击杀时触发双方动画 |
| `Assets/_Project/Scripts/SocialDeduction/BodyVisual.cs` | 新建 | 尸体视觉效果（轮廓+阵营色+报告按钮） |

---

## 八、待完成 / 已知限制

1. **BodyVisual 与现有 KillSystem.CreateReportButton 的去重**：当前 `KillSystem` 已有 `CreateReportButton()` 生成世界空间报告按钮；`BodyVisual` 也生成报告按钮。需要决定保留哪一套，或让 `BodyVisual` 完全替代 `CreateReportButton`。
2. **VentSystem 与 AnimationController 的集成**：`VentSystem` 已有黑屏过渡实现（`onSetBlackoutAlpha` 回调），需要将 `AnimationController.PlayVentTransition` 接入 `VentSystem.EnterVent` / `ExitVent` 流程。
3. **Offline 模式 DeathAnimationComplete 订阅**：`SocialPrototypeController` 需要订阅 `SocialCharacter.DeathAnimationComplete` 事件以生成 `BodyVisual`，当前代码中 `KillCharacter()` 直接生成简陋立方体，需要更新。
4. **Online 模式 DeathAnimationComplete 订阅**：`OnlineMatchController` 需要在玩家死亡时订阅该事件，当前通过 `PlayerAliveChanged` 事件处理，需要确认是否接入 `DeathAnimationComplete`。

---

## 九、测试建议

1. **Offline 模式**：运行 `SocialPrototypeController`，移动角色确认 Walk / Jog 动画正确切换；击杀角色确认死亡倒地动画播放 1.5 秒后生成尸体。
2. **Online 模式**：Host + Client，击杀时确认攻击者播放 Action 动画、受害者播放死亡倒地动画。
3. **通风管**：进入/离开通风管确认黑屏过渡效果。
4. **BodyVisual**：确认轮廓颜色、脉冲动画、报告按钮功能正常。
*（内容由AI生成，仅供参考）*
