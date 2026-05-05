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
        private Text sideText;
        private Text modalTitleText;
        private Text modalBodyText;
        private Image blackoutOverlay;
        private GameObject modalPanel;
        private Transform buttonRoot;
        private Transform modalButtonRoot;
        private string viewKey = string.Empty;

        public void Bind(SocialPrototypeController socialController)
        {
            controller = socialController;
            controller.Changed += Sync;
            BuildLayout();
            Sync();
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            RefreshText();
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.Changed -= Sync;
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

            GameObject overlay = CreatePanel("Blackout Overlay", transform, new Color(0f, 0f, 0f, 0f));
            blackoutOverlay = overlay.GetComponent<Image>();
            Stretch(overlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject topPanel = CreatePanel("Top Panel", transform, new Color(0.05f, 0.05f, 0.045f, 0.82f));
            Stretch(topPanel.GetComponent<RectTransform>(), new Vector2(0f, 0.84f), Vector2.one, Vector2.zero, Vector2.zero);

            titleText = CreateText("Title", topPanel.transform, 26, TextAnchor.MiddleLeft);
            Stretch(titleText.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.42f, 1f), new Vector2(20f, 0f), new Vector2(-8f, 0f));

            infoText = CreateText("Info", topPanel.transform, 17, TextAnchor.MiddleLeft);
            Stretch(infoText.GetComponent<RectTransform>(), new Vector2(0.42f, 0f), Vector2.one, new Vector2(8f, 0f), new Vector2(-20f, 0f));

            GameObject buttonPanel = CreatePanel("Button Panel", transform, new Color(0.08f, 0.075f, 0.065f, 0.9f));
            Stretch(buttonPanel.GetComponent<RectTransform>(), new Vector2(0.02f, 0.04f), new Vector2(0.24f, 0.8f), Vector2.zero, Vector2.zero);

            VerticalLayoutGroup layout = buttonPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 10;
            buttonRoot = buttonPanel.transform;

            GameObject sidePanel = CreatePanel("Intel Panel", transform, new Color(0.055f, 0.06f, 0.058f, 0.88f));
            Stretch(sidePanel.GetComponent<RectTransform>(), new Vector2(0.76f, 0.04f), new Vector2(0.98f, 0.8f), Vector2.zero, Vector2.zero);
            sideText = CreateText("Intel", sidePanel.transform, 15, TextAnchor.UpperLeft);
            Stretch(sideText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -14f));

            modalPanel = CreatePanel("Modal Panel", transform, new Color(0.045f, 0.047f, 0.043f, 0.94f));
            Stretch(modalPanel.GetComponent<RectTransform>(), new Vector2(0.28f, 0.16f), new Vector2(0.72f, 0.78f), Vector2.zero, Vector2.zero);

            modalTitleText = CreateText("Modal Title", modalPanel.transform, 28, TextAnchor.UpperCenter);
            Stretch(modalTitleText.GetComponent<RectTransform>(), new Vector2(0f, 0.76f), Vector2.one, new Vector2(18f, 8f), new Vector2(-18f, -8f));

            modalBodyText = CreateText("Modal Body", modalPanel.transform, 17, TextAnchor.UpperLeft);
            Stretch(modalBodyText.GetComponent<RectTransform>(), new Vector2(0f, 0.26f), new Vector2(1f, 0.76f), new Vector2(22f, 8f), new Vector2(-22f, -8f));

            GameObject modalButtons = CreatePanel("Modal Buttons", modalPanel.transform, new Color(0f, 0f, 0f, 0f));
            Stretch(modalButtons.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.26f), new Vector2(18f, 18f), new Vector2(-18f, -8f));
            VerticalLayoutGroup modalLayout = modalButtons.AddComponent<VerticalLayoutGroup>();
            modalLayout.padding = new RectOffset(0, 0, 0, 0);
            modalLayout.spacing = 8;
            modalButtonRoot = modalButtons.transform;
        }

        private void Sync()
        {
            string nextKey = BuildViewKey();

            if (nextKey != viewKey)
            {
                viewKey = nextKey;
                RebuildButtons();
            }

            RefreshText();
        }

        private string BuildViewKey()
        {
            string language = controller.Language.ToString();

            if (!controller.HasStarted)
            {
                return "start|" + language;
            }

            if (controller.IsGameOver)
            {
                return "gameover|" + language + "|" + controller.PlayerRole;
            }

            if (controller.IsRoleRevealVisible)
            {
                return "reveal|" + language + "|" + controller.PlayerRole;
            }

            if (controller.IsTaskChallengeVisible)
            {
                return "task|" + language + "|" + controller.TaskChallengeTitle;
            }

            if (controller.IsMeeting)
            {
                return "meeting|" + language + "|" + AliveNames();
            }

            return "play|" + language + "|" + controller.PlayerRole;
        }

        private string AliveNames()
        {
            List<string> names = new List<string>();

            foreach (SocialCharacter character in controller.Characters)
            {
                if (character.IsAlive)
                {
                    names.Add(character.CharacterName);
                }
            }

            return string.Join(",", names);
        }

        private void RebuildButtons()
        {
            ClearButtons();

            titleText.text = T("港区潜线", "Harbor Undercover");

            if (!controller.HasStarted)
            {
                AddButton(T("警察", "Police"), () => controller.StartGame(SocialRole.Police), 62f);
                AddButton(T("卧底", "Undercover"), () => controller.StartGame(SocialRole.Undercover), 62f);
                AddButton(T("黑帮", "Gang"), () => controller.StartGame(SocialRole.Gang), 62f);
                AddButton(LanguageLabel(), controller.ToggleLanguage, 46f);
                return;
            }

            if (controller.IsGameOver)
            {
                AddButton(T("再来一局", "Restart"), () => controller.StartGame(controller.PlayerRole), 52f);
                AddButton(T("卧底", "Undercover"), () => controller.StartGame(SocialRole.Undercover), 42f);
                AddButton(T("警察", "Police"), () => controller.StartGame(SocialRole.Police), 42f);
                AddButton(T("黑帮", "Gang"), () => controller.StartGame(SocialRole.Gang), 42f);
                AddButton(LanguageLabel(), controller.ToggleLanguage, 46f);
                return;
            }

            if (controller.IsRoleRevealVisible)
            {
                AddButtonTo(modalButtonRoot, T("开始行动", "Start Run"), controller.BeginRound, 56f);
                AddButton(T("卧底开局", "Undercover Run"), () => controller.StartGame(SocialRole.Undercover), 42f);
                AddButton(T("警察开局", "Police Run"), () => controller.StartGame(SocialRole.Police), 42f);
                AddButton(T("黑帮开局", "Gang Run"), () => controller.StartGame(SocialRole.Gang), 42f);
                AddButton(LanguageLabel(), controller.ToggleLanguage, 42f);
                return;
            }

            if (controller.IsTaskChallengeVisible)
            {
                for (int i = 0; i < controller.TaskChallengeOptions.Count; i++)
                {
                    int captured = i;
                    AddButtonTo(modalButtonRoot, (i + 1) + ". " + controller.TaskChallengeOptions[i], () => controller.ResolveTaskChallenge(captured), 50f);
                }

                return;
            }

            if (controller.IsMeeting)
            {
                foreach (SocialCharacter character in controller.Characters)
                {
                    if (character.IsAlive)
                    {
                        SocialCharacter captured = character;
                        string suffix = character.IsPlayer ? T("（你）", " (You)") : string.Empty;
                        AddButtonTo(modalButtonRoot, character.CharacterName + suffix, () => controller.CastVote(captured), 44f);
                    }
                }

                AddButtonTo(modalButtonRoot, T("跳过投票", "Skip Vote"), controller.SkipVote, 44f);
                AddButtonTo(modalButtonRoot, T("自动投票", "Auto Vote"), controller.ResolveAutoVote, 44f);
                return;
            }

            AddButton(T("卧底开局", "Undercover Run"), () => controller.StartGame(SocialRole.Undercover), 46f);
            AddButton(T("警察开局", "Police Run"), () => controller.StartGame(SocialRole.Police), 46f);
            AddButton(T("黑帮开局", "Gang Run"), () => controller.StartGame(SocialRole.Gang), 46f);
            AddButton(LanguageLabel(), controller.ToggleLanguage, 46f);
        }

        private void RefreshText()
        {
            titleText.text = T("港区潜线", "Harbor Undercover");
            sideText.text = BuildSideInfo();

            if (blackoutOverlay != null)
            {
                blackoutOverlay.color = controller.IsBlackout
                    ? new Color(0f, 0f, 0f, 0.34f)
                    : new Color(0f, 0f, 0f, 0f);
            }

            if (!controller.HasStarted)
            {
                SetModal(false, string.Empty, string.Empty);
                infoText.text = T("选择身份进入港区。目标：专案组完成取证或投出黑帮线人；黑帮线人制造混乱。", "Pick a role. The task force gathers evidence or votes out the gang mole; the mole creates chaos.");
                return;
            }

            if (controller.IsGameOver)
            {
                infoText.text = BuildGameInfo();
                SetModal(true, T("结算", "Result"), controller.ResultText + "\n\n" + TrimLog(controller.CaseLog));
                return;
            }

            if (controller.IsRoleRevealVisible)
            {
                infoText.text = BuildGameInfo();
                SetModal(
                    true,
                    T("身份揭示", "Role Reveal"),
                    controller.RoleBrief
                    + "\n\n" + controller.GoalBrief
                    + "\n\n" + T("一局完整流程：完成任务、观察路线、报告尸体、会议投票、触发胜负。", "Full loop: complete tasks, read routes, report bodies, vote in meetings, reach a result."));
                return;
            }

            if (controller.IsTaskChallengeVisible)
            {
                infoText.text = BuildGameInfo();
                SetModal(true, controller.TaskChallengeTitle, controller.TaskChallengeBody + "\n\n" + BuildTaskOptionText());
                return;
            }

            if (controller.IsMeeting)
            {
                string clue = string.IsNullOrEmpty(controller.CurrentClue)
                    ? T("没有可靠线索。只能根据路线和行为判断。", "No reliable clue. Judge by route and behavior.")
                    : controller.CurrentClue;

                infoText.text = BuildGameInfo();
                SetModal(
                    true,
                    T("会议", "Meeting"),
                    controller.MeetingReason
                    + "\n\n" + clue
                    + "\n\n" + T("路线", "Routes") + ": " + controller.RouteIntel
                    + "\n\n" + T("选择一个怀疑对象，或跳过。", "Vote for a suspect, or skip."));
                return;
            }

            SetModal(false, string.Empty, string.Empty);
            infoText.text = BuildGameInfo();
        }

        private void SetModal(bool visible, string title, string body)
        {
            if (modalPanel != null)
            {
                modalPanel.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            modalTitleText.text = title;
            modalBodyText.text = body;
        }

        private string BuildGameInfo()
        {
            string killText = controller.PlayerRole == SocialRole.Gang
                ? T("Q 击倒", "Q Kill") + " " + Mathf.CeilToInt(controller.PlayerKillCooldown) + "s"
                : T("Q 不可用", "Q Disabled");

            string blackoutText = controller.IsBlackout
                ? " | " + T("断电", "Blackout") + " " + Mathf.CeilToInt(controller.BlackoutTimer) + "s"
                : string.Empty;

            return T("WASD 移动 | E 交互 | R 报告 | ", "WASD Move | E Interact | R Report | ")
                + killText
                + " | " + controller.SpecialActionPrompt
                + "\n" + T("任务", "Tasks") + ": " + controller.CompletedTasks + "/" + controller.TotalTasks
                + " | " + T("证据链", "Evidence") + ": " + controller.EvidenceScore + "/" + controller.EvidenceTargetValue
                + " | " + T("身份", "Role") + ": " + RoleName(controller.PlayerRole)
                + " | " + T("剩余", "Time") + ": " + FormatTime(controller.RoundTimer)
                + " | " + T("会议", "Meetings") + ": " + controller.MeetingsCalled
                + " | " + T("紧急", "Emergency") + ": " + controller.EmergencyMeetingsCalled + "/" + controller.EmergencyMeetingLimitValue
                + blackoutText
                + "\n" + T("提示", "Prompt") + ": " + controller.InteractionPrompt
                + "\n" + T("路线", "Routes") + ": " + controller.RouteIntel
                + "\n" + controller.LastEvent
                + "\n" + TrimLog(controller.CaseLog);
        }

        private string BuildSideInfo()
        {
            if (!controller.HasStarted)
            {
                return T("选择一个身份开始完整 Demo。", "Pick a role to start the full demo.");
            }

            return T("任务清单", "Tasks")
                + "\n" + controller.TaskChecklist
                + "\n\n" + T("案件板", "Case Board")
                + "\n" + controller.CaseBoard
                + "\n\n" + T("嫌疑榜", "Suspects")
                + "\n" + controller.SuspectBoard
                + "\n\n" + T("存活名单", "Roster")
                + "\n" + controller.RosterSummary
                + "\n\n" + T("玩法", "Loop")
                + "\n" + T("取证 / 反侦察 / 追捕 / 报告 / 会议 / 投票", "Evidence / counter-surveillance / chase / report / meeting / vote");
        }

        private string BuildTaskOptionText()
        {
            List<string> lines = new List<string>();

            for (int i = 0; i < controller.TaskChallengeOptions.Count; i++)
            {
                lines.Add((i + 1) + ". " + controller.TaskChallengeOptions[i]);
            }

            return string.Join("\n", lines);
        }

        private static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            int minutes = totalSeconds / 60;
            int remainder = totalSeconds % 60;
            return minutes + ":" + remainder.ToString("00");
        }

        private static string TrimLog(string caseLog)
        {
            if (string.IsNullOrEmpty(caseLog))
            {
                return string.Empty;
            }

            string[] lines = caseLog.Split('\n');
            int count = Mathf.Min(3, lines.Length);
            return string.Join("\n", lines, 0, count);
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
            AddButtonTo(buttonRoot, label, onClick, height);
        }

        private void AddButtonTo(Transform root, string label, UnityEngine.Events.UnityAction onClick, float height)
        {
            Button button = CreateButton(label, root, height);
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
