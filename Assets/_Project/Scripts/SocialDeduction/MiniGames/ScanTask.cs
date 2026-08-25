using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 扫描小游戏（Among Us MedBay Scan 复刻）。
    /// 圆形扫描环从外向内收缩，在绿色安全区域点击停止即完成。
    /// </summary>
    public sealed class ScanTask : MiniGameBase
    {
        private const float OuterRadius = 0.32f;     // 外圈半径（anchor 归一化）
        private const float InnerRadius = 0.08f;     // 内圈半径
        private const float GreenMinRatio = 0.78f;   // 绿色区域外边界（1=外圈, 0=内圈）
        private const float GreenMaxRatio = 0.88f;   // 绿色区域内边界
        private const float ScanSpeed = 0.35f;       // 扫描环收缩速度
        private const float SuccessDelay = 0.4f;
        private const float FailShakeDuration = 0.35f;

        private Canvas canvas;
        private RectTransform scanRing;
        private RectTransform greenZone;
        private RectTransform targetArea;
        private Image scanRingImage;
        private Image greenZoneImage;
        private Image targetImage;
        private Text statusText;
        private Text hintText;

        private float currentRadiusRatio = 1f; // 1 = 外圈, 0 = 内圈
        private bool isScanning;
        private bool isComplete;

        public override void Show()
        {
            currentRadiusRatio = 1f;
            isScanning = true;
            isComplete = false;
            CreateUI();
            gameObject.SetActive(true);
        }

        public override void Hide()
        {
            isScanning = false;
            isComplete = false;
            if (canvas != null)
            {
                DestroyRuntimeObject(canvas.gameObject);
                canvas = null;
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isScanning || isComplete) return;

            // 扫描环收缩
            currentRadiusRatio -= ScanSpeed * Time.deltaTime;

            if (currentRadiusRatio <= 0f)
            {
                // 错过绿色区域，重新开始
                currentRadiusRatio = 1f;
                StartCoroutine(FailIndicatorRoutine());
            }

            UpdateRingVisual();
        }

        private void CreateUI()
        {
            GameObject canvasObj = new GameObject("ScanTaskCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 背景
            GameObject bg = CreatePanel(canvasObj, "Background", new Color(0.12f, 0.14f, 0.18f, 0.96f));
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // 优先使用 Resources 面板背景 sprite
            var panelBgSprite = GanglandUndercover.Art.MinigameArtCache.ScanPanelBg
                             ?? GanglandUndercover.Art.MinigameArtCache.PanelBackground;
            if (panelBgSprite != null)
            {
                var bgImg = bg.GetComponent<Image>();
                if (bgImg != null)
                {
                    bgImg.sprite = panelBgSprite;
                    bgImg.type = Image.Type.Sliced;
                    bgImg.color = Color.white;
                }
            }

            // 标题
            CreateLabel(canvasObj, "扫描任务：在绿色区域点击停止", 20,
                new Vector2(0.5f, 0.90f), new Vector2(0.5f, 0.90f));

            hintText = CreateLabel(canvasObj, "等待环收缩到绿色区域，然后点击屏幕中央", 14,
                new Vector2(0.5f, 0.84f), new Vector2(0.5f, 0.84f));

            // 扫描区域容器
            GameObject scanArea = new GameObject("ScanArea");
            scanArea.transform.SetParent(canvasObj.transform);
            RectTransform scanAreaRT = scanArea.AddComponent<RectTransform>();
            scanAreaRT.anchorMin = new Vector2(0.5f, 0.5f);
            scanAreaRT.anchorMax = new Vector2(0.5f, 0.5f);
            scanAreaRT.sizeDelta = new Vector2(Screen.width * 0.45f, Screen.width * 0.45f);
            scanAreaRT.anchoredPosition = new Vector2(0f, -20f);

            // 背景圆盘
            GameObject discObj = new GameObject("Disc");
            discObj.transform.SetParent(scanArea.transform);
            Image discImg = discObj.AddComponent<Image>();
            discImg.color = new Color(0.18f, 0.20f, 0.26f, 1f);
            RectTransform discRT = discObj.GetComponent<RectTransform>();
            discRT.anchorMin = Vector2.zero;
            discRT.anchorMax = Vector2.one;
            discRT.offsetMin = Vector2.zero;
            discRT.offsetMax = Vector2.zero;

            // 绿色目标区域（环形）
            greenZone = CreateRing(scanArea, "GreenZone", new Color(0.18f, 0.72f, 0.32f, 0.45f));
            greenZoneImage = greenZone.GetComponent<Image>();

            // 扫描环
            scanRing = CreateRing(scanArea, "ScanRing", new Color(0.35f, 0.65f, 0.80f, 0.7f));
            scanRingImage = scanRing.GetComponent<Image>();

            // 中心目标点
            GameObject centerObj = new GameObject("Center");
            centerObj.transform.SetParent(scanArea.transform);
            targetImage = centerObj.AddComponent<Image>();
            targetImage.color = new Color(0.50f, 0.52f, 0.58f, 1f);
            RectTransform centerRT = centerObj.GetComponent<RectTransform>();
            centerRT.anchorMin = new Vector2(0.44f, 0.44f);
            centerRT.anchorMax = new Vector2(0.56f, 0.56f);
            centerRT.offsetMin = Vector2.zero;
            centerRT.offsetMax = Vector2.zero;

            // 点击检测（在整个扫描区域上）
            Button btn = scanArea.AddComponent<Button>();
            btn.onClick.AddListener(OnScanAreaClicked);

            // 状态文字
            statusText = CreateLabel(canvasObj, "", 18,
                new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f));
            statusText.color = new Color(0.88f, 0.88f, 0.92f);

            UpdateRingVisual();
        }

        private RectTransform CreateRing(GameObject parent, string name, Color color)
        {
            GameObject ringObj = new GameObject(name);
            ringObj.transform.SetParent(parent.transform);
            Image img = ringObj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            RectTransform rt = ringObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return rt;
        }

        private void UpdateRingVisual()
        {
            if (scanRing == null || greenZone == null) return;

            // 扫描环：从外圈向内
            float ringSize = Mathf.Lerp(InnerRadius, OuterRadius, currentRadiusRatio);
            ringSize *= 0.6f; // 映射到父级 anchor 空间
            float ringAnchorMin = 0.5f - ringSize;
            float ringAnchorMax = 0.5f + ringSize;

            scanRing.anchorMin = new Vector2(ringAnchorMin, ringAnchorMin);
            scanRing.anchorMax = new Vector2(ringAnchorMax, ringAnchorMax);

            // 绿色区域固定
            float greenOuter = Mathf.Lerp(InnerRadius, OuterRadius, GreenMinRatio) * 0.6f;
            float greenInner = Mathf.Lerp(InnerRadius, OuterRadius, GreenMaxRatio) * 0.6f;

            greenZone.anchorMin = new Vector2(0.5f - greenOuter, 0.5f - greenOuter);
            greenZone.anchorMax = new Vector2(0.5f - greenInner, 0.5f - greenInner);
        }

        private void OnScanAreaClicked()
        {
            if (!isScanning || isComplete) return;

            // 判断扫描环是否在绿色区域内
            if (currentRadiusRatio >= GreenMaxRatio && currentRadiusRatio <= GreenMinRatio)
            {
                // 成功！
                isComplete = true;
                isScanning = false;
                scanRingImage.color = new Color(0.18f, 0.82f, 0.32f, 1f);
                greenZoneImage.color = new Color(0.18f, 0.82f, 0.32f, 0.8f);
                targetImage.color = new Color(0.18f, 0.82f, 0.32f, 1f);
                statusText.text = "扫描成功！";
                statusText.color = new Color(0.35f, 0.78f, 0.36f);
                StartCoroutine(SuccessRoutine());
            }
            else
            {
                // 失败：红色闪烁，环继续
                StartCoroutine(MissIndicatorRoutine());
            }
        }

        private IEnumerator SuccessRoutine()
        {
            yield return new WaitForSeconds(SuccessDelay);
            Complete();
        }

        private IEnumerator MissIndicatorRoutine()
        {
            scanRingImage.color = new Color(0.90f, 0.20f, 0.20f, 0.7f);
            statusText.text = "未命中！";
            statusText.color = new Color(0.90f, 0.20f, 0.20f);
            yield return new WaitForSeconds(FailShakeDuration);
            scanRingImage.color = new Color(0.35f, 0.65f, 0.80f, 0.7f);
            statusText.text = "";
        }

        private IEnumerator FailIndicatorRoutine()
        {
            // 环已错过绿色区域
            scanRingImage.color = new Color(0.90f, 0.20f, 0.20f, 0.6f);
            statusText.text = "错过！重新扫描...";
            statusText.color = new Color(0.90f, 0.20f, 0.20f);
            yield return new WaitForSeconds(FailShakeDuration);
            scanRingImage.color = new Color(0.35f, 0.65f, 0.80f, 0.7f);
            statusText.text = "";
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
