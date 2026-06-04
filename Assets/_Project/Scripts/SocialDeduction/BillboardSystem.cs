using UnityEngine;
using System.Collections.Generic;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 广告牌与霓虹招牌系统
    /// 管理建筑顶部广告牌和夜市霓虹招牌闪烁效果
    /// </summary>
    public class BillboardSystem : MonoBehaviour
    {
        [Header("Rooftop Billboards")]
        [SerializeField] private BillboardConfig[] rooftopBillboards;
        [SerializeField] private float rooftopHeightOffset = 15f;

        [Header("NightMarket Neon Signs")]
        [SerializeField] private NeonSignConfig[] neonSigns;
        [SerializeField] private Transform nightMarketCenter;
        [SerializeField] private float nightMarketRadius = 30f;
        [SerializeField] private int neonSignCount = 12;

        [Header("Common Settings")]
        [SerializeField] private bool enableFlickerEffect = true;
        [SerializeField] private float globalFlickerIntensity = 1f;

        private List<NeonSignInstance> neonInstances = new List<NeonSignInstance>();

        [System.Serializable]
        public class BillboardConfig
        {
            public string billboardName;
            public GameObject billboardPrefab;
            public Vector3 position;
            public float width = 5f;
            public float height = 2.5f;
            public Color mainColor = Color.white;
            [Range(0f, 5f)]
            public float emissiveIntensity = 1f;
        }

        [System.Serializable]
        public class NeonSignConfig
        {
            public string signName;
            public string signText;
            public Color neonColor = Color.red;
            [Range(0.5f, 3f)]
            public float flickerSpeed = 1.5f;
            [Range(0f, 1f)]
            public float flickerAmount = 0.3f;
            public Vector2 signSize = new Vector2(2f, 0.6f);
        }

        private class NeonSignInstance
        {
            public GameObject signObject;
            public NeonSignConfig config;
            public Light neonLight;
            public MeshRenderer meshRenderer;
            public Material material;
            public float baseOffset;
        }

        private void Awake()
        {
            CreateRooftopBillboards();
            CreateNeonSigns();
        }

        /// <summary>
        /// 创建建筑顶部广告牌
        /// </summary>
        private void CreateRooftopBillboards()
        {
            if (rooftopBillboards == null || rooftopBillboards.Length == 0)
            {
                Debug.Log("[BillboardSystem] No rooftop billboard configs, creating defaults");

                rooftopBillboards = new BillboardConfig[]
                {
                    new BillboardConfig
                    {
                        billboardName = "Gambling_Ad",
                        position = new Vector3(10f, rooftopHeightOffset, 5f),
                        width = 6f, height = 3f,
                        mainColor = new Color(1f, 0.1f, 0.1f),
                        emissiveIntensity = 1.5f
                    },
                    new BillboardConfig
                    {
                        billboardName = "Liquor_Ad",
                        position = new Vector3(-15f, rooftopHeightOffset, -10f),
                        width = 5f, height = 2.5f,
                        mainColor = new Color(0.1f, 1f, 0.3f),
                        emissiveIntensity = 1.2f
                    },
                    new BillboardConfig
                    {
                        billboardName = "NightClub_Ad",
                        position = new Vector3(-5f, rooftopHeightOffset, 20f),
                        width = 7f, height = 3f,
                        mainColor = new Color(0.8f, 0.1f, 1f),
                        emissiveIntensity = 2f
                    }
                };
            }

            foreach (BillboardConfig config in rooftopBillboards)
            {
                GameObject billboard;
                if (config.billboardPrefab != null)
                {
                    billboard = Instantiate(config.billboardPrefab, config.position, Quaternion.identity, transform);
                }
                else
                {
                    billboard = CreateBillboardPlane(config);
                }

                billboard.name = $"Billboard_{config.billboardName}";

                // 添加点光源照亮广告牌
                GameObject lightObj = new GameObject("BillboardLight");
                lightObj.transform.SetParent(billboard.transform);
                lightObj.transform.localPosition = new Vector3(0f, 0f, config.width * 0.6f);

                Light billboardLight = lightObj.AddComponent<Light>();
                billboardLight.type = LightType.Point;
                billboardLight.color = config.mainColor;
                billboardLight.intensity = config.emissiveIntensity * 3f;
                billboardLight.range = config.width * 1.5f;
                billboardLight.shadows = LightShadows.None;

                Debug.Log($"[BillboardSystem] Created billboard: {config.billboardName} at {config.position}");
            }
        }

        private GameObject CreateBillboardPlane(BillboardConfig config)
        {
            GameObject go = new GameObject(config.billboardName);
            go.transform.SetParent(transform);
            go.transform.position = config.position;

            // 支撑结构
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(go.transform);
            pole.transform.localPosition = new Vector3(0f, -config.height * 0.7f, 0f);
            pole.transform.localScale = new Vector3(0.15f, config.height * 0.7f, 0.15f);
            DestroyImmediate(pole.GetComponent<CapsuleCollider>());
            pole.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.2f, 0.2f, 0.2f)
            };

            // 广告牌面板
            GameObject signPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            signPlane.name = "SignPlane";
            signPlane.transform.SetParent(go.transform);
            signPlane.transform.localPosition = Vector3.zero;
            signPlane.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            signPlane.transform.localScale = new Vector3(config.width, config.height, 1f);
            DestroyImmediate(signPlane.GetComponent<MeshCollider>());

            MeshRenderer mr = signPlane.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = config.mainColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", config.mainColor * config.emissiveIntensity);
            mat.SetFloat("_Glossiness", 0.2f);
            mr.material = mat;

            return go;
        }

        /// <summary>
        /// 创建夜市霓虹招牌
        /// </summary>
        private void CreateNeonSigns()
        {
            if (neonSigns == null || neonSigns.Length == 0)
            {
                Debug.Log("[BillboardSystem] No neon configs, creating defaults");

                neonSigns = new NeonSignConfig[]
                {
                    new NeonSignConfig
                    {
                        signName = "Dragon_Palace", signText = "龙宫",
                        neonColor = new Color(1f, 0.1f, 0.3f), flickerSpeed = 2f, flickerAmount = 0.3f
                    },
                    new NeonSignConfig
                    {
                        signName = "Golden_Phoenix", signText = "金凤",
                        neonColor = new Color(1f, 0.8f, 0.1f), flickerSpeed = 1.5f, flickerAmount = 0.2f
                    },
                    new NeonSignConfig
                    {
                        signName = "Night_Bar", signText = "夜吧",
                        neonColor = new Color(0.1f, 0.8f, 1f), flickerSpeed = 1.8f, flickerAmount = 0.25f
                    },
                    new NeonSignConfig
                    {
                        signName = "Casino", signText = "娱乐",
                        neonColor = new Color(0.9f, 0.1f, 0.8f), flickerSpeed = 2.5f, flickerAmount = 0.35f
                    },
                    new NeonSignConfig
                    {
                        signName = "Tea_House", signText = "茶楼",
                        neonColor = new Color(0.2f, 1f, 0.3f), flickerSpeed = 1.2f, flickerAmount = 0.15f
                    },
                    new NeonSignConfig
                    {
                        signName = "Medicine_Hall", signText = "药铺",
                        neonColor = new Color(1f, 0.5f, 0.1f), flickerSpeed = 1f, flickerAmount = 0.1f
                    }
                };
            }

            Vector3 center = nightMarketCenter != null ? nightMarketCenter.position : Vector3.zero;

            for (int i = 0; i < neonSignCount; i++)
            {
                NeonSignConfig config = neonSigns[i % neonSigns.Length];

                float angle = (360f / neonSignCount) * i;
                Vector3 position = center + new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * nightMarketRadius,
                    Random.Range(3f, 8f),
                    Mathf.Sin(angle * Mathf.Deg2Rad) * nightMarketRadius
                );

                CreateNeonSign(config, position, i);
            }

            Debug.Log($"[BillboardSystem] Created {neonInstances.Count} neon signs");
        }

        private void CreateNeonSign(NeonSignConfig config, Vector3 position, int index)
        {
            GameObject signObj = new GameObject($"NeonSign_{config.signName}_{index}");
            signObj.transform.SetParent(transform);
            signObj.transform.position = position;

            // 招牌面板
            GameObject signPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            signPlane.name = "SignPlane";
            signPlane.transform.SetParent(signObj.transform);
            signPlane.transform.localPosition = Vector3.zero;
            signPlane.transform.localScale = new Vector3(config.signSize.x, config.signSize.y, 1f);
            DestroyImmediate(signPlane.GetComponent<MeshCollider>());

            MeshRenderer mr = signPlane.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = config.neonColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", config.neonColor * 2f);
            mat.SetFloat("_Glossiness", 0.8f);
            mr.material = mat;

            // 霓虹光源
            GameObject lightObj = new GameObject("NeonLight");
            lightObj.transform.SetParent(signObj.transform);
            lightObj.transform.localPosition = new Vector3(0f, 0f, signObj.transform.localScale.x * 0.5f);

            Light neonLight = lightObj.AddComponent<Light>();
            neonLight.type = LightType.Point;
            neonLight.color = config.neonColor;
            neonLight.intensity = 3f;
            neonLight.range = 5f;
            neonLight.shadows = LightShadows.None;

            NeonSignInstance instance = new NeonSignInstance
            {
                signObject = signObj,
                config = config,
                neonLight = neonLight,
                meshRenderer = mr,
                material = mat,
                baseOffset = Random.Range(0f, 100f)
            };

            neonInstances.Add(instance);
        }

        private void Update()
        {
            if (!enableFlickerEffect) return;

            // 更新广告牌和霓虹闪烁
            foreach (var instance in neonInstances)
            {
                float noise = Mathf.PerlinNoise(
                    (Time.time + instance.baseOffset) * instance.config.flickerSpeed,
                    0f
                );

                float flicker = 1f - (noise * instance.config.flickerAmount * globalFlickerIntensity);

                if (instance.neonLight != null)
                {
                    instance.neonLight.intensity = 3f * flicker;
                }

                if (instance.material != null)
                {
                    Color emission = instance.config.neonColor * (2f * flicker);
                    instance.material.SetColor("_EmissionColor", emission);
                }
            }
        }

        /// <summary>
        /// 设置全局闪烁强度
        /// </summary>
        public void SetGlobalFlickerIntensity(float intensity)
        {
            globalFlickerIntensity = Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// 切换闪烁效果开关
        /// </summary>
        public void ToggleFlicker(bool enable)
        {
            enableFlickerEffect = enable;
        }

        private void OnDestroy()
        {
            foreach (var instance in neonInstances)
            {
                if (instance.material != null) Destroy(instance.material);
                if (instance.signObject != null) Destroy(instance.signObject);
            }
            neonInstances.Clear();
        }
    }
}