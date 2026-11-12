using UnityEngine;
using UnityEngine.Rendering;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 批次 6c：全屏调色（内置管线 CommandBuffer 后处理）。
    /// 在主相机 AfterEverything 阶段把 CameraTarget 经 Gangland/ScreenGrade 材质
    /// blit 一个来回：暗角 + 轻对比 + 轻饱和 + 微暖调。
    /// 自动寻找 Camera.main 并幂等绑定（相机切换会自动重绑）；
    /// shader 缺失时告警一次并静默停用，不影响对局。
    /// </summary>
    public sealed class ScreenGradeController : MonoBehaviour
    {
        public const string ShaderName = "Gangland/ScreenGrade";

        // 可调参数（与 shader Properties 同名同义，运行时可热调）
        [Range(0f, 1f)] public float vignetteStrength = 0.30f;
        [Range(0f, 1f)] public float vignetteStart = 0.45f;
        [Range(0f, 0.5f)] public float contrast = 0.13f;
        [Range(0f, 0.5f)] public float saturation = 0.14f;
        [Range(0f, 0.2f)] public float warmTint = 0.045f;

        private static Shader _gradeShader;
        private Material _material;
        private CommandBuffer _commandBuffer;
        private Camera _boundCamera;
        private bool _warnedMissingShader;

        /// <summary>确保场景里恰好一个 ScreenGradeController（幂等）。</summary>
        public static ScreenGradeController Ensure(GameObject host)
        {
            ScreenGradeController existing = FindFirstObjectByType<ScreenGradeController>();
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject("ScreenGrade");
            if (host != null)
            {
                go.transform.SetParent(host.transform, false);
            }
            return go.AddComponent<ScreenGradeController>();
        }

        /// <summary>当前已绑定的相机（测试用）。</summary>
        public Camera BoundCamera => _boundCamera;

        /// <summary>立即绑定指定相机（批处理截图工具用；运行时 LateUpdate 自动绑 Camera.main）。</summary>
        public void BindNow(Camera camera)
        {
            Bind(camera);
        }

        /// <summary>调色材质是否就绪（测试用）。</summary>
        public bool MaterialReady => _material != null;

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == _boundCamera)
            {
                return;
            }
            Bind(cam);
        }

        private void Bind(Camera cam)
        {
            Unbind();
            if (cam == null)
            {
                return;
            }
            if (!EnsureMaterial())
            {
                return;
            }

            _boundCamera = cam;

            int tempId = Shader.PropertyToID("GanglandScreenGradeTemp");
            _commandBuffer = new CommandBuffer { name = "Gangland ScreenGrade" };
            _commandBuffer.GetTemporaryRT(tempId, -1, -1, 0, FilterMode.Point, RenderTextureFormat.Default);
            _commandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, (RenderTargetIdentifier)tempId);
            _commandBuffer.Blit((RenderTargetIdentifier)tempId, BuiltinRenderTextureType.CameraTarget, _material);
            _commandBuffer.ReleaseTemporaryRT(tempId);
            cam.AddCommandBuffer(CameraEvent.AfterEverything, _commandBuffer);
        }

        private bool EnsureMaterial()
        {
            if (_material != null)
            {
                return true;
            }

            if (_gradeShader == null)
            {
                // Resources.Load 保证发布构建包含 shader（纯 Shader.Find 字符串引用不会打进包）
                _gradeShader = Resources.Load<Shader>("Shaders/ScreenGrade") ?? Shader.Find(ShaderName);
            }
            if (_gradeShader == null)
            {
                if (!_warnedMissingShader)
                {
                    _warnedMissingShader = true;
                    Debug.LogWarning("[ScreenGrade] shader " + ShaderName + " 未找到，全屏调色停用");
                }
                return false;
            }

            _material = new Material(_gradeShader);
            return true;
        }

        private void Update()
        {
            if (_material == null)
            {
                return;
            }
            _material.SetFloat("_VignetteStrength", vignetteStrength);
            _material.SetFloat("_VignetteStart", vignetteStart);
            _material.SetFloat("_Contrast", contrast);
            _material.SetFloat("_Saturation", saturation);
            _material.SetFloat("_WarmTint", warmTint);
        }

        private void Unbind()
        {
            if (_boundCamera != null && _commandBuffer != null)
            {
                _boundCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, _commandBuffer);
            }
            if (_commandBuffer != null)
            {
                _commandBuffer.Dispose();
                _commandBuffer = null;
            }
            _boundCamera = null;
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
