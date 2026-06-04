using UnityEngine;
using UnityEngine.Rendering;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 场景灯光氛围控制器
    /// 管理白天/傍晚/夜间三种灯光模式，适配写实黑帮氛围
    /// </summary>
    public class LightingMaster : MonoBehaviour
    {
        [Header("Lighting Profiles")]
        [SerializeField] private LightingProfile dayProfile;
        [SerializeField] private LightingProfile eveningProfile;
        [SerializeField] private LightingProfile nightProfile;

        [Header("Current Mode")]
        [SerializeField] private LightingMode currentMode = LightingMode.Evening;

        [Header("Shadow Settings")]
        [SerializeField] private float shadowDistance = 80f;
        [SerializeField] private LightShadows shadowQuality = LightShadows.Soft;

        [Header("Zone Lighting")]
        [SerializeField] private ZoneLightingConfig nightMarketZone;
        [SerializeField] private ZoneLightingConfig tenementZone;
        [SerializeField] private ZoneLightingConfig dockZone;

        private Light mainDirectionalLight;
        private RenderPipelineAsset renderPipelineAsset;

        public enum LightingMode
        {
            Day,
            Evening,
            Night
        }

        [System.Serializable]
        public class LightingProfile
        {
            public string profileName;
            public Color skyColor = Color.white;
            public Color equatorColor = Color.gray;
            public Color groundColor = Color.black;
            public float ambientIntensity = 1f;
            public float fogDensity = 0.002f;
            public Color fogColor = Color.gray;
            public Vector3 sunRotation = new Vector3(50f, -30f, 0f);
            public Color sunColor = Color.white;
            public float sunIntensity = 1.5f;
        }

        [System.Serializable]
        public class ZoneLightingConfig
        {
            public string zoneName;
            public Light[] zoneLights;
            public Color zoneColor = Color.white;
            public float zoneIntensity = 1f;
            public bool enableFlicker;
            public float flickerSpeed = 1f;
        }

        private void Awake()
        {
            mainDirectionalLight = GetComponent<Light>();
            renderPipelineAsset = GraphicsSettings.currentRenderPipeline;
            ApplyShadowSettings();
        }

        private void Start()
        {
            SetLightingMode(currentMode);
            InitializeZoneLighting();
        }

        /// <summary>
        /// 设置灯光模式
        /// </summary>
        public void SetLightingMode(LightingMode mode)
        {
            currentMode = mode;
            LightingProfile profile = GetProfile(mode);
            if (profile != null)
            {
                ApplyLightingProfile(profile);
            }
        }

        private LightingProfile GetProfile(LightingMode mode)
        {
            switch (mode)
            {
                case LightingMode.Day: return dayProfile;
                case LightingMode.Evening: return eveningProfile;
                case LightingMode.Night: return nightProfile;
                default: return eveningProfile;
            }
        }

        private void ApplyLightingProfile(LightingProfile profile)
        {
            // 环境光
            RenderSettings.ambientSkyColor = profile.skyColor;
            RenderSettings.ambientEquatorColor = profile.equatorColor;
            RenderSettings.ambientGroundColor = profile.groundColor;
            RenderSettings.ambientIntensity = profile.ambientIntensity;

            // 雾效
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = profile.fogDensity;
            RenderSettings.fogColor = profile.fogColor;

            // 主光源
            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.color = profile.sunColor;
                mainDirectionalLight.intensity = profile.sunIntensity;
                mainDirectionalLight.transform.rotation = Quaternion.Euler(profile.sunRotation);
                mainDirectionalLight.shadows = shadowQuality;
            }

            Debug.Log($"[LightingMaster] Applied {profile.profileName} lighting profile");
        }

        private void ApplyShadowSettings()
        {
            QualitySettings.shadowDistance = shadowDistance;
            Debug.Log($"[LightingMaster] Shadow distance set to {shadowDistance}m");
        }

        private void InitializeZoneLighting()
        {
            SetupZone(nightMarketZone, "NightMarket");
            SetupZone(tenementZone, "Tenement");
            SetupZone(dockZone, "Dock");
        }

        private void SetupZone(ZoneLightingConfig zone, string defaultName)
        {
            if (zone == null || zone.zoneLights == null) return;

            foreach (Light light in zone.zoneLights)
            {
                if (light != null)
                {
                    light.color = zone.zoneColor;
                    light.intensity = zone.zoneIntensity;
                    light.shadows = shadowQuality;
                }
            }

            Debug.Log($"[LightingMaster] Zone lighting initialized: {zone.zoneName ?? defaultName}");
        }

        private void Update()
        {
            // 区域灯光闪烁效果
            UpdateZoneFlicker(nightMarketZone);
            UpdateZoneFlicker(tenementZone);
            UpdateZoneFlicker(dockZone);
        }

        private void UpdateZoneFlicker(ZoneLightingConfig zone)
        {
            if (zone == null || !zone.enableFlicker || zone.zoneLights == null) return;

            float flicker = Mathf.PerlinNoise(Time.time * zone.flickerSpeed, 0f);
            foreach (Light light in zone.zoneLights)
            {
                if (light != null)
                {
                    light.intensity = zone.zoneIntensity * (0.8f + 0.2f * flicker);
                }
            }
        }

        /// <summary>
        /// 创建默认灯光配置（用于初始化）
        /// </summary>
        [ContextMenu("Create Default Profiles")]
        public void CreateDefaultProfiles()
        {
            // 白天配置
            dayProfile = new LightingProfile
            {
                profileName = "Daytime",
                skyColor = new Color(0.5f, 0.5f, 0.5f),
                equatorColor = new Color(0.4f, 0.4f, 0.4f),
                groundColor = new Color(0.2f, 0.2f, 0.2f),
                ambientIntensity = 1.2f,
                fogDensity = 0.001f,
                fogColor = new Color(0.7f, 0.7f, 0.6f),
                sunRotation = new Vector3(70f, -30f, 0f),
                sunColor = new Color(1f, 0.95f, 0.8f),
                sunIntensity = 2f
            };

            // 傍晚配置（默认，写实黑帮氛围）
            eveningProfile = new LightingProfile
            {
                profileName = "Evening",
                skyColor = new Color(0.3f, 0.25f, 0.2f),
                equatorColor = new Color(0.25f, 0.2f, 0.15f),
                groundColor = new Color(0.1f, 0.08f, 0.06f),
                ambientIntensity = 0.8f,
                fogDensity = 0.002f,
                fogColor = new Color(0.3f, 0.25f, 0.2f),
                sunRotation = new Vector3(15f, -60f, 0f),
                sunColor = new Color(1f, 0.6f, 0.3f),
                sunIntensity = 0.8f
            };

            // 夜间配置
            nightProfile = new LightingProfile
            {
                profileName = "Night",
                skyColor = new Color(0.05f, 0.05f, 0.1f),
                equatorColor = new Color(0.03f, 0.03f, 0.08f),
                groundColor = new Color(0.01f, 0.01f, 0.02f),
                ambientIntensity = 0.3f,
                fogDensity = 0.003f,
                fogColor = new Color(0.1f, 0.1f, 0.15f),
                sunRotation = new Vector3(-20f, -30f, 0f),
                sunColor = new Color(0.2f, 0.2f, 0.3f),
                sunIntensity = 0.1f
            };

            // 区域灯光配置
            nightMarketZone = new ZoneLightingConfig
            {
                zoneName = "NightMarket",
                zoneColor = new Color(1f, 0.3f, 0.6f), // 霓虹粉红
                zoneIntensity = 2f,
                enableFlicker = true,
                flickerSpeed = 2f
            };

            tenementZone = new ZoneLightingConfig
            {
                zoneName = "Tenement",
                zoneColor = new Color(1f, 0.7f, 0.3f), // 暖黄路灯
                zoneIntensity = 1.5f,
                enableFlicker = false
            };

            dockZone = new ZoneLightingConfig
            {
                zoneName = "Dock",
                zoneColor = new Color(0.5f, 0.6f, 1f), // 冷白
                zoneIntensity = 1.2f,
                enableFlicker = false
            };

            Debug.Log("[LightingMaster] Default profiles created");
        }
    }
}
