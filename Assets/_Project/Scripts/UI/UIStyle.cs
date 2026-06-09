using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    /// <summary>
    /// Centralized UI theme for Gangland Undercover.
    /// Noir police-operation aesthetic: dark surfaces, restrained status accents,
    /// and readable CJK-first typography.
    /// </summary>
    public static class UIStyle
    {
        // ═══════════════════════════════════════════════
        //  COLOR PALETTE
        // ═══════════════════════════════════════════════

        // Backgrounds
        public static readonly Color BgDeep    = new Color(0.018f, 0.023f, 0.027f, 0.96f);
        public static readonly Color BgPanel   = new Color(0.042f, 0.050f, 0.056f, 0.94f);
        public static readonly Color BgDock    = new Color(0.030f, 0.038f, 0.044f, 0.92f);
        public static readonly Color BgOverlay = new Color(0.012f, 0.016f, 0.020f, 0.88f);

        // Accents. Kept under the old names for compatibility with existing code.
        public static readonly Color NeonBlue   = new Color(0.30f, 0.58f, 0.68f, 1f);
        public static readonly Color NeonRed    = new Color(0.72f, 0.20f, 0.17f, 1f);
        public static readonly Color NeonAmber  = new Color(0.78f, 0.58f, 0.20f, 1f);
        public static readonly Color NeonGreen  = new Color(0.28f, 0.58f, 0.38f, 1f);
        public static readonly Color NeonPurple = new Color(0.48f, 0.42f, 0.62f, 1f);
        public static readonly Color NeonPink   = new Color(0.64f, 0.30f, 0.42f, 1f);

        // Text
        public static readonly Color TextPrimary   = new Color(0.88f, 0.86f, 0.80f, 1f);
        public static readonly Color TextSecondary = new Color(0.62f, 0.65f, 0.62f, 1f);
        public static readonly Color TextDim       = new Color(0.36f, 0.40f, 0.38f, 1f);
        public static readonly Color TextWarning   = new Color(0.82f, 0.70f, 0.28f, 1f);

        // Borders
        public static readonly Color BorderSubtle = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color BorderStrong = new Color(0.30f, 0.58f, 0.68f, 0.28f);
        public static readonly Color BorderGold   = new Color(0.78f, 0.58f, 0.20f, 0.32f);
        public static readonly Color BorderRed    = new Color(0.72f, 0.20f, 0.17f, 0.30f);

        // ═══════════════════════════════════════════════
        //  FONT SYSTEM — Smart CJK fallback
        // ═══════════════════════════════════════════════

        private static Font _pixelFont;
        private static Font _cjkFont;
        private static bool _fontInitDone;

        /// <summary>Kenney Future pixel font — English/alphanumeric.</summary>
        public static Font PixelFont
        {
            get { EnsureFonts(); return _pixelFont; }
        }

        /// <summary>CJK-capable font for Chinese/Japanese/Korean text.</summary>
        public static Font CJKFont
        {
            get { EnsureFonts(); return _cjkFont; }
        }

        private static void EnsureFonts()
        {
            if (_fontInitDone) return;
            _fontInitDone = true;

            // 1. Load Kenney Future (CC0 sci-fi pixel font) — English only
            _pixelFont = Resources.Load<Font>("Fonts/KenneyFuture");
            if (_pixelFont != null)
                Debug.Log("[UIStyle] Loaded KenneyFuture pixel font.");

            // 2. Load or create CJK font
            _cjkFont = Resources.Load<Font>("Fonts/CJKPixelFallback");
            if (_cjkFont == null)
            {
                // Try system CJK fonts
                _cjkFont = Font.CreateDynamicFontFromOSFont("PingFang SC", 16) ??
                           Font.CreateDynamicFontFromOSFont("STHeiti", 16) ??
                           Font.CreateDynamicFontFromOSFont("Noto Sans CJK SC", 16);
                if (_cjkFont != null)
                    Debug.Log("[UIStyle] Using system CJK font for Chinese text.");
            }

            // 3. Ultimate fallback — use OS default
            if (_pixelFont == null)
            {
                _pixelFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
                Debug.LogWarning("[UIStyle] No pixel font found, using Arial.");
            }
            if (_cjkFont == null)
            {
                _cjkFont = _pixelFont; // Same as pixel font (likely Arial) as last resort
                Debug.LogWarning("[UIStyle] No CJK font found, CJK may display as tofu (□□□).");
            }
        }

        /// <summary>
        /// Returns the best font for runtime UI. The game is Chinese-first, so
        /// the tactical HUD favors a clean CJK-capable font over a decorative
        /// pixel font even for short ASCII labels.
        /// </summary>
        public static Font GetFontForText(string text)
        {
            EnsureFonts();
            return _cjkFont != null ? _cjkFont : _pixelFont;
        }

        private static bool ContainsCJK(string text)
        {
            foreach (char c in text)
            {
                // CJK Unified Ideographs + Extensions + Compatibility + Kana + Hangul
                if ((c >= 0x4E00 && c <= 0x9FFF) ||   // CJK Unified Ideographs
                    (c >= 0x3400 && c <= 0x4DBF) ||    // CJK Extension A
                    (c >= 0x20000 && c <= 0x2A6DF) ||   // CJK Extension B
                    (c >= 0xF900 && c <= 0xFAFF) ||     // CJK Compatibility
                    (c >= 0x3040 && c <= 0x309F) ||     // Hiragana
                    (c >= 0x30A0 && c <= 0x30FF) ||     // Katakana
                    (c >= 0xAC00 && c <= 0xD7AF))       // Hangul
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Apply the best font to a Text component based on its content.
        /// Call this after setting text.text.
        /// </summary>
        public static void StylizeText(Text text)
        {
            if (text == null) return;
            text.font = GetFontForText(text.text);
            text.raycastTarget = false;
        }

        // ═══════════════════════════════════════════════
        //  PANEL CREATION
        // ═══════════════════════════════════════════════

        public static GameObject CreateStyledPanel(string name, Transform parent, Color bgColor,
            Color borderColor, float borderWidth = 2f)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = bgColor;

            if (borderWidth > 0f)
            {
                CreateBorderEdge(panel.transform, Vector2.up, Vector2.one,       Vector2.down  * borderWidth, borderColor);
                CreateBorderEdge(panel.transform, Vector2.zero, Vector2.right,   Vector2.up    * borderWidth, borderColor);
                CreateBorderEdge(panel.transform, Vector2.zero, Vector2.up,      Vector2.right * borderWidth, borderColor);
                CreateBorderEdge(panel.transform, Vector2.right, Vector2.one,    Vector2.left  * borderWidth, borderColor);
            }

            return panel;
        }

        private static void CreateBorderEdge(Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Color color)
        {
            var edge = new GameObject("Border", typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(parent, false);
            var rt = edge.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
            var img = edge.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        // ═══════════════════════════════════════════════
        //  DECORATIVE: SCANLINE OVERLAY
        // ═══════════════════════════════════════════════

        /// <summary>
        /// Add a subtle scanline effect over a panel (CRT monitor aesthetic).
        /// Creates alternating 1px horizontal lines at 0.04 alpha.
        /// </summary>
        public static void AddScanlines(Transform panel, float alpha = 0.04f)
        {
            // Create a procedural scanline texture
            int h = 4;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < h; y++)
            {
                float a = (y % 2 == 0) ? alpha : 0f;
                tex.SetPixel(0, y, new Color(0, 0, 0, a));
            }
            tex.Apply();

            var go = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(panel, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
        }

        // ═══════════════════════════════════════════════
        //  TEXT CREATION
        // ═══════════════════════════════════════════════

        public static Text CreateStyledText(string name, Transform parent, int fontSize,
            TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = CJKFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // ═══════════════════════════════════════════════
        //  BUTTONS
        // ═══════════════════════════════════════════════

        private static Sprite _btnSprite;

        public static Sprite LoadButtonSprite()
        {
            if (_btnSprite == null)
            {
                _btnSprite = Resources.Load<Sprite>("Sprites/UI/Buttons/buttonSquare_beige") ??
                             Resources.Load<Sprite>("Sprites/UI/Buttons/button_round_gloss");
            }
            return _btnSprite;
        }

        public static Button CreateStyledButton(string label, Transform parent, float height,
            UnityEngine.Events.UnityAction onClick, Color accentColor)
        {
            var btnGo = new GameObject(label + "_Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            var btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            var img = btnGo.GetComponent<Image>();
            var sprite = LoadButtonSprite();
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
            img.color = accentColor;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelText = labelGo.GetComponent<Text>();
            labelText.font = CJKFont;
            labelText.fontSize = 14;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;

            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.sizeDelta = Vector2.zero;

            return btn;
        }
    }
}
