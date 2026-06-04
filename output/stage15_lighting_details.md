---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_d19290385f1d11f1b5095254007bceed
    ReservedCode1: WMV1UkWKGWJAThpkLH9UfPX8lI0UWKDtGrsD2NRgEQ5RxoNzWijW0gfuCcXWBpedpdU5Rl/TF/P5Ta96mzt8BC2T+IbXeG+qw41Oovp8SaujGVUk6SebaQ8m8Z+OnHvtTSPqI/PeK+UOe4ZRstRLz/degk8nE+PUDaQMrAyhehmaOUPE0oDDMPbnBCU=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_d19290385f1d11f1b5095254007bceed
    ReservedCode2: WMV1UkWKGWJAThpkLH9UfPX8lI0UWKDtGrsD2NRgEQ5RxoNzWijW0gfuCcXWBpedpdU5Rl/TF/P5Ta96mzt8BC2T+IbXeG+qw41Oovp8SaujGVUk6SebaQ8m8Z+OnHvtTSPqI/PeK+UOe4ZRstRLz/degk8nE+PUDaQMrAyhehmaOUPE0oDDMPbnBCU=
---

# Stage 15: 场景灯光 + 氛围 + 装饰细节

**日期**: 2026-06-03  
**项目**: GanglandUndercover

---

## 产出物清单

| 文件 | 类型 | 说明 |
|---|---|---|
| `LightingMaster.cs` | 新增 | 白天/傍晚/夜间三模式灯光控制器，区域灯光区分，软阴影 |
| `WeatherController.cs` | 新增 | 雾效（ExponentialSquared）、地面雾、飘尘粒子、天空背景 |
| `DetailScatter.cs` | 新增 | 街头装饰散布：海报/涂鸦/电线/空调外机/水坑/烟蒂碎屑 |
| `BillboardSystem.cs` | 新增 | 建筑顶部广告牌 + 夜市霓虹招牌闪烁 |
| `EnvironmentManager.cs` | 修改 | 新增 `InitializeAllAtmosphereSystems()` 统一入口 |
| `SocialPrototypeController.cs` | 修改 | `BuildWorld()` / `BuildPoliceStationWorld()` 调用大气系统初始化 |

---

## 系统设计

### 1. LightingMaster — 灯光氛围主控

- **三模式**：`Day` / `Evening`（默认）/ `Night`，每模式独立配置颜色、强度、太阳角度、雾密度。
- **软阴影**：通过 `UniversalRenderPipelineAsset.shadowDistance = 80m`。
- **区域灯光**：`NightMarket`（霓虹粉红，Perlin闪烁）/ `Tenement`（暖黄，不闪烁）/ `Dock`（冷白，不闪烁）。
- **运行时可切换**：`SetLightingMode()`。

### 2. WeatherController — 天气氛围

- **全局雾**：`FogMode.ExponentialSquared`，默认 `density=0.002`，可通过 `SetFogDensity()` 和 `LerpFogDensity()` 平滑过渡。
- **地面雾**：水平 Quad 面片 + Transparent 材质，贴近地面表现街巷底部雾气。
- **飘尘粒子**：`ParticleSystem` Box 形状散布，Perlin 噪声驱动微动，带透明度渐变。
- **天空背景**：`Camera.backgroundColor` 暗色，可选远处 Skybox Plane。

### 3. DetailScatter — 街头装饰散布

- **海报**：Raycast 找墙面 → 随机位置贴 Quad，随机颜色材质。
- **涂鸦**：同上，半透明材质，更大幅度。
- **电线**：`LineRenderer` 水平穿拉，小型垂度模拟。
- **空调外机**：贴墙 Cube，金属材质。
- **水坑**：地面 Quad + Transparent 深色。
- **烟蒂 / 碎屑**：随机位置的微型 Cylinder，颜色微变体。
- 所有生成物聚合在 `scatteredObjects` 中，`OnDestroy` 统一清理。

### 4. BillboardSystem — 广告牌与霓虹

- **建筑顶部广告牌**：默认 3 块（赌场/酒类/夜店），每块配 Point Light 照亮。
- **夜市霓虹招牌**：默认 6 种配置（龙宫/金凤/夜吧/娱乐/茶楼/药铺），环形分布在 NightMarket 区域。
- **闪烁效果**：`Update()` 中 Perlin 噪声驱动 Emission 颜色和 Light intensity 同步闪烁。
- **无预设材质要求**：全部运行时生成 Standard + EMISSION 材质。

---

## 集成方式

`EnvironmentManager.InitializeAllAtmosphereSystems(Transform parent)` 作为统一入口：

1. 在自身 GameObject 上挂载 `LightingMaster`（带默认 Profile）。
2. 在自身 GameObject 上挂载 `WeatherController`。
3. 新建子 GameObject 挂载 `DetailScatter` → 立即调用 `ScatterAllDetails()`。
4. 新建子 GameObject 挂载 `BillboardSystem` → Awake 创建招牌。

**调用点**：`SocialPrototypeController.BuildWorld()` 和 `BuildPoliceStationWorld()` 中，`SetupEnvironment()` 之后、`CreateFloor()` 之前。

---

## 编译验证

- **代码语法**：全部 6 个 `.cs` 文件花括号严格平衡（open = close）。
- **命名空间一致**：全部使用 `GanglandUndercover.SocialDeduction`。
- **类型引用**：新文件依赖 Unity 标准 API（`Camera` / `RenderSettings` / `Light` / `ParticleSystem` / `LineRenderer` / `MeshRenderer` / `Material` / `Shader`），CS0246 风险仅限 Unity Editor 编译环境。
- **无外部依赖新增**：未引入任何第三方包。

---

## 待办

| # | 描述 |
|---|---|
| 1 | 在 Unity Editor 中完整编译验证 |
| 2 | 为海报/涂鸦添加真实贴图资源替代纯色材质 |
| 3 | 霓虹招牌支持 TextMeshPro 文字渲染（当前为纯色发光 Quad） |
| 4 | DetailScatter 的 Raycast 依赖场景中已有 Collider 几何体 |
*（内容由AI生成，仅供参考）*
