using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// 统一 UI 管理器 — 管理 Canvas 生命周期、面板切换、UI 元素工厂。
    /// 由 PrototypeBootstrap.Awake 创建，所有 UI 控制器通过 FindAnyObjectByType 获取引用。
    /// 各控制器将自己的根面板注册到 UIManager，通过 ShowOnly 切换面板。
    ///
    /// 颜色方案：
    ///   - Gang（黑帮）= 红色
    ///   - Undercover（卧底）= 蓝色
    ///   - Police（警察）= 灰色
    ///   - 背景深色
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        // ─── 颜色常量 ───────────────────────────────────────────
        public static readonly Color GangColor = new Color(0.78f, 0.22f, 0.16f, 1f);
        public static readonly Color UndercoverColor = new Color(0.08f, 0.62f, 0.82f, 1f);
        public static readonly Color PoliceColor = new Color(0.55f, 0.55f, 0.62f, 1f);
        public static readonly Color AccentOrange = new Color(0.86f, 0.48f, 0.13f, 1f);
        public static readonly Color ReadyGreen = new Color(0.12f, 0.66f, 0.34f, 1f);
        public static readonly Color GoldTitle = new Color(0.92f, 0.75f, 0.18f, 1f);

        public static readonly Color TextColor = new Color(0.92f, 0.9f, 0.82f, 1f);
        public static readonly Color MutedColor = new Color(0.55f, 0.58f, 0.56f, 1f);
        public static readonly Color TitleCream = new Color(0.92f, 0.88f, 0.72f, 1f);
        public static readonly Color BgDark = new Color(0.08f, 0.09f, 0.07f, 1f);
        public static readonly Color PanelBg = new Color(0.12f, 0.13f, 0.11f, 0.97f);
        public static readonly Color ButtonBg = new Color(0.18f, 0.19f, 0.17f, 1f);
        public static readonly Color ButtonHover = new Color(0.28f, 0.29f, 0.25f, 1f);
        public static readonly Color InputBg = new Color(0.14f, 0.15f, 0.13f, 1f);
        public static readonly Color OverlayBg = new Color(0.005f, 0.008f, 0.01f, 0.82f);

        private Canvas _mainCanvas;
        private readonly Dictionary<string, GameObject> _panels = new Dictionary<string, GameObject>();
        private string _activePanel;

        public Canvas MainCanvas => _mainCanvas;

        // ─── 生命周期 ───────────────────────────────────────────
        private void Awake()
        {
            BuildCanvas();
        }

        private void BuildCanvas()
        {
            GameObject canvasObj = new GameObject("UICanvas");
            canvasObj.transform.SetParent(transform);

            _mainCanvas = canvasObj.AddComponent<Canvas>();
            _mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _mainCanvas.sortingOrder = 0;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // ─── 面板管理 ───────────────────────────────────────────
        public void RegisterPanel(string name, GameObject panel)
        {
            if (_panels.ContainsKey(name))
            {
                Destroy(_panels[name]);
            }

            _panels[name] = panel;
            panel.transform.SetParent(_mainCanvas.transform, false);
        }

        /// <summary>显示指定面板，隐藏其他所有面板。</summary>
        public void ShowOnly(string name)
        {
            foreach (KeyValuePair<string, GameObject> kvp in _panels)
            {
                bool active = kvp.Key == name;
                kvp.Value.SetActive(active);
            }

            _activePanel = name;
        }

        public void ShowPanel(string name)
        {
            if (_panels.TryGetValue(name, out GameObject panel))
            {
                panel.SetActive(true);
                _activePanel = name;
            }
        }

        public void HidePanel(string name)
        {
            if (_panels.TryGetValue(name, out GameObject panel))
            {
                panel.SetActive(false);
            }
        }

        public void HideAll()
        {
            foreach (KeyValuePair<string, GameObject> kvp in _panels)
            {
                kvp.Value.SetActive(false);
            }

            _activePanel = null;
        }

        public string ActivePanel => _activePanel;

        // ─── UI 元素工厂方法 ────────────────────────────────────
        public static GameObject CreatePanel(string name, Transform parent, Color bgColor)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = bgColor;
            return obj;
        }

        public static Text CreateText(string name, Transform parent, string content, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text text = obj.GetComponent<Text>();
            text.text = content;
            text.font = GetBuiltinFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Text CreateTextWithStyle(string name, Transform parent, string content, int fontSize, Color color,
            FontStyle fontStyle, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = CreateText(name, parent, content, fontSize, color, alignment);
            text.fontStyle = fontStyle;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label,
            float width, float height, Color bgColor, Color textColor, int fontSize)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);

            Image img = obj.GetComponent<Image>();
            img.color = bgColor;

            Button btn = obj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = bgColor;
            cb.highlightedColor = bgColor * 1.35f;
            cb.pressedColor = bgColor * 0.65f;
            cb.disabledColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            btn.colors = cb;

            SetSize(obj, width, height);

            Text text = CreateText("Label", obj.transform, label, fontSize, textColor, TextAnchor.MiddleCenter);
            Stretch(text.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return btn;
        }

        public static Button CreateImageButton(string name, Transform parent, float width, float height, Color bgColor)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);

            Image img = obj.GetComponent<Image>();
            img.color = bgColor;

            Button btn = obj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = bgColor;
            cb.highlightedColor = bgColor * 1.35f;
            cb.pressedColor = bgColor * 0.65f;
            btn.colors = cb;

            SetSize(obj, width, height);
            return btn;
        }

        public static InputField CreateInputField(string name, Transform parent, string placeholder,
            float width, float height, string defaultText = "")
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            obj.transform.SetParent(parent, false);

            obj.GetComponent<Image>().color = InputBg;
            SetSize(obj, width, height);

            // Text area
            GameObject textArea = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textArea.transform.SetParent(obj.transform, false);
            Text text = textArea.GetComponent<Text>();
            text.font = GetBuiltinFont();
            text.fontSize = 18;
            text.color = TextColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.text = defaultText;
            Stretch(textArea, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));

            // Placeholder
            GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            phObj.transform.SetParent(obj.transform, false);
            Text phText = phObj.GetComponent<Text>();
            phText.text = placeholder;
            phText.font = GetBuiltinFont();
            phText.fontSize = 18;
            phText.color = MutedColor;
            phText.alignment = TextAnchor.MiddleLeft;
            phText.fontStyle = FontStyle.Italic;
            Stretch(phObj, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));

            InputField inputField = obj.GetComponent<InputField>();
            inputField.textComponent = text;
            inputField.placeholder = phText;

            return inputField;
        }

        // ─── 布局辅助 ───────────────────────────────────────────
        public static RectTransform Stretch(GameObject obj, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static void SetSize(GameObject obj, float width, float height)
        {
            RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject obj, int padding, int spacing)
        {
            VerticalLayoutGroup layout = obj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        // ─── 字体 ───────────────────────────────────────────────
        public static Font GetBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }
    }
}
