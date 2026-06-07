using GanglandUndercover.Audio;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 下载数据小游戏。进度条从 0% 自动增长到 100%，
    /// 期间随机出现"信号干扰"需要点击修复，干扰出现时进度暂停。
    /// </summary>
    public sealed class DownloadTask : MiniGameBase
    {
        private const float BaseDownloadSpeed = 16f;   // 基础下载速度（%/秒）
        private const float InterferenceChance = 0.35f; // 每秒干扰概率
        private const float InterferenceCooldown = 1.5f; // 两次干扰最小间隔
        private const float InterferenceFixTime = 1.2f;  // 修复干扰所需点击持续
        private const int MaxInterferences = 4;
        private const float SuccessDelay = 0.35f;

        private Canvas canvas;
        private Text statusText;
        private Text percentText;
        private Image progressFill;
        private GameObject interferencePanel;
        private Text interferenceText;
        private Image interferenceFill;
        private float downloadPercent;
        private float interferenceTimer;
        private int interferenceCount;
        private bool isInterfering;
        private bool isComplete;
        private float interferenceFixProgress;

        public override void Show()
        {
            downloadPercent = 0f;
            interferenceTimer = InterferenceCooldown;
            interferenceCount = 0;
            isInterfering = false;
            isComplete = false;
            interferenceFixProgress = 0f;
            CreateUI();
            gameObject.SetActive(true);
        }

        public override void Hide()
        {
            isComplete = true;
            StopAllCoroutines();
            if (canvas != null)
            {
                DestroyRuntimeObject(canvas.gameObject);
                canvas = null;
            }
            gameObject.SetActive(false);
        }

        private void CreateUI()
        {
            GameObject canvasObj = new GameObject("DownloadTaskCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 背景
            CreatePanel(canvasObj, "Background", new Color(0.10f, 0.12f, 0.16f, 0.97f));

            // 标题
            statusText = CreateLabel(canvasObj, "正在下载数据...", 22,
                new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f));
            statusText.color = new Color(0.35f, 0.65f, 0.80f);

            // 百分比
            percentText = CreateLabel(canvasObj, "0%", 28,
                new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f));
            percentText.color = new Color(0.88f, 0.90f, 0.92f);

            // 进度条背景
            GameObject progressBg = CreatePanel(canvasObj, "ProgressBg",
                new Color(0.18f, 0.20f, 0.26f, 1f));
            RectTransform progressBgRT = progressBg.GetComponent<RectTransform>();
            progressBgRT.anchorMin = new Vector2(0.2f, 0.48f);
            progressBgRT.anchorMax = new Vector2(0.8f, 0.52f);
            progressBgRT.offsetMin = Vector2.zero;
            progressBgRT.offsetMax = Vector2.zero;

            // 进度条填充
            GameObject progressFillObj = new GameObject("ProgressFill");
            progressFillObj.transform.SetParent(progressBg.transform);
            progressFill = progressFillObj.AddComponent<Image>();
            progressFill.color = new Color(0.20f, 0.60f, 0.86f);

            RectTransform fillRT = progressFillObj.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.zero;
            fillRT.pivot = new Vector2(0f, 0.5f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fillRT.sizeDelta = new Vector2(0f, 0f);

            // 信号干扰面板
            interferencePanel = new GameObject("InterferencePanel");
            interferencePanel.transform.SetParent(canvas.transform);
            interferencePanel.SetActive(false);

            RectTransform ipRT = interferencePanel.AddComponent<RectTransform>();
            ipRT.anchorMin = new Vector2(0.15f, 0.32f);
            ipRT.anchorMax = new Vector2(0.85f, 0.44f);
            ipRT.offsetMin = Vector2.zero;
            ipRT.offsetMax = Vector2.zero;

            Image ipBg = interferencePanel.AddComponent<Image>();
            ipBg.color = new Color(0.80f, 0.22f, 0.18f, 0.25f);

            interferenceText = CreateLabel(interferencePanel, "信号干扰！点击修复", 16,
                new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f));
            interferenceText.color = new Color(0.95f, 0.55f, 0.20f);

            // 干扰修复进度条（小）
            GameObject ifBgObj = new GameObject("IFBg");
            ifBgObj.transform.SetParent(interferencePanel.transform);
            Image ifBgImg = ifBgObj.AddComponent<Image>();
            ifBgImg.color = new Color(0.15f, 0.10f, 0.08f, 0.8f);

            RectTransform ifBgRT = ifBgObj.GetComponent<RectTransform>();
            ifBgRT.anchorMin = new Vector2(0.1f, 0.15f);
            ifBgRT.anchorMax = new Vector2(0.9f, 0.40f);
            ifBgRT.offsetMin = Vector2.zero;
            ifBgRT.offsetMax = Vector2.zero;

            GameObject ifFillObj = new GameObject("IFFill");
            ifFillObj.transform.SetParent(ifBgObj.transform);
            interferenceFill = ifFillObj.AddComponent<Image>();
            interferenceFill.color = new Color(0.35f, 0.78f, 0.36f);

            RectTransform ifFillRT = ifFillObj.GetComponent<RectTransform>();
            ifFillRT.anchorMin = Vector2.zero;
            ifFillRT.anchorMax = Vector2.zero;
            ifFillRT.pivot = new Vector2(0f, 0.5f);
            ifFillRT.offsetMin = Vector2.zero;
            ifFillRT.offsetMax = Vector2.zero;
            ifFillRT.sizeDelta = new Vector2(0f, 0f);

            // 点击修复按钮（覆盖式）
            GameObject fixBtnObj = new GameObject("FixButton");
            fixBtnObj.transform.SetParent(canvas.transform);
            RectTransform fixBtnRT = fixBtnObj.AddComponent<RectTransform>();
            fixBtnRT.anchorMin = Vector2.zero;
            fixBtnRT.anchorMax = Vector2.one;
            fixBtnRT.offsetMin = Vector2.zero;
            fixBtnRT.offsetMax = Vector2.zero;

            Button fixBtn = fixBtnObj.AddComponent<Button>();
            fixBtn.targetGraphic = fixBtnObj.AddComponent<Image>();
            fixBtn.targetGraphic.color = Color.clear;
            fixBtn.onClick.AddListener(OnFixClick);

            // 提示文字
            CreateLabel(canvasObj, "干扰修复：连续点击 / 按住修复按钮", 14,
                new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f));
        }

        private void Update()
        {
            if (isComplete) return;

            if (isInterfering)
            {
                // 干扰期间不增长进度
                interferenceTimer -= Time.deltaTime;

                if (interferenceTimer <= 0f)
                {
                    // 干扰超时：下载失败
                    isComplete = true;
                    StartCoroutine(FailureRoutine());
                }
                return;
            }

            // 正常下载
            downloadPercent += BaseDownloadSpeed * Time.deltaTime;

            if (downloadPercent >= 100f)
            {
                downloadPercent = 100f;
                isComplete = true;
                percentText.text = "100%";
                percentText.color = new Color(0.18f, 0.82f, 0.32f);
                statusText.text = "下载完成！";
                statusText.color = new Color(0.18f, 0.82f, 0.32f);
                UpdateProgressBar();
                StartCoroutine(SuccessRoutine());
                return;
            }

            percentText.text = string.Format("{0:F0}%", downloadPercent);
            UpdateProgressBar();

            // 检查信号干扰
            interferenceTimer -= Time.deltaTime;

            if (interferenceTimer <= 0f && interferenceCount < MaxInterferences)
            {
                if (Random.value < InterferenceChance)
                {
                    TriggerInterference();
                }
                interferenceTimer = InterferenceCooldown;
            }
        }

        private void TriggerInterference()
        {
            isInterfering = true;
            interferenceCount++;
            interferenceFixProgress = 0f;
            interferenceTimer = InterferenceFixTime + 2f; // 修复窗口
            interferencePanel.SetActive(true);
            UpdateInterferenceProgress();

            statusText.text = string.Format("信号干扰 x{0}!", interferenceCount);
            statusText.color = new Color(0.90f, 0.30f, 0.20f);

            // 干扰音效提示
            Audio.AudioManager.Instance?.PlaySFX(Audio.SoundEffect.Emergency);
        }

        private void OnFixClick()
        {
            if (!isInterfering || isComplete) return;

            // 每次点击推进修复进度
            interferenceFixProgress += 0.22f;
            UpdateInterferenceProgress();

            if (interferenceFixProgress >= 1f)
            {
                ResolveInterference();
            }
        }

        private void UpdateInterferenceProgress()
        {
            if (interferenceFill == null) return;

            RectTransform fillRT = interferenceFill.GetComponent<RectTransform>();
            RectTransform parentRT = interferenceFill.transform.parent.GetComponent<RectTransform>();
            float width = parentRT.rect.width;
            fillRT.sizeDelta = new Vector2(width * interferenceFixProgress, 0f);

            // 颜色从红渐变到绿
            interferenceFill.color = Color.Lerp(
                new Color(0.90f, 0.30f, 0.20f),
                new Color(0.30f, 0.82f, 0.35f),
                interferenceFixProgress);
        }

        private void ResolveInterference()
        {
            isInterfering = false;
            interferencePanel.SetActive(false);
            interferenceTimer = InterferenceCooldown;

            statusText.text = "正在下载数据...";
            statusText.color = new Color(0.35f, 0.65f, 0.80f);
        }

        private void UpdateProgressBar()
        {
            if (progressFill == null) return;

            RectTransform fillRT = progressFill.GetComponent<RectTransform>();
            RectTransform parentRT = progressFill.transform.parent.GetComponent<RectTransform>();
            float width = parentRT.rect.width;
            fillRT.sizeDelta = new Vector2(width * downloadPercent / 100f, 0f);
        }

        private IEnumerator SuccessRoutine()
        {
            yield return new WaitForSeconds(SuccessDelay);
            Complete();
        }

        private IEnumerator FailureRoutine()
        {
            statusText.text = "信号中断，下载失败！";
            statusText.color = new Color(0.90f, 0.20f, 0.20f);
            interferencePanel.SetActive(false);

            yield return new WaitForSeconds(0.6f);
            Cancel();
        }

        private GameObject CreatePanel(GameObject parent, string name, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent.transform);
            Image img = panel.AddComponent<Image>();
            img.color = color;
            return panel;
        }

        private Text CreateLabel(GameObject parent, string text, float fontSize, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject labelObj = new GameObject("Label_" + text.GetHashCode());
            labelObj.transform.SetParent(parent.transform);
            Text txt = labelObj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = (int)fontSize;
            txt.color = new Color(0.88f, 0.90f, 0.92f);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform rt = labelObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return txt;
        }
    }
}