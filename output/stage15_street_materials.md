---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_d13759545f1d11f1bd025254006c9bbf
    ReservedCode1: 1IsV+hpJq/cJioz0/G6TZ1Z8FRNOHbYRvjyOp8E+ckxbMZD43UApQMHcINq1ejHfDuBG1k1Geu1qhMu06bMqeePcLvODXt/53hrxmQ6cVfHmg1aCyF9erS87XWmrGkT8ScrLFWKTe3eoLfRT7mpqN0UU3UI/O5qfGY7dMI584S4oVkk8blAq6xH1Yd0=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_d13759545f1d11f1bd025254006c9bbf
    ReservedCode2: 1IsV+hpJq/cJioz0/G6TZ1Z8FRNOHbYRvjyOp8E+ckxbMZD43UApQMHcINq1ejHfDuBG1k1Geu1qhMu06bMqeePcLvODXt/53hrxmQ6cVfHmg1aCyF9erS87XWmrGkT8ScrLFWKTe3eoLfRT7mpqN0UU3UI/O5qfGY7dMI584S4oVkk8blAq6xH1Yd0=
---

# Stage 15: 街景物品建模 + 材质纹理系统

> 产出日期: 2026-06-03
> 项目: GanglandUndercover — SocialDeduction 模块
> 风格定位: GTA / Watch Dogs 级别街头写实

---

## 一、新增文件

| 文件 | 路径 | 说明 |
|---|---|---|
| MaterialFactory.cs | `Assets/_Project/Scripts/SocialDeduction/MaterialFactory.cs` | PBR 材质工厂（8 类预设 + 缓存） |
| ProceduralTexture.cs | `Assets/_Project/Scripts/SocialDeduction/ProceduralTexture.cs` | 程序化纹理生成器（4 类纹理） |
| StreetFurniture.cs | `Assets/_Project/Scripts/SocialDeduction/StreetFurniture.cs` | 街景物品（路灯/信号灯/围栏/垃圾桶/长椅/消防栓/报刊亭） |
| StreetProps.cs | `Assets/_Project/Scripts/SocialDeduction/StreetProps.cs` | 散落道具（纸箱堆/油桶堆/轮胎堆/木托盘/锥筒） |

## 二、修改文件

| 文件 | 修改内容 |
|---|---|
| BuildingBuilder.cs | 移除 `FindShader()` 和原生 `SetMaterial()`；全部改为 `MaterialFactory.GetSimpleMaterial` / `MaterialFactory.GetNeonMaterial`；新增 `SetSimpleMaterial` |
| EnvironmentManager.cs | 新增 `PreWarmAssets()` 材质预热入口；新增 `PlaceStreetFurniture()` 批量布置路灯/围栏/道具 |

---

## 三、MaterialFactory — 材质工厂

### 材质预设（8 类）

| 预设 | BaseColor | Metallic | Smoothness | 程序化纹理 | 用途 |
|---|---|---|---|---|---|
| BrickWall | #964d39 红棕 | 0.02 | 0.25 | 砖墙纹理 | 公寓外墙 |
| Concrete | #7b7975 灰白 | 0 | 0.12 | 混凝土纹理 | 仓库/基座 |
| IronSheet | #4b4d4f 暗灰 | 0.82 | 0.35 | — | 灯杆/围栏/门框 |
| Glass | 半透蓝灰 | 0.1 | 0.88 | — | 玻璃幕墙/橱窗 |
| Wood | #64442b 棕褐 | 0 | 0.28 | 木纹纹理 | 长椅/托盘/报刊亭 |
| Asphalt | #242322 深黑 | 0 | 0.06 | 沥青纹理 | 路面/屋顶 |
| RustedIron | #824223 锈橙 | 0.35 | 0.08 | — | 消防梯/旧铁件 |
| Neon | #FF385C 霓虹粉 | 0 | 0.15 | — | 招牌/信号灯 |

### API 要点

```csharp
// 获取预设材质（带程序化纹理绑定）
Material mat = MaterialFactory.GetMaterial(MaterialPreset.BrickWall);

// 获取霓虹材质（自定义颜色/强度）
Material mat = MaterialFactory.GetNeonMaterial(Color.red, 3.0f);

// 获取简易纯色材质
Material mat = MaterialFactory.GetSimpleMaterial(Color.gray, 0.5f, 0.3f);

// 预热所有纹理（BuildWorld 早期调用一次）
MaterialFactory.PreWarmTextures(512);

// 清除缓存（场景卸载时）
MaterialFactory.ClearCache();
```

---

## 四、ProceduralTexture — 程序化纹理

### 纹理类型

| 类型 | 算法 | 分辨率 | 特性 |
|---|---|---|---|
| 砖墙 | 交错网格 + 随机色差 | 512x512 RGB24 | 8列12行砖块 + 灰缝 |
| 混凝土 | 3 层 Perlin Noise 叠加 | 512x512 RGB24 | 多层噪声 + 随机气孔暗点 |
| 木纹 | 正弦波条 + 结疤 | 512x512 RGB24 | 木纹周期 + 随机深色圆斑 |
| 沥青 | 高频噪声 + 随机亮斑 | 512x512 RGB24 | 石子颗粒感 + 反光亮斑 |

### API 要点

