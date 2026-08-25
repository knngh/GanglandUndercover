using GanglandUndercover.Online;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// E5 破坏/技能/交互视觉特效系统（增强版）。
    ///
    /// 特效层级（sortingOrder）：
    ///   499 — 证据泄露脉冲（底层）
    ///   500 — 停电遮罩
    ///   501 — 锁门红叉
    ///   502 — 通讯干扰 glitch
    ///   503 — 巡逻警报闪烁
    ///   505 — 停电应急红灯
    ///   504 — 击杀溅血（最顶层）
    ///   506 — 命中冲击闪光
    ///
    /// 优先使用 Resources/Sprites/VFX/ 帧动画，加载失败自动回退程序化。
    /// </summary>
    public sealed class SabotageVFX : MonoBehaviour
    {
        [Header("Overlay Panels")]
        public SpriteRenderer blackoutOverlay;
        public SpriteRenderer lockdownOverlay;
        public SpriteRenderer commJamOverlay;
        public SpriteRenderer evidenceLeakOverlay;
        public SpriteRenderer patrolAlertOverlay;

        [Header("VFX Objects")]
        public GameObject killBloodFX;
        public GameObject meetingAlertFX;

        [Header("Blackout Settings")]
        public Color blackoutColor     = new Color(0.02f, 0.03f, 0.10f, 0.75f);
        public Color emergencyRedColor = new Color(0.90f, 0.10f, 0.05f, 0.40f);

        [Header("Lockdown")]
        public Color lockdownColor     = new Color(0.25f, 0.06f, 0.04f, 0.55f);
        public Color lockdownXColor    = new Color(0.90f, 0.15f, 0.05f, 0.85f);

        [Header("Comm Jam")]
        public Color commJamColor      = new Color(0.05f, 0.08f, 0.15f, 0.55f);
        public Color commJamGlitchColor= new Color(0.30f, 0.50f, 0.70f, 0.30f);

        [Header("Evidence Leak")]
        public Color evidenceLeakColor = new Color(0.70f, 0.08f, 0.08f, 0.70f);

        [Header("Patrol Alert")]
        public Color patrolAlertColor  = new Color(0.95f, 0.45f, 0.08f, 0.60f);

        private OnlineMatchController _ctrl;
        private float _pulseTimer;
        private float _glitchTimer;
        private float _emergencyTimer;
        private float _bloodFadeTimer;
        private bool _wasKillActive;

        // 缓存的 "X" 标记纹理
        private Texture2D _xMarkTex;

        // ── 帧动画 VFX（Resources 加载成功时启用，否则回退程序化）──
        private VFXSheetPlayer _killSheetPlayer;
        private VFXSheetPlayer _blackoutSheet;
        private VFXSheetPlayer _lockdownSheet;
        private VFXSheetPlayer _commJamSheet;
        private VFXSheetPlayer _evidenceLeakSheet;
        private VFXSheetPlayer _patrolAlertSheet;
        private VFXSheetPlayer _emergencyLightSheet;
        private VFXSheetPlayer _hitSheetPlayer;
        private SpriteRenderer _emergencyLightRenderer;
        private GameObject _hitImpactFX;

        public void Bind(OnlineMatchController ctrl)
        {
            _ctrl = ctrl;
            EnsureVFXObjects();
        }

        public void EnsureVFXObjects()
        {
            if (_ctrl == null) return;

            // ── 停电遮罩（深蓝黑+应急红光）──
            if (blackoutOverlay == null)
            {
                var go = CreateOverlayQuad("BlackoutOverlay", blackoutColor, 500);
                blackoutOverlay = go.GetComponent<SpriteRenderer>();
                _emergencyLightRenderer = CreateEmergencyLight(go.transform);
            }
            else if (_emergencyLightRenderer == null)
            {
                Transform emergencyLight = blackoutOverlay.transform.Find("EmergencyRedLight");
                _emergencyLightRenderer = emergencyLight != null
                    ? emergencyLight.GetComponent<SpriteRenderer>()
                    : CreateEmergencyLight(blackoutOverlay.transform);
            }

            // ── 锁门红叉遮罩 ──
            if (lockdownOverlay == null)
            {
                var go = CreateOverlayQuad("LockdownOverlay", lockdownColor, 501);
                lockdownOverlay = go.GetComponent<SpriteRenderer>();
            }

            // ── 通讯干扰 glitch 遮罩 ──
            if (commJamOverlay == null)
            {
                var go = CreateOverlayQuad("CommJamOverlay", commJamColor, 502);
                commJamOverlay = go.GetComponent<SpriteRenderer>();
                // Glitch 用重复纹理实现条状干扰
                commJamOverlay.sprite = CreateGlitchStripSprite();
            }

            // ── 证据泄露脉冲遮罩 ──
            if (evidenceLeakOverlay == null)
            {
                var go = CreateOverlayQuad("EvidenceLeakOverlay", evidenceLeakColor, 499);
                evidenceLeakOverlay = go.GetComponent<SpriteRenderer>();
            }

            // ── 巡逻警报闪烁 ──
            if (patrolAlertOverlay == null)
            {
                var go = CreateOverlayQuad("PatrolAlertOverlay", patrolAlertColor, 503);
                patrolAlertOverlay = go.GetComponent<SpriteRenderer>();
            }

            // ── 帧动画 VFX 初始化（Resources 加载失败则保持 null → 程序化回退）──
            InitOverlaySheet(ref _blackoutSheet, blackoutOverlay, VfxEffectProfile.Blackout);
            InitOverlaySheet(ref _lockdownSheet, lockdownOverlay, VfxEffectProfile.DoorLock);
            InitOverlaySheet(ref _commJamSheet, commJamOverlay, VfxEffectProfile.CommsJam);
            InitOverlaySheet(ref _evidenceLeakSheet, evidenceLeakOverlay, VfxEffectProfile.EvidenceLeak);
            InitOverlaySheet(ref _patrolAlertSheet, patrolAlertOverlay, VfxEffectProfile.PatrolAlert);
            InitOverlaySheet(ref _emergencyLightSheet, _emergencyLightRenderer, VfxEffectProfile.EmergencyLight);

            // 预加载击杀帧（首次触发时不卡顿）
            VFXSheetPlayer.Preload(VfxEffectProfile.Kill.Name);
            VFXSheetPlayer.Preload(VfxEffectProfile.Hit.Name);
        }

        /// <summary>触发击杀溅血 VFX（帧动画优先，3 秒渐隐回退）</summary>
        public void TriggerKillBlood(Vector3 worldPos)
        {
            if (killBloodFX != null) DestroyRuntimeObject(killBloodFX);

            killBloodFX = new GameObject("KillBloodFX");
            killBloodFX.transform.position = worldPos + Vector3.back;

            TriggerHitImpact(worldPos);

            // 尝试帧动画（kill 序列 10 帧 @ 15fps ≈ 0.67s）
            var player = killBloodFX.AddComponent<VFXSheetPlayer>();
            if (player.Init(VfxEffectProfile.Kill.Name, VfxEffectProfile.Kill.PlaybackMode, VfxEffectProfile.Kill.FramesPerSecond))
            {
                player.SetSortingOrder(VfxEffectProfile.Kill.SortingOrder);
                player.Play();
                _killSheetPlayer = player;
            }
            else
            {
                // 回退：程序化 BloodSplatter 静态贴图
                DestroyRuntimeObject(player);
                var sr = killBloodFX.GetComponent<SpriteRenderer>();
                if (sr == null) sr = killBloodFX.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite2DAssetCache.BloodSplatter;
                sr.sortingOrder = VfxEffectProfile.Kill.SortingOrder;
                sr.transform.localScale = Vector3.one * 2f;
            }

            _bloodFadeTimer = 3.0f;
            _wasKillActive = true;
        }

        /// <summary>触发会议警报闪光</summary>
        public void TriggerMeetingAlert()
        {
            if (meetingAlertFX == null)
            {
                meetingAlertFX = CreateOverlayQuad("MeetingAlert", new Color(1f, 0.2f, 0.1f, 0.25f), 503);
            }
            meetingAlertFX.SetActive(true);
            CancelInvoke(nameof(HideMeetingAlert));
            Invoke(nameof(HideMeetingAlert), 2.0f);
        }

        private void HideMeetingAlert()
        {
            if (meetingAlertFX != null) meetingAlertFX.SetActive(false);
        }

        // ── 运行时代理 ──

        private void LateUpdate()
        {
            if (_ctrl == null) return;

            bool blackout    = _ctrl.BlackoutTimer > 0f;
            bool lockdown    = _ctrl.LockdownTimer > 0f;
            bool commJam     = _ctrl.CommunicationJamTimer > 0f;
            bool evidenceLeak= _ctrl.EvidenceLeakTimer > 0f;
            bool patrol      = _ctrl.PatrolAlertTimer > 0f;

            _pulseTimer   += Time.deltaTime;
            _glitchTimer  += Time.deltaTime;

            // ── 停电：深蓝遮罩 + 应急红灯脉冲 ──
            SetOverlay(blackoutOverlay, blackout);
            if (blackout)
            {
                _emergencyTimer += Time.deltaTime;
                float emergencyPulse = Mathf.Abs(Mathf.Sin(_emergencyTimer * 3.5f));
                Color blackoutTint = blackoutColor;
                blackoutTint.a = 0.58f + emergencyPulse * 0.16f;
                ApplySheetAwareColor(
                    blackoutOverlay,
                    _blackoutSheet,
                    Color.Lerp(blackoutOverlay.color, blackoutTint, Time.deltaTime * 4f),
                    0.9f + emergencyPulse * 0.1f);
                SetEmergencyLightPulse(true, emergencyPulse);
            }
            else
            {
                SetEmergencyLightPulse(false, 0f);
            }

            // ── 锁门：暗红遮罩 + 边缘红脉动 ──
            SetOverlay(lockdownOverlay, lockdown);
            if (lockdown)
            {
                float lockdownPulse = Mathf.Abs(Mathf.Sin(_pulseTimer * 2.5f));
                Color lc = lockdownColor;
                lc.a = 0.35f + lockdownPulse * 0.30f;
                ApplySheetAwareColor(
                    lockdownOverlay,
                    _lockdownSheet,
                    Color.Lerp(lockdownOverlay.color, lc, Time.deltaTime * 3f),
                    0.82f + lockdownPulse * 0.18f);
            }

            // ── 通讯干扰：暗蓝遮罩 + glitch 闪烁 + 偏移 ──
            SetOverlay(commJamOverlay, commJam);
            if (commJam)
            {
                // 周期性完全透明闪烁模拟信号中断
                float glitchCycle = Mathf.Sin(_glitchTimer * 12f);
                bool glitchOn = glitchCycle > 0.3f || Mathf.Abs(glitchCycle) < 0.1f;

                Color gc = commJamColor;
                gc.a = glitchOn ? 0.35f : 0.05f;
                ApplySheetAwareColor(
                    commJamOverlay,
                    _commJamSheet,
                    Color.Lerp(commJamOverlay.color, gc, Time.deltaTime * 8f),
                    glitchOn ? 1f : 0.45f);

                // 周期性偏移 glitch
                float xOffset = glitchOn ? Mathf.Sin(_glitchTimer * 47f) * 0.5f : 0f;
                float yOffset = glitchOn ? Mathf.Cos(_glitchTimer * 53f) * 0.3f : 0f;
                commJamOverlay.transform.localPosition = new Vector3(xOffset, yOffset, -9.5f);
            }

            // ── 证据泄露：红色脉冲叠加 ──
            SetOverlay(evidenceLeakOverlay, evidenceLeak);
            if (evidenceLeak)
            {
                float leakPulse = Mathf.Abs(Mathf.Sin(_pulseTimer * 5f));
                Color lk = evidenceLeakColor;
                lk.a = 0.25f + leakPulse * 0.55f;
                ApplySheetAwareColor(
                    evidenceLeakOverlay,
                    _evidenceLeakSheet,
                    Color.Lerp(evidenceLeakOverlay.color, lk, Time.deltaTime * 4f),
                    0.9f + leakPulse * 0.1f);
            }

            // ── 巡逻警报：橙色闪烁（每 1.5 秒闪一次）──
            SetOverlay(patrolAlertOverlay, patrol);
            if (patrol)
            {
                float patrolPhase = Mathf.Repeat(_pulseTimer, 1.5f);
                float patrolAlpha = patrolPhase < 0.2f ? 0.6f : patrolPhase < 0.3f ? 0.1f : 0f;
                Color pc = patrolAlertColor;
                pc.a = patrolAlpha;
                ApplySheetAwareColor(patrolAlertOverlay, _patrolAlertSheet, pc, 1f);

                // 从边缘向中心收缩
                float shrink = patrolPhase < 0.2f ? 0.8f : 1.0f;
                patrolAlertOverlay.transform.localScale = Vector3.Lerp(
                    patrolAlertOverlay.transform.localScale,
                    new Vector3(40f * shrink, 30f * shrink, 1f),
                    Time.deltaTime * 6f);
            }

            // ── 击杀溅血渐隐 ──
            if (_wasKillActive)
            {
                _bloodFadeTimer -= Time.deltaTime;
                if (_bloodFadeTimer <= 0f)
                {
                    if (killBloodFX != null) { DestroyRuntimeObject(killBloodFX); killBloodFX = null; }
                    _wasKillActive = false;
                }
                else if (killBloodFX != null)
                {
                    var sr = killBloodFX.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Color c = sr.color;
                        c.a = Mathf.Clamp01(_bloodFadeTimer / 3.0f);
                        sr.color = c;
                    }
                }
            }
        }

        // ── 内部方法 ──

        private void InitOverlaySheet(ref VFXSheetPlayer player, SpriteRenderer overlay, VfxEffectProfile profile)
        {
            if (overlay == null) return;

            Color fallbackColor = overlay.color;
            Sprite fallbackSprite = overlay.sprite;

            if (player == null)
            {
                player = overlay.GetComponent<VFXSheetPlayer>();
                if (player == null) player = overlay.gameObject.AddComponent<VFXSheetPlayer>();
            }

            if (player.Init(profile.Name, profile.PlaybackMode, profile.FramesPerSecond))
            {
                player.SetSortingOrder(profile.SortingOrder);
                player.SetColor(SheetColor(1f));
                player.Play();
                return;
            }

            if (Application.isPlaying) Destroy(player);
            else DestroyImmediate(player);
            player = null;
            overlay.sprite = fallbackSprite;
            overlay.color = fallbackColor;
            overlay.sortingOrder = profile.SortingOrder;
        }

        private void SetOverlay(SpriteRenderer sr, bool active)
        {
            if (sr != null) sr.gameObject.SetActive(active);
        }

        private static void ApplySheetAwareColor(SpriteRenderer renderer, VFXSheetPlayer player, Color fallbackColor, float sheetAlpha)
        {
            if (renderer == null) return;

            renderer.color = IsSheetBacked(player)
                ? SheetColor(sheetAlpha)
                : fallbackColor;
        }

        private static bool IsSheetBacked(VFXSheetPlayer player)
        {
            return player != null && player.HasFrames;
        }

        private static Color SheetColor(float alpha)
        {
            return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        private GameObject CreateOverlayQuad(string name, Color color, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Camera.main?.transform ?? transform, false);
            var sr = go.AddComponent<SpriteRenderer>();

            // 纯色全屏纹理
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);

            sr.color = color;
            sr.sortingOrder = sortOrder;
            go.transform.localPosition = new Vector3(0, 0, -9f + sortOrder * 0.001f);
            go.transform.localScale = new Vector3(40f, 30f, 1f);
            go.SetActive(false);
            return go;
        }

        /// <summary>创建应急红灯（停电遮罩子对象）</summary>
        private SpriteRenderer CreateEmergencyLight(Transform parent)
        {
            var go = new GameObject("EmergencyRedLight");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();

            // 红色发光圆形纹理
            int sz = 128;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float cx = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cx) * (y - cx));
                    float a = d < cx * 0.8f ? Mathf.Clamp01(1f - d / (cx * 0.8f)) : 0f;
                    a *= 0.4f;
                    tex.SetPixel(x, y, new Color(0.9f, 0.15f, 0.05f, a));
                }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 32);
            sr.sortingOrder = 505;
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * 20f;
            return sr;
        }

        private void SetEmergencyLightPulse(bool visible, float pulse)
        {
            if (_emergencyLightRenderer == null) return;

            _emergencyLightRenderer.enabled = visible;
            if (!visible) return;

            Color em = emergencyRedColor;
            em.a = 0.18f + pulse * 0.42f;
            ApplySheetAwareColor(_emergencyLightRenderer, _emergencyLightSheet, em, 0.65f + pulse * 0.35f);
            float scale = 18f + pulse * 5f;
            _emergencyLightRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>创建 glitch 条纹纹理</summary>
        private Sprite CreateGlitchStripSprite()
        {
            int sz = 64;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var prng = new System.Random(42);

            for (int y = 0; y < sz; y++)
            {
                // 随机水平条纹
                bool stripe = prng.NextDouble() < 0.15f;
                Color col = stripe
                    ? (prng.NextDouble() < 0.3f
                        ? new Color(0.3f, 0.5f, 0.8f, 0.25f)  // 蓝条纹
                        : new Color(0.8f, 0.2f, 0.3f, 0.15f))  // 红条纹
                    : new Color(0, 0, 0, 0.03f);                // 半透明底噪

                for (int x = 0; x < sz; x++)
                    tex.SetPixel(x, y, col);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 4);
        }

        private void TriggerHitImpact(Vector3 worldPos)
        {
            if (_hitImpactFX != null) DestroyRuntimeObject(_hitImpactFX);

            _hitImpactFX = new GameObject("HitImpactFX");
            _hitImpactFX.transform.position = worldPos + Vector3.back * 1.05f;

            var player = _hitImpactFX.AddComponent<VFXSheetPlayer>();
            if (player.Init(VfxEffectProfile.Hit.Name, VfxEffectProfile.Hit.PlaybackMode, VfxEffectProfile.Hit.FramesPerSecond))
            {
                player.SetSortingOrder(VfxEffectProfile.Hit.SortingOrder);
                player.Play();
                player.OnComplete += ClearHitImpact;
                _hitSheetPlayer = player;
                return;
            }

            DestroyRuntimeObject(player);
            DestroyRuntimeObject(_hitImpactFX);
            _hitImpactFX = null;
            _hitSheetPlayer = null;
        }

        private void ClearHitImpact()
        {
            if (_hitImpactFX != null)
            {
                DestroyRuntimeObject(_hitImpactFX);
                _hitImpactFX = null;
            }

            _hitSheetPlayer = null;
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
