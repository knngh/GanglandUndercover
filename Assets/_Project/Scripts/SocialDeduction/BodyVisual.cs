using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 尸体视觉效果：在死亡位置生成半透明轮廓 + 阵营色标记 + 3D 世界空间报告按钮。
    ///
    /// 功能：
    /// 1. 半透明轮廓（LineRenderer 圆环）标识尸体位置
    /// 2. 阵营色标记（Gang=红 / Police=蓝 / Undercover=黄 / Mole=青）
    /// 3. 3D 世界空间 Canvas 报告按钮（漂浮在尸体上方）
    /// 4. 脉冲呼吸动画增强可见性
    /// </summary>
    public sealed class BodyVisual : MonoBehaviour
    {
        [Header("轮廓设置")]
        [Tooltip("轮廓圆环半径（米）")]
        public float outlineRadius = 0.6f;

        [Tooltip("轮廓线宽度")]
        public float outlineWidth = 0.06f;

        [Tooltip("脉冲呼吸速度")]
        public float pulseSpeed = 2.5f;

        [Header("报告按钮")]
        [Tooltip("按钮距尸体高度（米）")]
        public float buttonHeight = 1.4f;

        [Tooltip("按钮 Canvas 尺寸")]
        public Vector2 canvasSize = new Vector2(2.0f, 0.6f);

        [Tooltip("按钮颜色")]
        public Color buttonColor = new Color(0.72f, 0.12f, 0.08f, 0.88f);

        [Tooltip("按钮文字")]
        public string buttonText = "报告尸体";

        private LineRenderer outlineRenderer;
        private Light factionLight;
        private Canvas worldCanvas;
        private Button reportButton;
        private Color factionColor;
        private float baseAlpha;
        private Action onReport;

        /// <summary>
        /// 初始化尸体视觉效果。
        /// </summary>
        /// <param name="victim">死亡角色</param>
        /// <param name="onReportCallback">点击报告按钮的回调</param>
        public void Initialize(SocialCharacter victim, Action onReportCallback)
        {
            onReport = onReportCallback;

            // 阵营色
            factionColor = GetFactionColor(victim.Role);
            baseAlpha = factionColor.a;

            // 生成轮廓
            CreateOutline(victim.transform.position);

            // 阵营色点光源标记
            CreateFactionMarker(victim.transform.position);

            // 报告按钮
            CreateReportButton(victim.CharacterName);

            // 开始脉冲动画
            StartCoroutine(PulseRoutine());
        }

        private Color GetFactionColor(SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang:      return new Color(0.85f, 0.15f, 0.12f, 0.55f); // 红
                case SocialRole.Police:    return new Color(0.15f, 0.35f, 0.82f, 0.55f); // 蓝
                case SocialRole.Mole:      return new Color(0.18f, 0.58f, 0.52f, 0.55f); // 青
                default:                   return new Color(0.88f, 0.66f, 0.22f, 0.55f); // 黄（卧底）
            }
        }

        private void CreateOutline(Vector3 center)
        {
            GameObject outlineObj = new GameObject("BodyOutline");
            outlineObj.transform.SetParent(transform, false);
            outlineObj.transform.position = center + Vector3.up * 0.02f;
            outlineObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            outlineRenderer = outlineObj.AddComponent<LineRenderer>();
            outlineRenderer.loop = true;
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.startWidth = outlineWidth;
            outlineRenderer.endWidth = outlineWidth;
            outlineRenderer.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            outlineRenderer.startColor = factionColor;
            outlineRenderer.endColor = factionColor;
            outlineRenderer.sortingOrder = 10;

            // 生成圆环点
            int segments = 48;
            Vector3[] points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                points[i] = new Vector3(Mathf.Cos(angle) * outlineRadius, Mathf.Sin(angle) * outlineRadius, 0f);
            }
            outlineRenderer.positionCount = segments + 1;
            outlineRenderer.SetPositions(points);
        }

        private void CreateFactionMarker(Vector3 center)
        {
            GameObject lightObj = new GameObject("FactionMarker");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.position = center + Vector3.up * 0.05f;

            factionLight = lightObj.AddComponent<Light>();
            factionLight.type = LightType.Point;
            factionLight.color = new Color(factionColor.r, factionColor.g, factionColor.b, 1f);
            factionLight.intensity = 0.6f;
            factionLight.range = 2.5f;
            factionLight.renderMode = LightRenderMode.ForcePixel;
        }

        private void CreateReportButton(string victimName)
        {
            // 世界空间 Canvas
            GameObject canvasObj = new GameObject("ReportCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.localPosition = Vector3.up * buttonHeight;

            worldCanvas = canvasObj.GetComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;

            RectTransform crt = canvasObj.GetComponent<RectTransform>();
            crt.sizeDelta = canvasSize;

            // 按钮
            GameObject btnObj = new GameObject("ReportBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(canvasObj.transform, false);

            Image btnImage = btnObj.GetComponent<Image>();
            btnImage.color = buttonColor;

            RectTransform brt = btnObj.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            // 按钮文字
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(btnObj.transform, false);

            Text label = labelObj.GetComponent<Text>();
            label.text = $"{buttonText}：{victimName}";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 14;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;

            RectTransform lrt = labelObj.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            // 点击回调
            reportButton = btnObj.GetComponent<Button>();
            reportButton.onClick.AddListener(() => onReport?.Invoke());
        }

        private IEnumerator PulseRoutine()
        {
            while (true)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f; // 0~1
                float alpha = Mathf.Lerp(0.25f, baseAlpha, t);

                if (outlineRenderer != null)
                {
                    Color c = factionColor;
                    c.a = alpha;
                    outlineRenderer.startColor = c;
                    outlineRenderer.endColor = c;
                }

                if (factionLight != null)
                {
                    factionLight.intensity = Mathf.Lerp(0.3f, 0.9f, t);
                }

                yield return null;
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}