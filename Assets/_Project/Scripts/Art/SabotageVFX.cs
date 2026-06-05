using GanglandUndercover.Online;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// E4 破坏/技能/交互视觉特效系统。
    /// 在各破坏触发器控制 VFX GameObject 的显示/隐藏/颜色/透明度。
    /// 纯程序化，零外部资源依赖。
    /// </summary>
    public sealed class SabotageVFX : MonoBehaviour
    {
        [Header("Overlay Panels")]
        public SpriteRenderer blackoutOverlay;
        public SpriteRenderer lockdownOverlay;
        public SpriteRenderer commJamOverlay;

        [Header("VFX Objects")]
        public GameObject evidenceLeakPulse;
        public GameObject patrolAlertFlash;

        [Header("Colors")]
        public Color blackoutColor     = new Color(0.02f, 0.04f, 0.10f, 0.75f);
        public Color lockdownColor     = new Color(0.20f, 0.08f, 0.06f, 0.60f);
        public Color commJamColor      = new Color(0.15f, 0.15f, 0.20f, 0.50f);
        public Color evidenceLeakColor = new Color(0.60f, 0.10f, 0.10f, 0.80f);
        public Color patrolAlertColor  = new Color(0.90f, 0.45f, 0.10f, 0.70f);

        private OnlineMatchController _ctrl;
        private float _pulseTimer;

        public void Bind(OnlineMatchController ctrl)
        {
            _ctrl = ctrl;
            EnsureVFXObjects();
        }

        public void EnsureVFXObjects()
        {
            if (_ctrl == null) return;

            // 停电遮罩
            if (blackoutOverlay == null)
            {
                var go = CreateOverlayQuad("BlackoutOverlay", blackoutColor);
                blackoutOverlay = go.GetComponent<SpriteRenderer>();
            }

            // 封锁标记
            if (lockdownOverlay == null)
            {
                var go = CreateOverlayQuad("LockdownOverlay", lockdownColor);
                lockdownOverlay = go.GetComponent<SpriteRenderer>();
            }

            // 通讯干扰
            if (commJamOverlay == null)
            {
                var go = CreateOverlayQuad("CommJamOverlay", commJamColor);
                commJamOverlay = go.GetComponent<SpriteRenderer>();
            }
        }

        private GameObject CreateOverlayQuad(string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Camera.main?.transform ?? transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(
                new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point
                },
                new Rect(0, 0, 4, 4),
                new Vector2(0.5f, 0.5f),
                4
            );
            // Fill texture with white for color tint to work
            var tex = sr.sprite.texture;
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            sr.color = color;
            sr.sortingOrder = 500; // Above everything
            go.transform.localPosition = new Vector3(0, 0, -8f);
            go.transform.localScale = new Vector3(40f, 30f, 1f);
            go.SetActive(false);
            return go;
        }

        private void LateUpdate()
        {
            if (_ctrl == null) return;

            // 读取控制器公开的破坏计时器
            bool blackout    = _ctrl.BlackoutTimer > 0f;
            bool lockdown    = _ctrl.LockdownTimer > 0f;
            bool commJam     = _ctrl.CommunicationJamTimer > 0f;
            bool evidenceLeak= _ctrl.EvidenceLeakTimer > 0f;
            bool patrol      = _ctrl.PatrolAlertTimer > 0f;

            SetOverlay(blackoutOverlay, blackout);
            SetOverlay(lockdownOverlay, lockdown);
            SetOverlay(commJamOverlay, commJam);

            // 脉冲效果
            if (evidenceLeak)
            {
                _pulseTimer += Time.deltaTime;
                float pulse = Mathf.Abs(Mathf.Sin(_pulseTimer * 4f));
                if (blackoutOverlay != null)
                    blackoutOverlay.color = new Color(evidenceLeakColor.r, evidenceLeakColor.g, evidenceLeakColor.b, pulse * 0.5f);
            }
        }

        private void SetOverlay(SpriteRenderer sr, bool active)
        {
            if (sr != null) sr.gameObject.SetActive(active);
        }
    }
}
