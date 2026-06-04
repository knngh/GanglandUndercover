using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 环境管理器（v3）：管理全局光照、雾效、区域灯光、分区颜色分级（LUT模拟）、
    /// 动态环境光探针（区域色模拟）、地面贴花、断电（Sabotage Blackout）时的灯光切换，
    /// 以及程序化建筑生成（BuildingBuilder）。
    /// 由 SocialPrototypeController.BuildWorld 创建并绑定。
    /// </summary>
    public sealed class EnvironmentManager : MonoBehaviour
    {
        [Header("Light Storage")]
        private readonly List<LightEntry> sceneLights = new List<LightEntry>();
        private readonly List<ZoneGrading> zoneGradings = new List<ZoneGrading>();
        private readonly List<GameObject> decalObjects = new List<GameObject>();
        private bool isBlackout;

        // ─── 数据结构 ────────────────────────────────

        private struct LightEntry
        {
            public Light Light;
            public float OriginalIntensity;
            public Color OriginalColor;
        }

        /// <summary>区域颜色分级配置（LUT 模拟：zone 内所有 Renderer 被染色加权）。</summary>
        private struct ZoneGrading
        {
            public Vector3 Center;
            public Vector2 HalfSize;
            public Color TintColor;
            public float BlendFactor;
        }

        /// <summary>地面贴花数据。</summary>
        public enum DecalType : byte { Blood, Oil, Paper }

        // ─── 公共 API ─────────────────────────────────

        /// <summary>注册一盏动态灯光（含原始强度和颜色）。</summary>
        public void RegisterLight(Light light)
        {
            if (light == null) return;
            sceneLights.Add(new LightEntry
            {
                Light = light,
                OriginalIntensity = light.intensity,
                OriginalColor = light.color
            });
        }

        /// <summary>注册区域颜色分级（LUT 模拟染色）。</summary>
        public void RegisterZoneGrading(Vector3 center, Vector2 halfSize, Color tintColor, float blendFactor = 0.18f)
        {
            zoneGradings.Add(new ZoneGrading
            {
                Center = center,
                HalfSize = halfSize,
                TintColor = tintColor,
                BlendFactor = blendFactor
            });
        }

        /// <summary>切换断电状态：true=全局暗，false=恢复。</summary>
        public void SetBlackout(bool active)
        {
            if (isBlackout == active) return;
            isBlackout = active;

            float targetFactor = active ? 0.15f : 1f;
            Color ambientColor = active
                ? new Color(0.02f, 0.03f, 0.08f, 1f)
                : new Color(0.08f, 0.09f, 0.12f, 1f);

            RenderSettings.ambientLight = ambientColor;
            RenderSettings.fogColor = Color.Lerp(
                new Color(0.015f, 0.035f, 0.05f, 1f),
                ambientColor,
                0.5f);

            foreach (LightEntry entry in sceneLights)
            {
                if (entry.Light == null) continue;
                entry.Light.intensity = entry.OriginalIntensity * targetFactor;
                if (active)
                {
                    entry.Light.color = Color.Lerp(entry.OriginalColor, new Color(0.1f, 0.1f, 0.3f, 1f), 0.7f);
                }
                else
                {
                    entry.Light.color = entry.OriginalColor;
                }
            }
        }

        /// <summary>设置全局雾效（Scene Startup 时调用一次）。</summary>
        public void SetupFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.015f, 0.035f, 0.05f, 1f);
            RenderSettings.fogStartDistance = 6f;
            RenderSettings.fogEndDistance = 16f;
            RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.12f, 1f);
        }

        /// <summary>
        /// 每帧更新：动态环境光探针模拟 — 根据主相机位置混合区域颜色，
        /// 注入 RenderSettings.ambientLight（LightProbes API 不可用时的替代方案）。
        /// </summary>
        public void TickAmbientProbe(Vector3 observerPosition)
        {
            if (isBlackout || zoneGradings.Count == 0) return;

            Color blended = RenderSettings.ambientLight;
            int contributors = 0;

            foreach (ZoneGrading zg in zoneGradings)
            {
                if (Mathf.Abs(observerPosition.x - zg.Center.x) <= zg.HalfSize.x
                    && Mathf.Abs(observerPosition.y - zg.Center.y) <= zg.HalfSize.y)
                {
                    blended = Color.Lerp(blended, zg.TintColor, zg.BlendFactor);
                    contributors++;
                }
            }

            if (contributors > 0)
            {
                RenderSettings.ambientLight = blended;
            }
        }

        /// <summary>
        /// 创建区域环境光 + 分区颜色分级（暖橙/暗蓝灰/霓虹粉紫/冷白蓝/柔和绿/暗暖黄）。
        /// 同时注册 zone grading 供 TickAmbientProbe 使用。
        /// </summary>
        public void CreateZoneAreaLights(Transform parent, List<GameObject> generatedObjects)
        {
            // ─── 货柜码头：暖橙色 ───
            CreateAreaLight("DockyardAmbient", new Vector3(-3.5f, 1.9f, -0.3f),
                new Color(1f, 0.62f, 0.28f, 1f), 2.2f, 2.8f, parent, generatedObjects);
            RegisterZoneGrading(new Vector3(-3.25f, 1.85f, 0f), new Vector2(1.28f, 0.9f),
                new Color(0.14f, 0.09f, 0.04f, 1f), 0.20f);

            // ─── 证物库 / Warehouse：暗蓝灰 ───
            CreateAreaLight("WarehouseAmbient", new Vector3(-2.8f, -1.9f, -0.3f),
                new Color(0.36f, 0.42f, 0.62f, 1f), 1.5f, 2.5f, parent, generatedObjects);
            RegisterZoneGrading(new Vector3(-2.8f, -1.9f, 0f), new Vector2(1.18f, 0.78f),
                new Color(0.03f, 0.04f, 0.07f, 1f), 0.22f);

            // ─── 夜市巷：霓虹粉紫 ───
            CreateAreaLight("NightMarketAmbient", new Vector3(0f, 2.05f, -0.3f),
                new Color(0.92f, 0.18f, 0.48f, 1f), 1.5f, 2.6f, parent, generatedObjects);
            RegisterZoneGrading(new Vector3(0f, 2.05f, 0f), new Vector2(1.18f, 0.78f),
                new Color(0.07f, 0.02f, 0.06f, 1f), 0.18f);

            // ─── 专案办公室：冷白蓝 ───
            CreateAreaLight("OfficeAmbient", new Vector3(3.25f, 1.25f, -0.3f),
                new Color(0.42f, 0.52f, 0.82f, 1f), 1.8f, 2.6f, parent, generatedObjects);
            RegisterZoneGrading(new Vector3(3.25f, 1.25f, 0f), new Vector2(1.1f, 0.9f),
                new Color(0.04f, 0.05f, 0.08f, 1f), 0.15f);

            // ─── 地下诊所：柔和绿 ───
            CreateAreaLight("ClinicAmbient", new Vector3(2.65f, -2f, -0.3f),
                new Color(0.2f, 0.7f, 0.42f, 1f), 1.8f, 2.5f, parent, generatedObjects);
            RegisterZoneGrading(new Vector3(2.65f, -2f, 0f), new Vector2(1.23f, 0.78f),
                new Color(0.02f, 0.06f, 0.04f, 1f), 0.18f);

            // ─── 主街 / Tenement：暗暖黄 ───
            CreateAreaLight("MainStreetAmbient", new Vector3(0f, 0f, -0.3f),
                new Color(0.78f, 0.62f, 0.28f, 1f), 1.2f, 5.5f, parent, generatedObjects);
            RegisterZoneGrading(new Vector3(0f, 0.05f, 0f), new Vector2(4.4f, 0.48f),
                new Color(0.06f, 0.05f, 0.02f, 1f), 0.14f);
        }

        // ─── 街景物品布置 ─────────────────────────

        /// <summary>
        /// 沿主街布置路灯、围栏和散落道具。
        /// 在 BuildDistrict 末尾调用。
        /// </summary>
        public void PlaceStreetFurniture(Transform parent, List<GameObject> generatedObjects)
        {
            // 主街路灯（沿 Y=0 轴，X 方向每隔 1.8m 一盏）
            StreetFurniture.PlaceStreetLightsAlong(
                new Vector3(-4f, 0.05f, 0f),
                new Vector3(4f, 0.05f, 0f),
                1.8f, parent, generatedObjects);

            // 人行道围栏（主街两侧）
            StreetFurniture.PlaceRailingAlong(
                new Vector3(-4f, 0.18f, 0f),
                new Vector3(4f, 0.18f, 0f),
                parent, generatedObjects);
            StreetFurniture.PlaceRailingAlong(
                new Vector3(-4f, -0.08f, 0f),
                new Vector3(4f, -0.08f, 0f),
                parent, generatedObjects);

            // 路口交通信号灯
            StreetFurniture.PlaceTrafficLight(
                new Vector3(-4f, 0.05f, 0f), parent, generatedObjects);
            StreetFurniture.PlaceTrafficLight(
                new Vector3(4f, 0.05f, 0f), parent, generatedObjects);

            // 散落道具（在货柜码头和夜市巷区域）
            StreetProps.ScatterProps(
                new Vector3(-3.5f, 1.9f, 0f), new Vector2(0.8f, 0.6f),
                Random.Range(3, 6), parent, generatedObjects);
            StreetProps.ScatterProps(
                new Vector3(0f, 2.05f, 0f), new Vector2(0.6f, 0.5f),
                Random.Range(2, 5), parent, generatedObjects);

            // 长椅（沿人行道）
            StreetFurniture.PlaceBench(
                new Vector3(-1.5f, 0.16f, 0f), parent, generatedObjects);
            StreetFurniture.PlaceBench(
                new Vector3(1.5f, 0.16f, 0f), parent, generatedObjects);

            // 消防栓（街角）
            StreetFurniture.PlaceFireHydrant(
                new Vector3(-3.9f, -0.05f, 0f), parent, generatedObjects);
            StreetFurniture.PlaceFireHydrant(
                new Vector3(3.9f, -0.05f, 0f), parent, generatedObjects);

            // 报刊亭（夜市巷附近）
            StreetFurniture.PlaceNewsStand(
                new Vector3(0.5f, 2.4f, 0f), parent, generatedObjects);

            // 垃圾桶
            StreetFurniture.PlaceTrashBin(
                new Vector3(-0.8f, 0.16f, 0f), parent, generatedObjects);
            StreetFurniture.PlaceTrashBin(
                new Vector3(0.8f, 0.16f, 0f), parent, generatedObjects);

            Debug.Log("[EnvironmentManager] 街景物品布置完成。");
        }

        /// <summary>
        /// 场景启动时调用：预热所有程序化纹理和材质缓存。
        /// 应在 BuildWorld 阶段最早期调用。
        /// </summary>
        public void PreWarmAssets()
        {
            MaterialFactory.PreWarmTextures(512);
            Debug.Log("[EnvironmentManager] 材质与程序化纹理预热完成。");
        }

        // ─── 程序化建筑生成（v3）───────────────────────

        /// <summary>
        /// 调用 BuildingBuilder 为 6 个区域生成 GTA/Watch Dogs 级别街头写实建筑。
        /// 每区域生成核心建筑 + 附属建筑，间距 3-5m 形成街巷感。
        /// 同时沿街道布置路灯、围栏、散落道具。
        /// </summary>
        public void BuildDistrict(Transform parent, List<GameObject> generatedObjects)
        {
            // ─── 货柜码头（Dockyard）→ Tenement 公寓 ───
            BuildDistrictCore(parent, generatedObjects,
                BuildingBuilder.TenementConfig(new Vector3(-3.25f, 1.85f, 0f), 1.6f, 1.2f));
            BuildAnnex(parent, generatedObjects,
                BuildingBuilder.TenementConfig(new Vector3(-4.05f, 1.35f, 0f), 0.9f, 0.9f));
            BuildAnnex(parent, generatedObjects,
                BuildingBuilder.TenementConfig(new Vector3(-2.45f, 2.35f, 0f), 0.85f, 0.85f));

            // ─── 证物库（WarehouseRow）→ Warehouse 仓库 ───
            BuildDistrictCore(parent, generatedObjects,
                BuildingBuilder.WarehouseConfig(new Vector3(-2.8f, -1.9f, 0f), 2.0f, 1.3f));
            BuildAnnex(parent, generatedObjects,
                BuildingBuilder.WarehouseConfig(new Vector3(-1.95f, -2.4f, 0f), 1.05f, 0.85f));

            // ─── 夜市巷（NightMarket）→ NightMarket 商铺 ───
            BuildDistrictCore(parent, generatedObjects,
                BuildingBuilder.NightMarketConfig(new Vector3(-0.55f, 2.05f, 0f), 0.85f, 0.75f));
            BuildAnnex(parent, generatedObjects,
                BuildingBuilder.NightMarketConfig(new Vector3(0.65f, 2.05f, 0f), 0.85f, 0.75f));
            BuildAnnex(parent, generatedObjects,
                BuildingBuilder.NightMarketConfig(new Vector3(0.05f, 2.62f, 0f), 0.7f, 0.6f));

            // ─── 专案办公室（PolicePrecinct）→ Office 办公楼 ───
            BuildDistrictCore(parent, generatedObjects,
                BuildingBuilder.OfficeConfig(new Vector3(3.25f, 1.25f, 0f), 1.7f, 1.4f));
            BuildAnnex(parent, generatedObjects,
                BuildingBuilder.OfficeConfig(new Vector3(4.05f, 0.65f, 0f), 0.8f, 0.8f));

            // ─── 地下诊所（Clinic）→ Clinic 诊所 ───
            BuildDistrictCore(parent, generatedObjects,
                BuildingBuilder.ClinicConfig(new Vector3(2.65f, -2f, 0f), 1.5f, 1.1f));
            BuildAnnex(parent, generatedObjects,
                BuildingBuilder.ClinicConfig(new Vector3(3.55f, -2.35f, 0f), 0.7f, 0.7f));

            // ─── 主街（TenementBlock）→ Tenement 公寓排 ───
            // 沿主街 Y=0 轴布置多栋公寓，间距 0.7-0.9m（≈ 3-5m 街巷感）
            float[] mainStPositions = { -3.2f, -1.6f, 0f, 1.6f, 3.2f };
            for (int i = 0; i < mainStPositions.Length; i++)
            {
                float x = mainStPositions[i];
                float width = Random.Range(0.9f, 1.25f);
                float depth = 0.6f + Random.Range(0f, 0.1f);
                float yOffset = (i % 2 == 0) ? 0.05f : -0.05f; // 错落
                BuildingConfig cfg = BuildingBuilder.TenementConfig(
                    new Vector3(x, 0.05f + yOffset, 0f), width, depth);
                cfg.Floors = Random.Range(3, 6);
                BuildingBuilder.GenerateBuilding(cfg, parent, generatedObjects);
            }

            Debug.Log("[EnvironmentManager] BuildDistrict: 6 区域建筑生成完成。");
        }

        /// <summary>生成区域核心建筑。</summary>
        private static void BuildDistrictCore(Transform parent, List<GameObject> gen, BuildingConfig config)
        {
            BuildingBuilder.GenerateBuilding(config, parent, gen);
        }

        /// <summary>生成附属建筑（微调参数增加变体）。</summary>
        private static void BuildAnnex(Transform parent, List<GameObject> gen, BuildingConfig config)
        {
            config.Floors = Mathf.Max(1, config.Floors - Random.Range(1, 3));
            config.WindowCountPerFloor = Mathf.Max(1, config.WindowCountPerFloor - Random.Range(1, 2));
            config.HasFireEscape = false;
            config.HasRoofWaterTank = false;
            BuildingBuilder.GenerateBuilding(config, parent, gen);
        }

        // ─── 地面贴花 ────────────────────────────────

        /// <summary>
        /// 在指定位置创建地面贴花（血迹/油渍/纸屑），使用简单 Quad + 透明材质。
        /// </summary>
        public void PlaceFloorDecal(DecalType type, Vector3 worldPosition, Transform parent,
            List<GameObject> generatedObjects)
        {
            Color decalColor;
            float decalSize;
            float rotationDeg;

            switch (type)
            {
                case DecalType.Blood:
                    decalColor = new Color(0.58f, 0.04f, 0.04f, 0.42f);
                    decalSize = Random.Range(0.28f, 0.52f);
                    rotationDeg = Random.Range(0f, 360f);
                    break;
                case DecalType.Oil:
                    decalColor = new Color(0.04f, 0.04f, 0.06f, 0.55f);
                    decalSize = Random.Range(0.35f, 0.65f);
                    rotationDeg = Random.Range(0f, 360f);
                    break;
                case DecalType.Paper:
                    decalColor = new Color(0.72f, 0.68f, 0.56f, 0.48f);
                    decalSize = Random.Range(0.12f, 0.24f);
                    rotationDeg = Random.Range(0f, 360f);
                    break;
                default:
                    return;
            }

            GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            decal.name = $"Decal_{type}_{decalObjects.Count}";
            generatedObjects.Add(decal);
            decalObjects.Add(decal);

            decal.transform.SetParent(parent, false);
            decal.transform.position = new Vector3(worldPosition.x, worldPosition.y, -0.06f);
            decal.transform.rotation = Quaternion.Euler(90f, rotationDeg, 0f);
            decal.transform.localScale = new Vector3(decalSize, decalSize, 1f);

            MeshRenderer mr = decal.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Transparent")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Sprites/Default");

                Material mat = new Material(shader);
                mat.color = decalColor;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_Surface", 1); // Transparent
                mat.renderQueue = 3000;
                mr.sharedMaterial = mat;
            }
        }

        /// <summary>清除所有贴花。</summary>
        public void ClearDecals()
        {
            foreach (GameObject decal in decalObjects)
            {
                if (decal != null) SafeDestroy(decal);
            }
            decalObjects.Clear();
        }

        // ─── 内部辅助 ────────────────────────────────

        private void CreateAreaLight(string name, Vector3 position, Color color, float intensity, float range,
            Transform parent, List<GameObject> generatedObjects)
        {
            GameObject lightObj = new GameObject(name);
            lightObj.transform.SetParent(parent, false);
            lightObj.transform.position = position;
            generatedObjects.Add(lightObj);

            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;

            RegisterLight(light);
        }

        // ─── 灯光、天气、装饰系统集成 ──────────────────────────────

        /// <summary>
        /// 初始化全局灯光主控（LightingMaster），挂载在 EnvironmentManager 所在的 GameObject 上。
        /// </summary>
        public LightingMaster InitializeLightingMaster()
        {
            LightingMaster lm = GetComponent<LightingMaster>();
            if (lm != null) return lm;

            lm = gameObject.AddComponent<LightingMaster>();
            lm.CreateDefaultProfiles();
            return lm;
        }

        /// <summary>
        /// 初始化天气与氛围控制器（WeatherController），挂载在 EnvironmentManager 所在的 GameObject 上。
        /// </summary>
        public WeatherController InitializeWeatherController()
        {
            WeatherController wc = GetComponent<WeatherController>();
            if (wc != null) return wc;

            wc = gameObject.AddComponent<WeatherController>();
            return wc;
        }

        /// <summary>
        /// 初始化并散布街头装饰细节（DetailScatter）。
        /// </summary>
        public DetailScatter InitializeDetailScatter(Transform parent)
        {
            GameObject go = new GameObject("DetailScatter");
            go.transform.SetParent(parent, false);
            DetailScatter ds = go.AddComponent<DetailScatter>();
            ds.ScatterAllDetails();
            return ds;
        }

        /// <summary>
        /// 初始化广告牌与霓虹招牌系统（BillboardSystem）。
        /// </summary>
        public BillboardSystem InitializeBillboardSystem(Transform parent)
        {
            GameObject go = new GameObject("BillboardSystem");
            go.transform.SetParent(parent, false);
            BillboardSystem bs = go.AddComponent<BillboardSystem>();
            return bs;
        }

        /// <summary>
        /// 一次性初始化灯光、天气、装饰和广告牌系统。
        /// 在 SetupEnvironment 之后（BuildDistrict 之前或之后）调用。
        /// </summary>
        public void InitializeAllAtmosphereSystems(Transform parent)
        {
            InitializeLightingMaster();
            InitializeWeatherController();
            InitializeDetailScatter(parent);
            InitializeBillboardSystem(parent);
            Debug.Log("[EnvironmentManager] Atmosphere systems initialized (Lighting + Weather + DetailScatter + Billboards)");
        }

        private static void SafeDestroy(GameObject obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}