```csharp
// 生成纹理（不缓存）
Texture2D tex = ProceduralTexture.Generate(MaterialPreset.BrickWall, 512, 512);

// 获取缓存纹理
Texture2D tex = ProceduralTexture.GetTexture(MaterialPreset.BrickWall);

// 预生成全部
ProceduralTexture.PreWarmAll(512);

// 清除缓存
ProceduralTexture.ClearCache();
```

---

## 五、StreetFurniture — 街景物品

### 物件清单

| 物件 | 方法 | 结构 | 光源 |
|---|---|---|---|
| 路灯 | `PlaceStreetLight` | Cylinder 杆(3m高) + 弯臂 + 灯罩Box + 暖黄面板 | PointLight (1f,0.78f,0.42f) @1.8 intensity |
| 交通信号灯 | `PlaceTrafficLight` | Cylinder 杆 + 横臂 + 信号灯盒(Cube) + 3×Sphere(红/黄/绿) | 自发光 Neon 材质 |
| 围栏 | `PlaceRailingSegment` | N 根 Cylinder 立柱 + 2 层 Cube 横梁 | 无 |
| 垃圾桶 | `PlaceTrashBin` | Cylinder 桶身 + 略大桶盖 | 无（深绿/深灰随机） |
| 长椅 | `PlaceBench` | 3 根座面木条 + 靠背横梁 + 4 根铁支柱 | 无 |
| 消防栓 | `PlaceFireHydrant` | Cylinder 主体 + 顶盖 + 2 侧出水口 | 无（红色） |
| 报刊亭 | `PlaceNewsStand` | Cube 主体 + 玻璃橱窗 + 遮阳棚 | 无 |

### 批量布置

```csharp
// 沿街道放置路灯（间隔 1.8m）
StreetFurniture.PlaceStreetLightsAlong(start, end, 1.8f, parent, gen);

// 沿人行道放置围栏
StreetFurniture.PlaceRailingAlong(start, end, parent, gen);
```

---

## 六、StreetProps — 散落道具

| 道具 | 方法 | 结构 | 变体 |
|---|---|---|---|
| 纸箱堆 | `PlaceCardboardStack` | 2-4 层 Cube 叠放 + 随机旋转偏移 | 棕色随机变化 + 50% 封条 |
| 油桶堆 | `PlaceOilDrumStack` | 2-3 个 Cylinder 桶 + 加强筋 | 蓝/红/灰随机 + 微小倾斜 |
| 轮胎堆 | `PlaceTireStack` | 3-5 个扁 Cylinder 叠放 | 黑色橡胶 + 轮毂孔 |
| 木托盘 | `PlaceWoodenPallet` | 3 根横梁 + 5 根板条 | 标准托盘结构 |
| 锥筒 | `PlaceTrafficCone` | 2 段锥形 Cylinder + 底座 + 白色反光环 | 橙白标准配色 |

### 随机散落

```csharp
// 在矩形区域内随机放置 N 个道具（类型随机）
StreetProps.ScatterProps(center, size, count, parent, gen);
```

---

## 七、EnvironmentManager 集成

### 调用时序（BuildWorld 阶段）

```
1. env.PreWarmAssets()           // 预热 MaterialFactory + ProceduralTexture
2. env.CreateZoneAreaLights()    // 创建各区域环境光
3. env.BuildDistrict()           // 生成 6 区域建筑（BuildingBuilder 已使用 MaterialFactory）
4. env.PlaceStreetFurniture()    // 布置路灯/围栏/信号灯/长椅/消防栓/报刊亭/垃圾桶
                                 //   + 散落道具（货柜码头区 + 夜市巷区）
5. env.SetupFog()                // 雾效设置
```

### 布置清单（PlaceStreetFurniture）

- 主街路灯: ~5 盏（沿 Y=0，X: -4→4，间隔 1.8m）
- 人行道围栏: 2 段（主街两侧 Y=+0.18 / Y=-0.08）
- 交通信号灯: 2 座（路口两端 X=-4 和 X=+4）
- 散落道具: 3-6 个（货柜码头区）+ 2-5 个（夜市巷区）
- 长椅: 2 张（X=-1.5 和 X=+1.5）
- 消防栓: 2 个（街角）
- 报刊亭: 1 个（夜市巷旁）
- 垃圾桶: 2 个（人行道两侧）

---

## 八、代码规范

1. 所有新增类使用 `namespace GanglandUndercover.SocialDeduction`
2. `BuildingBuilder` / `StreetFurniture` / `StreetProps` 均为 `static class`，无 MonoBehaviour 依赖
3. MaterialFactory 和 ProceduralTexture 内部使用 Dictionary 缓存避免重复创建
4. 所有 GameObject 创建通过 `GameObject.CreatePrimitive` + `transform.SetParent` + `generatedObjects.Add` 统一管理生命周期
5. Shader 回退链: URP Lit → Standard → Unlit/Color → Sprites/Default
6. 玻璃材质自动设 `_Surface=1`（透明混合）

---

## 九、已知问题 / 后续改进

- [ ] 程序化纹理在 512x512 下砖缝偶有不对齐（下一阶段可增加抗锯齿）
- [ ] 围栏段沿曲线街道不支持旋转对齐（当前仅支持直线）
- [ ] 轮胎堆目前用 Cylinder 近似，可改用 Mesh 生成 Torus 形
- [ ] 阴影投射未开启（路灯/信号灯 `Light.shadows = None`），可根据性能预算后续打开
*（内容由AI生成，仅供参考）*
