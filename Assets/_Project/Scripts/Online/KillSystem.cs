using System;
using System.Collections;
using System.Collections.Generic;
using GanglandUndercover.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// 击杀系统（增强版）：
    /// 1. 当本地 Gang 玩家靠近存活非 Gang 目标 1.5m 内时，显示"击杀"按钮
    /// 2. 点击后通过 OnlineMatchHud.RequestKill() 执行击杀
    /// 3. 击杀瞬间屏幕红色闪光
    /// 4. 击杀冷却期间按钮灰色 + 倒计时数字
    /// 5. 尸体上方创建 3D 世界空间"报告"按钮
    /// 6. 冷却 18 秒（Inspector 可配）
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

        // -------- 运行时状态 --------
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
            if (TryFindNearestVictim(out OnlinePlayerState victim, out ulong victimId))
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
            var bodies = controller.Bodies;
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

        private bool TryFindNearestVictim(out OnlinePlayerState victim, out ulong victimId)
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
    }
}