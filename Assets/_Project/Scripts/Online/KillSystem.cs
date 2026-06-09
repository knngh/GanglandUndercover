using System;
using System.Collections;
using System.Collections.Generic;
using GanglandUndercover.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 击杀系统（单一数据源）：
    /// 持有击杀冷却、尸体、报告冷却等全部击杀相关状态。
    /// 本地 Gang/Mole 靠近目标时显示击杀按钮，管理冷却与特效。
    /// </summary>
    public sealed class KillSystem : MonoBehaviour
    {
        // -------- 配置 --------
        [Header("距离与冷却")]
        [Tooltip("显示击杀按钮的最大距离（米）")]
        public float killRange = 1.5f;

        [Tooltip("击杀冷却时间（秒）")]
        public float killCooldownSeconds = 18f;

        [Header("UI 引用（自动查找 Canvas/KillButton）")]
        [SerializeField] private Button killButton;
        [SerializeField] private GameObject killButtonRoot;
        [SerializeField] private Text killButtonLabel;
        [SerializeField] private Image killCooldownFill;
        [SerializeField] private Text cooldownText;

        [Header("效果预制体")]
        [Tooltip("击杀时生成的血迹效果")]
        [SerializeField] private GameObject bloodEffectPrefab;

        [Header("屏幕闪光配置")]
        [Tooltip("闪光持续时间（秒）")]
        [Range(0.1f, 1f)]
        [SerializeField] private float flashDuration = 0.35f;

        [Tooltip("闪光峰值透明度")]
        [Range(0.1f, 0.6f)]
        [SerializeField] private float flashPeakAlpha = 0.35f;

        // ══════════════════════════════════════════════════════
        // 击杀/尸体/报告状态（整个对战系统的唯一数据源）
        // ══════════════════════════════════════════════════════
        internal readonly Dictionary<ulong, float> killCooldowns = new Dictionary<ulong, float>();
        internal readonly List<OnlineBodyState> bodies = new List<OnlineBodyState>();
        internal readonly Dictionary<int, GameObject> bodyVisuals = new Dictionary<int, GameObject>();
        internal int nextBodyId;
        internal float reportCooldownTimer;
        internal int killCount;

        // -------- 本地击杀 UI 状态 --------
        private OnlineMatchController controller;
        private OnlineMatchHud hud;
        private float currentCooldown;
        private bool isOnCooldown;
        private OnlinePlayerState currentVictim;
        private ulong currentVictimId;
        private List<ulong> _trackedBodies;
        private Image _flashImage;
        private Canvas _flashCanvas;

        // -------- 生命周期 --------
        private void Awake()
        {
            controller = FindAnyObjectByType<OnlineMatchController>();
            hud = FindAnyObjectByType<OnlineMatchHud>();
            _trackedBodies = new List<ulong>();

            // 自动查找 KillButton（如果未手动绑定）
            if (killButton == null)
            {
                GameObject found = GameObject.Find("KillButton");
                if (found != null)
                {
                    killButton = found.GetComponent<Button>();
                    killButtonRoot = found;
                }
            }

            if (killButton != null)
            {
                killButton.onClick.AddListener(OnKillButtonClicked);
                killButtonRoot ??= killButton.gameObject;
            }

            // 查找冷却填充图
            if (killButtonRoot != null && killCooldownFill == null)
            {
                Transform fill = killButtonRoot.transform.Find("CooldownFill");
                if (fill != null) killCooldownFill = fill.GetComponent<Image>();
            }

            // 查找冷却文字
            if (killButtonRoot != null && cooldownText == null)
            {
                Transform ct = killButtonRoot.transform.Find("CooldownText");
                if (ct != null) cooldownText = ct.GetComponent<Text>();
            }

            SetKillButtonVisible(false);
            CreateScreenFlashOverlay();
        }

        internal void Bind(OnlineMatchController matchController)
        {
            if (matchController != null)
            {
                controller = matchController;
            }

            if (hud == null)
            {
                hud = FindAnyObjectByType<OnlineMatchHud>();
            }
        }

        private void Update()
        {
            if (controller == null) return;

            // 冷却更新
            TickCooldown();

            // 检测新出现的尸体，为其创建"报告"按钮
            CheckNewBodies();

            // 仅在 Action 阶段、本地玩家存活、且为 Gang/Mole 时检测击杀
            if (!ShouldShowKillButton())
            {
                SetKillButtonVisible(false);
                currentVictimId = 0;
                return;
            }

            // 查找最近可击杀目标
            if (TryFindNearestVictimForLocal(out OnlinePlayerState victim, out ulong victimId))
            {
                currentVictim = victim;
                currentVictimId = victimId;
                SetKillButtonVisible(true);
                UpdateKillButtonAppearance();
            }
            else
            {
                SetKillButtonVisible(false);
                currentVictimId = 0;
            }
        }

        // ══════════════════════════════════════════════════════
        // 击杀状态管理 API（供 OnlineMatchController 调用）
        // ══════════════════════════════════════════════════════

        internal void SetKillCooldown(ulong clientId, float value)
        {
            killCooldowns[clientId] = value;
        }

        internal bool TryGetKillCooldown(ulong clientId, out float value)
        {
            return killCooldowns.TryGetValue(clientId, out value);
        }

        /// <summary>
        /// 服务器端：查找指定位置附近的最近可击杀目标。
        /// 使用 controller 的 privateRoles 判断阵营。
        /// </summary>
        internal bool TryFindNearestVictim(Vector3 position, out ulong victimClientId, out OnlinePlayerState victim)
        {
            victimClientId = ulong.MaxValue;
            victim = default;
            if (controller == null) return false;

            float bestDistance = controller.RuleSet.KillRange;

            foreach (var pair in controller.Players)
            {
                OnlinePlayerState candidate = pair.Value;

                if (!candidate.Alive || controller.GetPrivateRole(pair.Key) == OnlineRole.Gang)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, candidate.Position);

                if (distance <= bestDistance)
                {
                    victimClientId = pair.Key;
                    victim = candidate;
                    bestDistance = distance;
                }
            }

            return victimClientId != ulong.MaxValue;
        }

        /// <summary>
        /// 服务器端：查找指定位置附近的最近未报案尸体。
        /// </summary>
        internal bool TryFindNearestBody(Vector3 position, out int bodyIndex)
        {
            bodyIndex = -1;
            if (controller == null) return false;
            float bestDistance = controller.RuleSet.ReportRange;

            for (int i = 0; i < bodies.Count; i++)
            {
                OnlineBodyState body = bodies[i];

                if (body.Reported)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, body.Position);

                if (distance <= bestDistance)
                {
                    bodyIndex = i;
                    bestDistance = distance;
                }
            }

            return bodyIndex >= 0;
        }

        internal int CountUnreportedBodies()
        {
            int activeBodies = 0;

            foreach (OnlineBodyState body in bodies)
            {
                if (!body.Reported)
                {
                    activeBodies++;
                }
            }

            return activeBodies;
        }

        internal void RemoveReportedBodies()
        {
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                if (bodies[i].Reported)
                {
                    bodies.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 服务器端：每帧 tick 击杀冷却。
        /// </summary>
        internal void TickKillCooldowns(float deltaTime)
        {
            if (controller == null) return;

            List<ulong> keys = new List<ulong>(killCooldowns.Keys);

            foreach (ulong clientId in keys)
            {
                killCooldowns[clientId] = Mathf.Max(0f, killCooldowns[clientId] - deltaTime);

                if (controller.Players.TryGetValue(clientId, out OnlinePlayerState state))
                {
                    state.KillCooldown = killCooldowns[clientId];
                    controller.Players[clientId] = state;
                }
            }
        }

        /// <summary>
        /// 服务器端：tick 报告冷却。
        /// </summary>
        internal void TickReportCooldown(float deltaTime)
        {
            if (reportCooldownTimer > 0f)
            {
                reportCooldownTimer = Mathf.Max(0f, reportCooldownTimer - deltaTime);
            }
        }

        /// <summary>
        /// M4.2: 会议结束后给所有黑帮阵营玩家施加击杀冷却宽容期。
        /// </summary>
        internal void ApplyPostMeetingKillGrace(float grace)
        {
            if (grace <= 0f || controller == null) return;

            foreach (var kv in controller.Players)
            {
                if (!kv.Value.Alive) continue;
                OnlineRole role = controller.GetPrivateRole(kv.Key);
                if (role == OnlineRole.Gang || role == OnlineRole.Undercover)
                {
                    if (!killCooldowns.ContainsKey(kv.Key))
                        killCooldowns[kv.Key] = grace;
                    else if (killCooldowns[kv.Key] < grace)
                        killCooldowns[kv.Key] = grace;
                }
            }
        }

        /// <summary>
        /// 创建尸体 GameObject 并添加到 bodyVisuals。
        /// </summary>
        internal void CreateBodyVisualFor(OnlineBodyState body)
        {
            if (controller == null || controller.WorldBuilder == null) return;

            Sprite bodySprite = controller.GetBodySpriteForClient(body.VictimClientId);
            GameObject visual = controller.WorldBuilder.CreateBodyVisual(body, bodySprite);
            if (visual != null)
            {
                bodyVisuals[body.Id] = visual;
            }
        }

        /// <summary>
        /// 更新所有尸体可视对象的位置，并清理过期可视。
        /// </summary>
        internal void UpdateBodyVisuals()
        {
            HashSet<int> seen = new HashSet<int>();

            foreach (OnlineBodyState body in bodies)
            {
                if (body.Reported)
                {
                    continue;
                }

                seen.Add(body.Id);

                if (!bodyVisuals.TryGetValue(body.Id, out GameObject visual) || visual == null)
                {
                    CreateBodyVisualFor(body);
                    visual = bodyVisuals.TryGetValue(body.Id, out GameObject v) ? v : null;
                }

                if (visual != null)
                {
                    visual.transform.position = body.Position + new Vector3(0f, 0f, 0.11f);
                    SetSortingFromZ(visual);
                }
            }

            RemoveStaleBodyVisuals(seen);
        }

        internal void ClearBodyVisuals()
        {
            foreach (var visual in bodyVisuals.Values)
            {
                if (visual != null)
                {
                    if (Application.isPlaying)
                        Destroy(visual);
                    else
                        DestroyImmediate(visual);
                }
            }
            bodyVisuals.Clear();
        }

        /// <summary>
        /// 清空所有击杀相关状态（对局结束/重置时调用）。
        /// </summary>
        internal void ClearAll()
        {
            killCooldowns.Clear();
            bodies.Clear();
            bodyVisuals.Clear();
            nextBodyId = 0;
            reportCooldownTimer = 0f;
            killCount = 0;
        }

        // -------- 核心逻辑 --------

        private void OnKillButtonClicked()
        {
            if (currentVictimId == 0 || isOnCooldown) return;
            if (controller == null) return;

            // 记录将被击杀的玩家位置用于后续效果
            Vector3 victimPos = currentVictim.Position;
            ulong victimClientId = currentVictim.ClientId;

            // 通过 controller 触发击杀
            controller.RequestAction(OnlineActionType.Kill);

            // 播放本地击杀效果
            PlayKillEffects(victimPos, victimClientId);
            TriggerScreenFlash();

            // 进入冷却
            StartCooldown();
        }

        private void PlayKillEffects(Vector3 victimPos, ulong victimClientId)
        {
            // 触发攻击者（本地玩家）的击杀动作动画
            if (controller != null && controller.Players != null)
            {
                ulong localId = controller.LocalClientIdValue;
                if (controller.Players.TryGetValue(localId, out var attackerState))
                {
                    if (attackerState.SocialChar != null)
                    {
                        attackerState.SocialChar.TriggerAction();
                    }
                }

                // 触发受害者死亡动画
                if (controller.Players.TryGetValue(victimClientId, out var victimState))
                {
                    if (victimState.SocialChar != null)
                    {
                        victimState.SocialChar.Kill();
                    }
                }
            }

            // 血迹效果
            if (bloodEffectPrefab != null)
            {
                Instantiate(bloodEffectPrefab, victimPos + new Vector3(0f, 0f, 0.05f), Quaternion.identity, controller.transform);
            }
            else
            {
                CreateFallbackBloodEffect(victimPos);
            }

            // 击杀音效
            GanglandUndercover.Audio.AudioManager.Instance?.PlaySFX(GanglandUndercover.Audio.SoundEffect.Kill);
        }

        // ══════════════════════════════════════════════════════
        // 屏幕红色闪光
        // ══════════════════════════════════════════════════════

        private void CreateScreenFlashOverlay()
        {
            _flashCanvas = new GameObject("KillFlashCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
                .GetComponent<Canvas>();
            _flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _flashCanvas.sortingOrder = 999;

            var go = new GameObject("KillFlashImage", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_flashCanvas.transform, false);
            _flashImage = go.GetComponent<Image>();
            _flashImage.color = new Color(0.8f, 0.05f, 0.05f, 0f);
            _flashImage.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public void TriggerScreenFlash()
        {
            if (_flashImage == null) return;
            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            float half = flashDuration * 0.5f;
            float elapsed = 0f;

            // 快速渐入
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                _flashImage.color = new Color(0.8f, 0.05f, 0.05f, Mathf.Lerp(0f, flashPeakAlpha, t));
                yield return null;
            }

            _flashImage.color = new Color(0.8f, 0.05f, 0.05f, flashPeakAlpha);
            yield return new WaitForSeconds(0.05f);

            // 渐出
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                _flashImage.color = new Color(0.8f, 0.05f, 0.05f, Mathf.Lerp(flashPeakAlpha, 0f, t));
                yield return null;
            }

            _flashImage.color = new Color(0.8f, 0.05f, 0.05f, 0f);
        }

        // ══════════════════════════════════════════════════════
        // 世界空间"报告"按钮
        // ══════════════════════════════════════════════════════

        private void CheckNewBodies()
        {
            if (controller == null) return;
            if (bodies == null) return;

            foreach (var body in bodies)
            {
                if (body.Reported) continue;
                if (_trackedBodies.Contains((ulong)body.Id)) continue;
                _trackedBodies.Add((ulong)body.Id);
                CreateReportButton(body);
            }
        }

        private void CreateReportButton(OnlineBodyState body)
        {
            // 查找尸体对应玩家名称
            string victimName = "未知";
            if (controller.Players != null && controller.Players.TryGetValue(body.VictimClientId, out var victim))
            {
                victimName = victim.DisplayName;
            }

            GameObject buttonRoot = new GameObject($"BodyReportBtn_{body.Id}");
            buttonRoot.transform.position = body.Position + new Vector3(0f, 1.1f, 0f);

            // 使用 World Space Canvas
            Canvas canvas = buttonRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 90;
            RectTransform crt = buttonRoot.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(200f, 60f);

            CanvasScaler scaler = buttonRoot.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;

            buttonRoot.AddComponent<GraphicRaycaster>();

            // 按钮背景
            GameObject btnObj = new GameObject("ReportBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(buttonRoot.transform, false);
            btnObj.GetComponent<Image>().color = new Color(0.72f, 0.12f, 0.08f, 0.85f);
            RectTransform brt = btnObj.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(160f, 36f);

            // 按钮文字
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(btnObj.transform, false);
            Text label = labelObj.GetComponent<Text>();
            label.text = $"报告 {victimName} 的尸体";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            RectTransform lrt = labelObj.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(150f, 28f);

            // 点击事件 — 调用 controller 的报告尸体
            btnObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (controller != null)
                {
                    controller.RequestAction(OnlineActionType.Report);
                }
                Destroy(buttonRoot);
            });
        }

        // ══════════════════════════════════════════════════════
        // 冷却 + UI
        // ══════════════════════════════════════════════════════

        private void StartCooldown()
        {
            isOnCooldown = true;
            currentCooldown = killCooldownSeconds;
            UpdateKillButtonAppearance();
        }

        private void TickCooldown()
        {
            if (!isOnCooldown) return;

            currentCooldown -= Time.deltaTime;

            float progress = 1f - Mathf.Clamp01(currentCooldown / killCooldownSeconds);

            if (killCooldownFill != null)
                killCooldownFill.fillAmount = progress;

            if (currentCooldown <= 0f)
            {
                isOnCooldown = false;
                currentCooldown = 0f;
                if (killCooldownFill != null) killCooldownFill.fillAmount = 0f;
                UpdateKillButtonAppearance();
            }
        }

        private void UpdateKillButtonAppearance()
        {
            if (killButton == null) return;

            if (isOnCooldown)
            {
                // 冷却中：灰色 + 显示倒计时
                killButton.interactable = false;
                var btnImg = killButton.GetComponent<Image>();
                if (btnImg != null) btnImg.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);

                if (killButtonLabel != null)
                    killButtonLabel.text = Mathf.CeilToInt(currentCooldown).ToString();

                if (cooldownText != null)
                {
                    cooldownText.gameObject.SetActive(true);
                    cooldownText.text = Mathf.CeilToInt(currentCooldown) + "s";
                }
            }
            else
            {
                // 可击杀：红色按钮
                killButton.interactable = true;
                var btnImg = killButton.GetComponent<Image>();
                if (btnImg != null) btnImg.color = new Color(0.72f, 0.12f, 0.08f, 0.9f);

                if (killButtonLabel != null)
                    killButtonLabel.text = currentVictimId != 0
                        ? $"击杀 {currentVictim.DisplayName}"
                        : "击杀";

                if (cooldownText != null)
                    cooldownText.gameObject.SetActive(false);
            }
        }

        private void SetKillButtonVisible(bool visible)
        {
            if (killButtonRoot != null && killButtonRoot.activeSelf != visible)
                killButtonRoot.SetActive(visible);
        }

        // ══════════════════════════════════════════════════════
        // 判定逻辑
        // ══════════════════════════════════════════════════════

        private bool ShouldShowKillButton()
        {
            if (controller == null) return false;
            if (controller.Phase != OnlineMatchPhase.Action) return false;

            // 检查本地玩家是否存活
            if (!controller.LocalAlive) return false;

            // 检查本地角色是否为 Gang 或 Mole
            OnlineRole localRole = controller.LocalRole;
            return localRole == OnlineRole.Gang || localRole == OnlineRole.Mole;
        }

        /// <summary>
        /// 本地端：从本地玩家出发查找最近可击杀目标（用于显示击杀按钮）。
        /// </summary>
        private bool TryFindNearestVictimForLocal(out OnlinePlayerState victim, out ulong victimId)
        {
            victim = default;
            victimId = 0;

            if (controller == null) return false;

            // 获取本地玩家位置
            ulong localId = controller.LocalClientIdValue;
            if (!controller.Players.TryGetValue(localId, out OnlinePlayerState localState)) return false;
            if (!localState.Alive) return false;

            Vector3 localPos = localState.Position;
            float bestDistance = killRange + 0.01f;
            ulong bestId = 0;
            OnlinePlayerState bestVictim = default;

            foreach (var kvp in controller.Players)
            {
                if (kvp.Key == localId) continue;
                var state = kvp.Value;
                if (!state.Alive) continue;
                // 不能击杀同阵营（Gang/Mole 之间不互相伤害）
                if (state.PublicRole == OnlineRole.Gang || state.PublicRole == OnlineRole.Mole) continue;

                float dist = Vector3.Distance(localPos, state.Position);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestId = kvp.Key;
                    bestVictim = state;
                }
            }

            if (bestId != 0)
            {
                victim = bestVictim;
                victimId = bestId;
                return true;
            }

            return false;
        }

        // ══════════════════════════════════════════════════════
        // 血迹效果
        // ══════════════════════════════════════════════════════

        private void CreateFallbackBloodEffect(Vector3 position)
        {
            GameObject blood = new GameObject("KillBloodEffect");
            blood.transform.position = position + new Vector3(0f, 0f, 0.05f);

            SpriteRenderer sr = blood.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite(32, new Color(0.72f, 0.08f, 0.06f, 0.82f));
            sr.sortingOrder = 80;
            sr.transform.localScale = new Vector3(0.42f, 0.42f, 1f);

            Destroy(blood, 2.0f);
        }

        private static Sprite CreateCircleSprite(int size, Color color)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = d <= radius ? color.a : 0f;
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // ══════════════════════════════════════════════════════
        // 内部工具
        // ══════════════════════════════════════════════════════

        private void RemoveStaleBodyVisuals(HashSet<int> seen)
        {
            List<int> stale = new List<int>();
            foreach (var kv in bodyVisuals)
            {
                if (!seen.Contains(kv.Key))
                {
                    stale.Add(kv.Key);
                }
            }

            foreach (int id in stale)
            {
                if (bodyVisuals.TryGetValue(id, out GameObject visual) && visual != null)
                {
                    if (Application.isPlaying)
                        Destroy(visual);
                    else
                        DestroyImmediate(visual);
                }
                bodyVisuals.Remove(id);
            }
        }

        private void SetSortingFromZ(GameObject visual)
        {
            if (visual == null) return;
            SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
            if (sr == null) sr = visual.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = Mathf.RoundToInt(-visual.transform.position.z * 100f);
            }
        }
    }
}
