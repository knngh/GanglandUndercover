# Stage 15 编译错误修复报告

**日期**：2026-06-03  
**项目**：GanglandUndercover  
**范围**：`Assets/_Project/Scripts/SocialDeduction/` 目录下的 Stage 15 新增/修改文件

---

## 诊断方法

由于 Unity Editor 已运行无法启动 batch mode 编译，采用**全量静态分析**方法：

1. 并行读取全部 5 个 Stage 15 核心文件的完整内容（`EnvironmentManager.cs` / `BuildingBuilder.cs` / `StreetFurniture.cs` / `MaterialFactory.cs` / `StreetProps.cs`）
2. 构建项目类型数据库（simple name → full namespace），校验跨命名空间类型引用可达性
3. 逐对检查所有方法调用与定义的参数数量、类型、返回值匹配
4. 扫描括号匹配、分号终结符、枚举成员完整性等语法约束

## 确认的编译错误（2 个）

### 错误 1：`BuildingBuilder.SetMaterial` 未定义（CS0117）

**文件**：`Assets/_Project/Scripts/SocialDeduction/BuildingBuilder.cs`

**现象**：类中调用了 17 处 `SetMaterial(GameObject obj, Color color)`，但方法未定义。类中仅定义了同功能的 `SetSimpleMaterial`。

**调用位置**：行 344, 367, 384, 389, 394, 403, 410, 424, 448, 461, 475, 486, 508, 515, 566, 572, 594, 601, 607, 625, 631

**修复**：在 `SetSimpleMaterial` 方法前新增 `SetMaterial` 方法（行 659），Body 与 `SetSimpleMaterial` 一致——委托给 `MaterialFactory.GetSimpleMaterial(color)`。

```csharp
private static void SetMaterial(GameObject obj, Color color)
{
    MeshRenderer mr = obj.GetComponent<MeshRenderer>();
    if (mr == null) return;
    mr.sharedMaterial = MaterialFactory.GetSimpleMaterial(color);
}
```

### 错误 2：`StreetFurniture.PlaceRailingSegment` 参数数量不匹配（CS1501）

**文件**：`Assets/_Project/Scripts/SocialDeduction/StreetFurniture.cs`，行 433

**现象**：调用 `PlaceRailingSegment(pos, parent, generatedObjects)` 传入 3 个参数，但方法定义（行 160）需要 4 个参数：
```csharp
public static GameObject PlaceRailingSegment(
    Vector3 startPos, float length, Transform parent, List<GameObject> generatedObjects)
```

**修复**：补充缺失的 `length` 参数。调用上下文 `PlaceRailingAlong` 方法中 `segmentLen = 0.3f` 已在作用域内定义，直接传入：

```csharp
// 修复前
PlaceRailingSegment(pos, parent, generatedObjects);

// 修复后
PlaceRailingSegment(pos, segmentLen, parent, generatedObjects);
```

---

## 已验证无错误的部分

以下检查均确认无误（调用与定义一致）：

| 文件 | 检查项 | 结果 |
|------|--------|------|
| `EnvironmentManager.cs` | `PlaceStreetLightsAlong` / `PlaceRailingAlong` / `PlaceTrafficLight` / `ScatterProps` / `PlaceBench` / `PlaceFireHydrant` / `PlaceNewsStand` / `PlaceTrashBin` 等 ~17 处调用 | 签名匹配 |
| `EnvironmentManager.cs` | `BuildDistrictCore` / `BuildAnnex` / `GenerateBuilding` 等 ~30 处调用 | 签名匹配 |
| `EnvironmentManager.cs` | `CreateAreaLight` 7 参数调用 × 6 | 签名匹配 |
| `BuildingBuilder.cs` | `SetSimpleMaterial` 调用 × 4 | 签名匹配 |
| `MaterialFactory.cs` | `GetSimpleMaterial` / `GetNeonMaterial` / `PreWarmTextures` | 签名匹配 |
| `StreetFurniture.cs` | 所有 `ApplySimpleMaterial` / `ApplyMaterial` / `SetupMeshRenderer` 调用 | 签名匹配 |
| 全局 | 跨命名空间类型引用（Editor 脚本引用 `GanglandUndercover.Online` 等） | 可达 |
| 全局 | 语法检查（括号匹配、分号终结） | 无违规 |

---

## 修复状态

| # | 错误码 | 文件 | 行号 | 状态 |
|---|--------|------|------|------|
| 1 | CS0117 | BuildingBuilder.cs | 344+ (第一处) | 已修复 |
| 2 | CS1501 | StreetFurniture.cs | 433 | 已修复 |
