using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 天气与大气氛围控制器
    /// 管理雾效、地面雾、飘尘粒子等视觉氛围效果
    /// </summary>
    public class WeatherController : MonoBehaviour
    {
        [Header("Fog Settings")]
        [SerializeField] private bool enableGlobalFog = true;
        [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
        [SerializeField] private float fogDensity = 0.002f;
        [SerializeField] private Color fogColor = new Color(0.25f, 0.22f, 0.2f);

        [Header("Ground Fog")]
        [SerializeField] private bool enableGroundFog = true;
        [SerializeField] private float groundFogHeight = 2f;
        [SerializeField] private float groundFogDensity = 0.008f;
        [SerializeField] private Color groundFogColor = new Color(0.3f, 0.28f, 0.25f, 0.6f);
        [SerializeField] private Material groundFogMaterial;

        [Header("Dust Particles")]
        [SerializeField] private bool enableDustParticles = true;
        [SerializeField] private int dustParticleCount = 200;
        [SerializeField] private Vector3 dustAreaSize = new Vector3(100f, 5f, 100f);
        [SerializeField] private float dustSize = 0.02f;
        [SerializeField] private Color dustColor = new Color(0.6f, 0.55f, 0.5f, 0.4f);
        [SerializeField] private float dustSpeed = 0.5f;

        [Header("Sky Settings")]
        [SerializeField] private Color skyColor = new Color(0.2f, 0.18f, 0.15f);
        [SerializeField] private GameObject skyboxPlane;

        private ParticleSystem dustParticleSystem;
        private GameObject groundFogQuad;

        private void Awake()
        {
            ApplyFogSettings();
            InitializeGroundFog();
            InitializeDustParticles();
            SetupSkyBackground();
        }

        private void ApplyFogSettings()
        {
            RenderSettings.fog = enableGlobalFog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;
        }

        private void InitializeGroundFog()
        {
            if (!enableGroundFog) return;

            groundFogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            groundFogQuad.name = "GroundFogPlane";
            Destroy(groundFogQuad.GetComponent<MeshCollider>());

            // 平放在地面的大面片
            groundFogQuad.transform.position = new Vector3(0f, groundFogHeight * 0.5f, 0f);
            groundFogQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            groundFogQuad.transform.localScale = new Vector3(200f, 200f, 1f);
            groundFogQuad.transform.SetParent(transform);

            MeshRenderer renderer = groundFogQuad.GetComponent<MeshRenderer>();

            if (groundFogMaterial != null)
            {
                renderer.material = groundFogMaterial;
            }
            else
            {
                // 使用透明 Shader 创建地面雾材质
                Shader fogShader = Shader.Find("Unlit/Transparent");
                if (fogShader == null) fogShader = Shader.Find("Standard");
                Material mat = new Material(fogShader);
                Color fogColorWithDensity = groundFogColor;
                fogColorWithDensity.a = Mathf.Clamp01(groundFogColor.a * Mathf.Max(0f, groundFogDensity * 125f));
                mat.color = fogColorWithDensity;
                mat.SetFloat("_Mode", 3); // Transparent
                renderer.material = mat;
                groundFogMaterial = mat;
            }

            Debug.Log("[WeatherController] Ground fog initialized");
        }

        private void InitializeDustParticles()
        {
            if (!enableDustParticles) return;

            GameObject dustObj = new GameObject("DustParticles");
            dustObj.transform.SetParent(transform);
            dustObj.transform.position = Vector3.zero;

            dustParticleSystem = dustObj.AddComponent<ParticleSystem>();

            var main = dustParticleSystem.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, dustSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(dustSize, dustSize * 2f);
            main.startColor = dustColor;
            main.maxParticles = dustParticleCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.loop = true;

            var emission = dustParticleSystem.emission;
            emission.rateOverTime = dustParticleCount / 5f;

            var shape = dustParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = dustAreaSize;

            var colorOverLifetime = dustParticleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(dustColor, 0f),
                    new GradientColorKey(new Color(dustColor.r, dustColor.g, dustColor.b, 0f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.1f, 0f),
                    new GradientAlphaKey(0.3f, 0.2f),
                    new GradientAlphaKey(0.2f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var noise = dustParticleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.1f;

            var renderer = dustParticleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = new Material(Shader.Find("Unlit/Transparent"));

            Debug.Log($"[WeatherController] Dust particles initialized ({dustParticleCount} particles)");
        }

        private void SetupSkyBackground()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.backgroundColor = skyColor;
                mainCam.clearFlags = CameraClearFlags.SolidColor;
            }

            if (skyboxPlane != null)
            {
                skyboxPlane.transform.SetParent(Camera.main?.transform);
                skyboxPlane.transform.localPosition = new Vector3(0f, 0f, 500f);
                skyboxPlane.transform.localRotation = Quaternion.identity;
            }

            Debug.Log("[WeatherController] Sky background configured");
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ApplyFogSettings();

                if (Camera.main != null)
                {
                    Camera.main.backgroundColor = skyColor;
                }
            }
        }

        /// <summary>
        /// 动态更新雾密度（用于天气过渡）
        /// </summary>
        public void SetFogDensity(float density)
        {
            fogDensity = density;
            RenderSettings.fogDensity = density;
        }

        /// <summary>
        /// 平滑过渡雾密度
        /// </summary>
        public System.Collections.IEnumerator LerpFogDensity(float targetDensity, float duration)
        {
            float startDensity = fogDensity;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                SetFogDensity(Mathf.Lerp(startDensity, targetDensity, t));
                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (groundFogQuad != null) Destroy(groundFogQuad);
            if (dustParticleSystem != null) Destroy(dustParticleSystem.gameObject);
        }
    }
}
