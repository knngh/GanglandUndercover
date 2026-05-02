using System.Collections.Generic;
using GanglandUndercover.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.SocialDeduction
{
    public sealed class SocialPrototypeHud : MonoBehaviour
    {
        private readonly List<Button> dynamicButtons = new List<Button>();

        private SocialPrototypeController controller;
        private Text titleText;
        private Text infoText;
        private Transform buttonRoot;

        public void Bind(SocialPrototypeController socialController)
        {
            controller = socialController;
            controller.Changed += Rebuild;
            BuildLayout();
            Rebuild();
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.Changed -= Rebuild;
            }
        }

        private void BuildLayout()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            gameObject.AddComponent<GraphicRaycaster>();

            GameObject topPanel = CreatePanel("Top Panel", transform, new Color(0.05f, 0.05f, 0.045f, 0.82f));
            Stretch(topPanel.GetComponent<RectTransform>(), new Vector2(0f, 0.84f), Vector2.one, Vector2.zero, Vector2.zero);

            titleText = CreateText("Title", topPanel.transform, 26, TextAnchor.MiddleLeft);
            Stretch(titleText.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.56f, 1f), new Vector2(20f, 0f), new Vector2(-8f, 0f));

            infoText = CreateText("Info", topPanel.transform, 17, TextAnchor.MiddleLeft);
            Stretch(infoText.GetComponent<RectTransform>(), new Vector2(0.56f, 0f), Vector2.one, new Vector2(8f, 0f), new Vector2(-20f, 0f));

            GameObject buttonPanel = CreatePanel("Button Panel", transform, new Color(0.08f, 0.075f, 0.065f, 0.9f));
            Stretch(buttonPanel.GetComponent<RectTransform>(), new Vector2(0.02f, 0.04f), new Vector2(0.32f, 0.8f), Vector2.zero, Vector2.zero);

            VerticalLayoutGroup layout = buttonPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 10;
            buttonRoot = buttonPanel.transform;
        }

        private void Rebuild()
        {
            ClearButtons();

            titleText.text = T("黑街疑云", "Gangland Suspects");

            if (!controller.HasStarted)
            {
                infoText.text = T("选择身份。目标：警方完成任务或投出黑帮；黑帮击倒并伪装。", "Pick a role. Police complete tasks or vote out gang. Gang kills and blends in.");
                AddButton(T("警察", "Police"), () => controller.StartGame(SocialRole.Police), 62f);
                AddButton(T("卧底", "Undercover"), () => controller.StartGame(SocialRole.Undercover), 62f);
                AddButton(T("黑帮", "Gang"), () => controller.StartGame(SocialRole.Gang), 62f);
                AddButton(LanguageLabel(), controller.ToggleLanguage, 46f);
                return;
            }

            if (controller.IsGameOver)
            {
                infoText.text = controller.ResultText;
                AddButton(T("再来一局", "Restart"), () => controller.StartGame(controller.PlayerRole), 62f);
                AddButton(LanguageLabel(), controller.ToggleLanguage, 46f);
                return;
            }

            if (controller.IsMeeting)
            {
                string clue = string.IsNullOrEmpty(controller.CurrentClue)
                    ? T("没有可靠线索。只能根据路线和行为判断。", "No reliable clue. Judge by route and behavior.")
                    : controller.CurrentClue;

                infoText.text = controller.MeetingReason
                    + "\n" + clue
                    + "\n" + T("投票阶段：选择一个怀疑对象，或跳过。", "Meeting: vote for a suspect, or skip.");

                foreach (SocialCharacter character in controller.Characters)
                {
                    if (character.IsAlive)
                    {
                        SocialCharacter captured = character;
                        string suffix = character.IsPlayer ? T("（你）", " (You)") : string.Empty;
                        AddButton(character.CharacterName + suffix, () => controller.CastVote(captured), 52f);
                    }
                }

                AddButton(T("跳过投票", "Skip Vote"), controller.SkipVote, 52f);
                return;
            }

            infoText.text = BuildGameInfo();
            AddButton(LanguageLabel(), controller.ToggleLanguage, 46f);
        }

        private string BuildGameInfo()
        {
            string killText = controller.PlayerRole == SocialRole.Gang
                ? T("Q 击倒", "Q Kill") + " " + Mathf.CeilToInt(controller.PlayerKillCooldown) + "s"
                : T("Q 不可用", "Q Disabled");

            string blackoutText = controller.IsBlackout
                ? " | " + T("断电", "Blackout") + " " + Mathf.CeilToInt(controller.BlackoutTimer) + "s"
                : string.Empty;

            return T("WASD 移动 | E 任务/紧急按钮 | R 报告尸体 | ", "WASD Move | E Task/Emergency | R Report | ")
                + killText
                + "\n" + T("任务", "Tasks") + ": " + controller.CompletedTasks + "/" + controller.TotalTasks
                + " | " + T("身份", "Role") + ": " + RoleName(controller.PlayerRole)
                + blackoutText
                + "\n" + controller.LastEvent;
        }

        private string RoleName(SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang:
                    return T("黑帮", "Gang");
                case SocialRole.Undercover:
                    return T("卧底", "Undercover");
                default:
                    return T("警察", "Police");
            }
        }

        private string LanguageLabel()
        {
            return controller.Language == GameLanguage.Chinese ? "Language: English" : "语言：中文";
        }

        private string T(string chinese, string english)
        {
            return controller.Language == GameLanguage.Chinese ? chinese : english;
        }

        private void AddButton(string label, UnityEngine.Events.UnityAction onClick, float height)
        {
            Button button = CreateButton(label, buttonRoot, height);
            button.onClick.AddListener(onClick);
            dynamicButtons.Add(button);
        }

        private void ClearButtons()
        {
            foreach (Button button in dynamicButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            dynamicButtons.Clear();
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private static Button CreateButton(string label, Transform parent, float height)
        {
            GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.26f, 0.22f, 0.15f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.26f, 0.22f, 0.15f, 1f);
            colors.highlightedColor = new Color(0.45f, 0.35f, 0.18f, 1f);
            colors.pressedColor = new Color(0.16f, 0.13f, 0.09f, 1f);
            button.colors = colors;

            Text text = CreateText("Label", buttonObject.transform, 16, TextAnchor.MiddleCenter);
            text.text = label;
            Stretch(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            return button;
        }

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = LoadBuiltinFont();
            text.fontSize = fontSize;
            text.color = new Color(0.94f, 0.9f, 0.78f, 1f);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Font LoadBuiltinFont()
        {
            Font font = TryLoadBuiltinFont("LegacyRuntime.ttf");

            if (font == null)
            {
                font = TryLoadBuiltinFont("Arial.ttf");
            }

            return font;
        }

        private static Font TryLoadBuiltinFont(string path)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(path);
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
