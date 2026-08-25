using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 数字键盘小游戏（Among Us 风格密码输入）。
    /// 显示 4 位目标密码，9 宫格数字按钮点击输入，3 次尝试机会。
    /// </summary>
    public sealed class KeypadTask : MiniGameBase
    {
        private const int PasswordLength = 4;
        private const int MaxAttempts = 3;
        private const float SuccessDelay = 0.4f;
        private const float FailFlashDuration = 0.35f;

        private Canvas canvas;
        private Text passwordDisplay;
        private Text inputDisplay;
        private Text attemptsDisplay;
        private string targetPassword;
        private string currentInput = "";
        private int attemptsRemaining;

        public override void Show()
        {
            targetPassword = GeneratePassword();
            currentInput = "";
            attemptsRemaining = MaxAttempts;
            CreateUI();
            gameObject.SetActive(true);
        }

        public override void Hide()
        {
            if (canvas != null)
            {
                DestroyRuntimeObject(canvas.gameObject);
                canvas = null;
            }
            gameObject.SetActive(false);
        }

        private string GeneratePassword()
        {
            char[] digits = new char[PasswordLength];
            for (int i = 0; i < PasswordLength; i++)
            {
                digits[i] = (char)('0' + Random.Range(0, 10));
            }
            return new string(digits);
        }

        private void CreateUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("KeypadTaskCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 背景面板（深色主题）
            GameObject bg = CreatePanel(canvasObj, "Background", new Color(0.12f, 0.14f, 0.18f, 0.96f));
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // 优先使用 Resources 面板背景 sprite
            var panelBgSprite = GanglandUndercover.Art.MinigameArtCache.KeypadPanelBg
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
            CreateLabel(canvasObj, "密码输入", 24, new Vector2(0.5f, 0.92f), new Vector2(0.5f, 0.92f));

            // 目标密码显示
            passwordDisplay = CreateLabel(canvasObj, "目标密码：" + targetPassword, 20,
                new Vector2(0.5f, 0.84f), new Vector2(0.5f, 0.84f));

            // 输入显示
            inputDisplay = CreateLabel(canvasObj, "_ _ _ _", 28,
                new Vector2(0.5f, 0.74f), new Vector2(0.5f, 0.74f));
            inputDisplay.color = new Color(0.35f, 0.78f, 0.36f);

            // 剩余次数
            attemptsDisplay = CreateLabel(canvasObj, "剩余次数：" + attemptsRemaining, 16,
                new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f));

            // 数字键盘 3×3 网格
            CreateKeypadGrid(canvasObj);

            // 退格按钮
            CreateBackspaceButton(canvasObj);

            // 清除按钮
            CreateClearButton(canvasObj);
        }

        private void CreateKeypadGrid(GameObject parent)
        {
            // 3×4 网格（数字 1-9 + 右下角 0）
            for (int i = 0; i < 10; i++)
            {
                int digit = i == 9 ? 0 : i + 1; // 第 10 个按钮是 0，放在底部中间
                int col = i % 3;
                int row = i / 3;

                GameObject btn = CreatePanel(parent, "Btn_" + digit, new Color(0.22f, 0.25f, 0.32f, 1f));
                RectTransform btnRT = btn.GetComponent<RectTransform>();
                btnRT.anchorMin = new Vector2(0.25f + col * 0.17f, 0.18f + (2 - row) * 0.12f);
                btnRT.anchorMax = new Vector2(0.40f + col * 0.17f, 0.28f + (2 - row) * 0.12f);
                btnRT.offsetMin = Vector2.zero;
                btnRT.offsetMax = Vector2.zero;

                Button button = btn.AddComponent<Button>();
                int capturedDigit = digit;
                button.onClick.AddListener(() => OnDigitPressed(capturedDigit));

                ColorBlock cb = button.colors;
                cb.highlightedColor = new Color(0.32f, 0.35f, 0.44f, 1f);
                cb.pressedColor = new Color(0.12f, 0.14f, 0.20f, 1f);
                button.colors = cb;

                // 按钮文字
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(btn.transform);
                Text txt = labelObj.AddComponent<Text>();
                txt.text = digit.ToString();
                txt.fontSize = 24;
                txt.color = new Color(0.90f, 0.92f, 0.94f);
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                RectTransform labelRT = labelObj.GetComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;
            }
        }

        private void CreateBackspaceButton(GameObject parent)
        {
            GameObject btn = CreatePanel(parent, "Btn_Back", new Color(0.70f, 0.35f, 0.20f, 1f));
            RectTransform btnRT = btn.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.76f, 0.30f);
            btnRT.anchorMax = new Vector2(0.88f, 0.40f);
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;

            Button button = btn.AddComponent<Button>();
            button.onClick.AddListener(() => OnBackspacePressed());

            ColorBlock cb = button.colors;
            cb.highlightedColor = new Color(0.80f, 0.45f, 0.30f, 1f);
            cb.pressedColor = new Color(0.50f, 0.15f, 0.05f, 1f);
            button.colors = cb;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btn.transform);
            Text txt = labelObj.AddComponent<Text>();
            txt.text = "←";
            txt.fontSize = 20;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform labelRT = labelObj.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
        }

        private void CreateClearButton(GameObject parent)
        {
            GameObject btn = CreatePanel(parent, "Btn_Clear", new Color(0.65f, 0.30f, 0.30f, 1f));
            RectTransform btnRT = btn.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.76f, 0.18f);
            btnRT.anchorMax = new Vector2(0.88f, 0.28f);
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;

            Button button = btn.AddComponent<Button>();
            button.onClick.AddListener(() => OnClearPressed());

            ColorBlock cb = button.colors;
            cb.highlightedColor = new Color(0.75f, 0.40f, 0.40f, 1f);
            cb.pressedColor = new Color(0.45f, 0.10f, 0.10f, 1f);
            button.colors = cb;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btn.transform);
            Text txt = labelObj.AddComponent<Text>();
            txt.text = "C";
            txt.fontSize = 20;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform labelRT = labelObj.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
        }

        private void OnDigitPressed(int digit)
        {
            if (currentInput.Length >= PasswordLength) return;

            currentInput += digit.ToString();
            UpdateInputDisplay();

            if (currentInput.Length == PasswordLength)
            {
                StartCoroutine(CheckPasswordRoutine());
            }
        }

        private void OnBackspacePressed()
        {
            if (currentInput.Length > 0)
            {
                currentInput = currentInput.Substring(0, currentInput.Length - 1);
                UpdateInputDisplay();
            }
        }

        private void OnClearPressed()
        {
            currentInput = "";
            UpdateInputDisplay();
        }

        private void UpdateInputDisplay()
        {
            char[] display = new char[PasswordLength * 2 - 1]; // "X X X X"

            for (int i = 0; i < PasswordLength; i++)
            {
                if (i < currentInput.Length)
                {
                    display[i * 2] = currentInput[i];
                }
                else
                {
                    display[i * 2] = '_';
                }

                if (i < PasswordLength - 1)
                {
                    display[i * 2 + 1] = ' ';
                }
            }

            inputDisplay.text = new string(display);

            if (currentInput.Length > 0)
            {
                inputDisplay.color = new Color(0.35f, 0.78f, 0.36f);
            }
        }

        private IEnumerator CheckPasswordRoutine()
        {
            if (currentInput == targetPassword)
            {
                inputDisplay.color = new Color(0.35f, 0.78f, 0.36f);
                yield return new WaitForSeconds(SuccessDelay);
                Complete();
            }
            else
            {
                attemptsRemaining--;
                attemptsDisplay.text = "剩余次数：" + attemptsRemaining;

                // 红色闪烁
                inputDisplay.color = new Color(0.90f, 0.20f, 0.20f);
                yield return new WaitForSeconds(FailFlashDuration);

                if (attemptsRemaining <= 0)
                {
                    Cancel();
                }
                else
                {
                    currentInput = "";
                    UpdateInputDisplay();
                }
            }
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
