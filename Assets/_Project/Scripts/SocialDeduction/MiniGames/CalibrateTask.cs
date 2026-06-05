using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 航向校准小游戏。十字准星从偏离位置自动向中心移动，
    /// 在准星到达中心时点击确认。共 3 轮，每轮速度递增。
    /// </summary>
    public sealed class CalibrateTask : MiniGameBase
    {
        private const int TotalRounds = 3;
        private const float BaseSpeed = 0.6f;
        private const float SpeedIncrement = 0.35f;
        private const float TargetRadius = 28f;
        private const float SuccessDelay = 0.4f;
        private const float EarlyPenaltyDelay = 0.8f;

        private Canvas canvas;
        private Text roundText;
        private Text instructionText;
        private RectTransform crosshairRT;
        private RectTransform targetRT;
        private int currentRound;
        private float currentSpeed;
        private Vector2 startOffset;
        private float progress;

        public override void Show()
        {
            currentRound = 0;
            progress = 0f;
            currentSpeed = BaseSpeed;
            CreateUI();
            StartNextRound();
            gameObject.SetActive(true);
        }

        public override void Hide()
        {
            StopAllCoroutines();
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
                canvas = null;
            }
            gameObject.SetActive(false);
        }

        private void CreateUI()
        {
            GameObject canvasObj = new GameObject("CalibrateTaskCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 背景
            CreatePanel(canvasObj, "Background", new Color(0.12f, 0.14f, 0.18f, 0.96f));

            // 标题
            roundText = CreateLabel(canvasObj, "航向校准 1/3", 22,
                new Vector2(0.5f, 0.92f), new Vector2(0.5f, 0.92f));
            roundText.color = new Color(0.35f, 0.78f, 0.36f);

            // 操作提示
            instructionText = CreateLabel(canvasObj, "准星到达中心时点击屏幕！", 16,
                new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f));
            instructionText.color = new Color(0.7f, 0.7f, 0.7f);

            // 目标中心点
            GameObject targetObj = new GameObject("Target");
            targetObj.transform.SetParent(canvas.transform);
            targetRT = targetObj.AddComponent<RectTransform>();
            targetRT.anchorMin = new Vector2(0.5f, 0.5f);
            targetRT.anchorMax = new Vector2(0.5f, 0.5f);
            targetRT.sizeDelta = new Vector2(60f, 60f);
            targetRT.anchoredPosition = Vector2.zero;

            Image targetImg = targetObj.AddComponent<Image>();
            targetImg.color = new Color(0.25f, 0.28f, 0.35f, 0.9f);

            // 十字准星
            GameObject crosshairObj = new GameObject("Crosshair");
            crosshairObj.transform.SetParent(canvas.transform);
            crosshairRT = crosshairObj.AddComponent<RectTransform>();
            crosshairRT.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRT.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRT.sizeDelta = new Vector2(48f, 48f);
            crosshairRT.anchoredPosition = Vector2.zero;

            // 绘制十字线（两条相交的线）
            CreateCrosshairLine(crosshairObj, true, 48f, 4f);
            CreateCrosshairLine(crosshairObj, false, 48f, 4f);

            // 点击捕获
            Button captureBtn = canvasObj.AddComponent<Button>();
            captureBtn.targetGraphic = canvasObj.GetComponentInChildren<Image>();
            captureBtn.onClick.AddListener(OnScreenClick);
        }

        private void CreateCrosshairLine(GameObject parent, bool horizontal, float length, float thickness)
        {
            GameObject line = new GameObject(horizontal ? "HLine" : "VLine");
            line.transform.SetParent(parent.transform);
            Image img = line.AddComponent<Image>();
            img.color = new Color(0.90f, 0.35f, 0.25f, 1f);
            img.raycastTarget = false;

            RectTransform rt = line.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = horizontal
                ? new Vector2(length, thickness)
                : new Vector2(thickness, length);
        }

        private void StartNextRound()
        {
            currentRound++;
            currentSpeed = BaseSpeed + SpeedIncrement * (currentRound - 1);

            // 十字准星从随机偏离位置出发
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(120f, 220f);
            startOffset = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
            crosshairRT.anchoredPosition = startOffset;
            progress = 0f;

            roundText.text = string.Format("航向校准 {0}/3", currentRound);

            // 缩放脉冲
            StartCoroutine(PulseTarget());
        }

        private IEnumerator PulseTarget()
        {
            while (true)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.08f;
                targetRT.localScale = new Vector3(pulse, pulse, 1f);
                yield return null;
            }
        }

        private void Update()
        {
            if (canvas == null) return;

            // 准星向中心移动
            progress += Time.deltaTime * currentSpeed;

            if (progress >= 1f)
            {
                progress = 1f;
                crosshairRT.anchoredPosition = Vector2.zero;
                return;
            }

            // 缓动：先快后慢，模拟自动校准
            float eased = Mathf.Sin(progress * Mathf.PI * 0.5f);
            crosshairRT.anchoredPosition = Vector2.Lerp(startOffset, Vector2.zero, eased);
        }

        private void OnScreenClick()
        {
            if (canvas == null) return;

            float distance = crosshairRT.anchoredPosition.magnitude;

            if (distance <= TargetRadius)
            {
                // 精准点击
                StartCoroutine(OnRoundSuccess());
            }
            else
            {
                // 过早点击 — 视觉反馈
                StartCoroutine(OnEarlyClick());
            }
        }

        private IEnumerator OnRoundSuccess()
        {
            // 绿色闪屏反馈
            FlashOverlay(new Color(0.18f, 0.72f, 0.32f, 0.3f));
            yield return new WaitForSeconds(SuccessDelay);

            if (currentRound >= TotalRounds)
            {
                Complete();
                yield break;
            }

            StopAllCoroutines();
            StartNextRound();
        }

        private IEnumerator OnEarlyClick()
        {
            // 红色闪屏反馈
            FlashOverlay(new Color(0.90f, 0.20f, 0.20f, 0.35f));
            yield return new WaitForSeconds(EarlyPenaltyDelay);

            // 重新开始当前轮
            StopAllCoroutines();
            StartNextRound();
        }

        private void FlashOverlay(Color color)
        {
            GameObject flash = CreatePanel(canvas.gameObject, "Flash", color);
            RectTransform flashRT = flash.GetComponent<RectTransform>();
            flashRT.anchorMin = Vector2.zero;
            flashRT.anchorMax = Vector2.one;
            flashRT.offsetMin = Vector2.zero;
            flashRT.offsetMax = Vector2.zero;

            StartCoroutine(FadeAndDestroy(flash, 0.5f));
        }

        private IEnumerator FadeAndDestroy(GameObject obj, float duration)
        {
            Image img = obj.GetComponent<Image>();
            float elapsed = 0f;
            Color original = img.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                img.color = new Color(original.r, original.g, original.b,
                    Mathf.Lerp(original.a, 0f, elapsed / duration));
                yield return null;
            }

            Destroy(obj);
        }

        private GameObject CreatePanel(GameObject parent, string name, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent.transform);
            Image img = panel.AddComponent<Image>();
            img.color = color;
            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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