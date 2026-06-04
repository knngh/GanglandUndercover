using UnityEngine;
using GanglandUndercover.SocialDeduction;

namespace GanglandUndercover.Gameplay
{
    /// <summary>
    /// 鬼魂模式组件 — 玩家被淘汰后进入鬼魂状态。
    /// 
    /// 行为：
    /// 1. 半透明渲染（材质 alpha = 0.35）
    /// 2. 碰撞器设为 trigger（可穿越墙壁）
    /// 3. 自由飞行移动（忽略碰撞）
    /// 4. 可以继续做任务帮助队友
    /// 5. 无法被活着的玩家看到（通过渲染层级/透明度实现）
    /// 6. 无法报告尸体或发起会议
    /// 
    /// 挂载到玩家 GameObject 上，由 SocialPrototypeController / OnlineMatchController
    /// 在淘汰时调用 EnterGhostMode() 激活。
    /// </summary>
    public sealed class GhostMode : MonoBehaviour
    {
        // ─── 配置 ──────────────────────────────────────────
        [Header("Ghost Visuals")]
        [Tooltip("鬼魂透明度（0=全透明，1=不透明）")]
        [Range(0f, 1f)]
        public float GhostAlpha = 0.35f;

        [Tooltip("鬼魂是否显示轮廓光晕")]
        public bool ShowGhostHalo = true;

        [Header("Movement")]
        [Tooltip("鬼魂移动速度倍率（可穿墙）")]
        public float GhostMoveSpeedMultiplier = 1.2f;

        [Tooltip("鬼魂飞行高度偏移（Z轴）")]
        public float GhostZOffset = 0.6f;

        [Header("Interaction")]
        [Tooltip("鬼魂是否可以继续做任务")]
        public bool CanDoTasks = true;

        [Tooltip("鬼魂是否可以报告尸体（通常 false）")]
        public bool CanReportBody = false;

        [Tooltip("鬼魂是否可以发起会议（通常 false）")]
        public bool GhostCanCallMeeting = false;

        // ─── 运行时状态 ────────────────────────────────────
        private bool _isGhost;
        private SocialCharacter _character;
        private Collider2D _collider2D;
        private Collider _collider3D;
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private float _originalMoveSpeed;
        private const string GhostLayerName = "Ghost"; // 需要在项目中创建此 Layer

        // ─── 公共属性 ──────────────────────────────────────
        public bool IsGhost => _isGhost;

        // ─── 生命周期 ──────────────────────────────────────
        private void Awake()
        {
            _character = GetComponent<SocialCharacter>();
            _collider2D = GetComponent<Collider2D>();
            _collider3D = GetComponent<Collider>();
            CacheRenderers();
        }

        // ─── 公共接口 ──────────────────────────────────────

        /// <summary>
        /// 进入鬼魂模式。
        /// </summary>
        public void EnterGhostMode()
        {
            if (_isGhost) return;
            _isGhost = true;

            // 1. 半透明渲染
            ApplyGhostVisuals();

            // 2. 碰撞器设为 trigger（可穿越墙壁）
            EnableGhostCollision();

            // 3. 提升 Z 轴（飞行感）
            Vector3 pos = transform.position;
            transform.position = new Vector3(pos.x, pos.y, pos.z + GhostZOffset);

            // 4. 加快移动速度
            if (_character != null)
            {
                _originalMoveSpeed = _character.MoveSpeed;
                _character.MoveSpeed = _originalMoveSpeed * GhostMoveSpeedMultiplier;
            }

            // 5. 设置 Layer（让活着的人看不到）
            // 需要在 Unity 中设置 Layer 碰撞矩阵
            // gameObject.layer = LayerMask.NameToLayer(GhostLayerName);

            Debug.Log($"[GhostMode] {gameObject.name} 进入鬼魂模式。");
        }

        /// <summary>
        /// 退出鬼魂模式（通常不需要，但保留接口）。
        /// </summary>
        public void ExitGhostMode()
        {
            if (!_isGhost) return;
            _isGhost = false;

            RestoreVisuals();
            RestoreCollision();
            RestoreMoveSpeed();

            Debug.Log($"[GhostMode] {gameObject.name} 退出鬼魂模式。");
        }

        /// <summary>
        /// 判断本地玩家是否能看到此鬼魂。
        /// 活着的人看不到鬼魂，只有同样死亡的玩家能看到。
        /// </summary>
        public static bool CanSeeGhost(SocialCharacter viewer, SocialCharacter ghost)
        {
            if (viewer == null || ghost == null) return false;
            if (!ghost.IsAlive) return true;  // 鬼魂本身
            return !viewer.IsAlive; // 只有死亡玩家能看到鬼魂
        }

        // ─── 内部方法 ──────────────────────────────────────

        private void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material != null)
                {
                    _originalColors[i] = _renderers[i].material.color;
                }
            }
        }

        private void ApplyGhostVisuals()
        {
            foreach (Renderer r in _renderers)
            {
                if (r == null) continue;
                Color c = r.material.color;
                c.a = GhostAlpha;
                r.material.color = c;
            }

            if (ShowGhostHalo)
            {
                // 可选：添加光晕效果
                AddGhostHalo();
            }
        }

        private void RestoreVisuals()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _originalColors[i] != default)
                {
                    _renderers[i].material.color = _originalColors[i];
                }
            }
        }

        private void EnableGhostCollision()
        {
            if (_collider2D != null)
            {
                _collider2D.isTrigger = true;
            }
            if (_collider3D != null)
            {
                _collider3D.isTrigger = true;
            }
        }

        private void RestoreCollision()
        {
            if (_collider2D != null)
            {
                _collider2D.isTrigger = false;
            }
            if (_collider3D != null)
            {
                _collider3D.isTrigger = false;
            }
        }

        private void RestoreMoveSpeed()
        {
            if (_character != null)
            {
                _character.MoveSpeed = _originalMoveSpeed;
            }
        }

        private void AddGhostHalo()
        {
            // 可选实现：添加 Light 组件或 Shader 效果
            Light halo = gameObject.AddComponent<Light>();
            halo.type = LightType.Point;
            halo.color = new Color(0.5f, 0.7f, 1f, 1f);
            halo.intensity = 0.6f;
            halo.range = 2.5f;
            halo.renderMode = LightRenderMode.ForcePixel;
        }

        // ─── 交互权限判断（供外部调用）─────────────────────

        /// <summary>鬼魂是否可以执行交互（任务等）。</summary>
        public bool CanInteract() => _isGhost && CanDoTasks;

        /// <summary>鬼魂是否可以报告尸体。</summary>
        public bool CanReport() => _isGhost && CanReportBody;

        /// <summary>鬼魂是否可以发起会议。</summary>
        public bool CanCallMeeting() => _isGhost && GhostCanCallMeeting;
    }
}
