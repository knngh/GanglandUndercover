using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GanglandUndercover.Online
{
    public sealed class OnlineMatchHud : MonoBehaviour
    {
        private enum NotebookTab
        {
            Roster,
            Intel,
            Log,
            Services
        }

        private const float DesignScaleX = 2f;
        private const float DesignScaleY = 1.85f;

        private static readonly Color BackdropColor = new Color(0.015f, 0.019f, 0.021f, 0.18f);
        private static readonly Color DockColor = new Color(0.035f, 0.045f, 0.048f, 0.88f);
        private static readonly Color PanelColor = new Color(0.055f, 0.066f, 0.068f, 0.92f);
        private static readonly Color PanelDeepColor = new Color(0.027f, 0.034f, 0.038f, 0.96f);
        private static readonly Color TextColor = new Color(0.92f, 0.9f, 0.82f, 1f);
        private static readonly Color MutedTextColor = new Color(0.63f, 0.68f, 0.66f, 1f);
        private static readonly Color BlueAccent = new Color(0.08f, 0.62f, 0.82f, 1f);
        private static readonly Color AmberAccent = new Color(0.86f, 0.65f, 0.13f, 1f);
        private static readonly Color RedAccent = new Color(0.78f, 0.14f, 0.1f, 1f);
        private static readonly Color GreenAccent = new Color(0.12f, 0.66f, 0.34f, 1f);
        private static readonly Color VerticalSliceAccent = new Color(0.9f, 0.48f, 0.12f, 0.9f);

        private readonly List<GameObject> voteButtons = new List<GameObject>();
        private readonly List<GameObject> mapMarkers = new List<GameObject>();

        private OnlineMatchController controller;
        private Canvas canvas;
        private NotebookTab notebookTab = NotebookTab.Roster;
        private string dynamicKey = string.Empty;
        private string taskMiniGameKey = string.Empty;
        private bool taskChargeHeld;
        private float nextMapRefreshTime;

        private GameObject connectionGroup;
        private GameObject settingsGroup;
        private GameObject lobbyGroup;
        private GameObject actionGroup;
        private GameObject resultGroup;
        private GameObject hudBackdrop;
        private GameObject headerGroup;
        private GameObject leftDockGroup;
        private GameObject centerDockGroup;
        private GameObject rightDockGroup;
        private GameObject footerGroup;
        private GameObject meetingOverlay;
        private GameObject resultOverlay;
        private GameObject taskOverlay;
        private GameObject mapOverlay;
        private GameObject compactActionHud;

        private Text titleText;
        private Text phaseText;
        private Text voiceText;
        private Text statusText;
        private Text connectionStatusText;
        private Text lobbyStatusText;
        private Text actionStatusText;
        private Text resultStatusText;
        private Text centerTitleText;
        private Text centerBodyText;
        private Text notebookTitleText;
        private Text notebookBodyText;
        private Text footerText;
        private Text meetingTitleText;
        private Text meetingBodyText;
        private RectTransform meetingSeatRoot;
        private Text resultTitleText;
        private Text resultBodyText;
        private Text taskTitleText;
        private Text taskBodyText;
        private Text taskFeedbackText;
        private Text mapTitleText;
        private Text mapLegendText;
        private Text compactTopText;
        private Text compactPromptText;
        private Text compactAbilityText;
        private Text compactActionBarText;
        private Text minPlayersText;
        private Text maxPlayersText;
        private Text evidenceTargetText;

        private RectTransform evidenceFill;
        private RectTransform taskFill;
        private RectTransform survivalFill;
        private RectTransform resultEvidenceFill;
        private RectTransform resultTaskFill;
        private RectTransform resultSurvivalFill;
        private RectTransform taskProgressFill;
        private RectTransform taskMiniGameRoot;
        private RectTransform mapStaticRoot;
        private RectTransform mapMarkerRoot;

        private InputField playerNameInput;
        private InputField roomNameInput;
        private InputField joinAddressInput;
        private InputField relayJoinInput;
        private Slider minPlayersSlider;
        private Slider maxPlayersSlider;
        private Slider evidenceTargetSlider;
        private Toggle autoFillToggle;
        private Toggle revealRoleToggle;
        private Toggle proximityVoiceToggle;

        private Button hostButton;
        private Button clientButton;
        private Button relayHostButton;
        private Button relayClientButton;
        private Button localPreviewButton;
        private Button readyButton;
        private Button startButton;
        private Button fillBotsButton;
        private Button shutdownButton;
        private Button returnLobbyButton;

        private Button restartButton;
        private Button mapButton;
        private Button intelButton;
        private Button interactButton;
        private Button reportButton;
        private Button killButton;
        private Button abilityButton;

        private Transform voteButtonRoot;

        public int VerticalSliceStaticMapElementCount => mapStaticRoot == null ? 0 : CountNamedChildren(mapStaticRoot, "Vertical Slice");
        public int TaskMiniGameCanvasElementCount => taskMiniGameRoot == null ? 0 : CountNamedChildren(taskMiniGameRoot, "Task MiniGame");
        public int MeetingSeatCanvasElementCount => meetingSeatRoot == null ? 0 : CountNamedChildren(meetingSeatRoot, "Meeting Seat");
        public bool HasCompleteLayout => HasRequiredLayoutReferences();

        public void Bind(OnlineMatchController matchController)
        {
            controller = matchController;
            EnsureLayout();
            Refresh(true);
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            EnsureLayout();
            SyncCanvasCamera();

            if (taskChargeHeld && controller.LocalTaskInputGateActive)
            {
                controller.RequestChargeActiveTask();
            }

            Refresh(false);
        }

        private void EnsureLayout()
        {
            if (HasRequiredLayoutReferences())
            {
                return;
            }

            ClearGeneratedLayout();
            ResetLayoutReferences();
            BuildLayout();
        }

        private bool HasRequiredLayoutReferences()
        {
            return canvas != null
                && connectionGroup != null
                && settingsGroup != null
                && lobbyGroup != null
                && actionGroup != null
                && resultGroup != null
                && hudBackdrop != null
                && headerGroup != null
                && leftDockGroup != null
                && centerDockGroup != null
                && rightDockGroup != null
                && footerGroup != null
                && meetingOverlay != null
                && resultOverlay != null
                && taskOverlay != null
                && mapOverlay != null
                && compactActionHud != null
                && titleText != null
                && phaseText != null
                && voiceText != null
                && statusText != null
                && connectionStatusText != null
                && lobbyStatusText != null
                && actionStatusText != null
                && resultStatusText != null
                && centerTitleText != null
                && centerBodyText != null
                && notebookTitleText != null
                && notebookBodyText != null
                && footerText != null
                && meetingTitleText != null
                && meetingBodyText != null
                && meetingSeatRoot != null
                && resultTitleText != null
                && resultBodyText != null
                && taskTitleText != null
                && taskBodyText != null
                && taskFeedbackText != null
                && mapTitleText != null
                && mapLegendText != null
                && compactTopText != null
                && compactPromptText != null
                && compactAbilityText != null
                && compactActionBarText != null
                && minPlayersText != null
                && maxPlayersText != null
                && evidenceTargetText != null
                && evidenceFill != null
                && taskFill != null
                && survivalFill != null
                && resultEvidenceFill != null
                && resultTaskFill != null
                && resultSurvivalFill != null
                && taskProgressFill != null
                && taskMiniGameRoot != null
                && mapStaticRoot != null
                && mapMarkerRoot != null
                && playerNameInput != null
                && roomNameInput != null
                && joinAddressInput != null
                && relayJoinInput != null
                && minPlayersSlider != null
                && maxPlayersSlider != null
                && evidenceTargetSlider != null
                && autoFillToggle != null
                && revealRoleToggle != null
                && proximityVoiceToggle != null
                && hostButton != null
                && clientButton != null
                && relayHostButton != null
                && relayClientButton != null
                && localPreviewButton != null
                && readyButton != null
                && startButton != null
                && fillBotsButton != null
                && shutdownButton != null
                && returnLobbyButton != null
                && restartButton != null
                && mapButton != null
                && intelButton != null
                && interactButton != null
                && reportButton != null
                && killButton != null
                && abilityButton != null
                && voteButtonRoot != null;
        }

        private void ClearGeneratedLayout()
        {
            ClearGameObjects(voteButtons);
            ClearGameObjects(mapMarkers);

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            taskChargeHeld = false;
            nextMapRefreshTime = 0f;
            dynamicKey = string.Empty;
            taskMiniGameKey = string.Empty;
        }

        private void ResetLayoutReferences()
        {
            canvas = null;
            connectionGroup = null;
            settingsGroup = null;
            lobbyGroup = null;
            actionGroup = null;
            resultGroup = null;
            hudBackdrop = null;
            headerGroup = null;
            leftDockGroup = null;
            centerDockGroup = null;
            rightDockGroup = null;
            footerGroup = null;
            meetingOverlay = null;
            resultOverlay = null;
            taskOverlay = null;
            mapOverlay = null;
            compactActionHud = null;
            titleText = null;
            phaseText = null;
            voiceText = null;
            statusText = null;
            connectionStatusText = null;
            lobbyStatusText = null;
            actionStatusText = null;
            resultStatusText = null;
            centerTitleText = null;
            centerBodyText = null;
            notebookTitleText = null;
            notebookBodyText = null;
            footerText = null;
            meetingTitleText = null;
            meetingBodyText = null;
            meetingSeatRoot = null;
            resultTitleText = null;
            resultBodyText = null;
            taskTitleText = null;
            taskBodyText = null;
            taskFeedbackText = null;
            mapTitleText = null;
            mapLegendText = null;
            compactTopText = null;
            compactPromptText = null;
            compactAbilityText = null;
            compactActionBarText = null;
            minPlayersText = null;
            maxPlayersText = null;
            evidenceTargetText = null;
            evidenceFill = null;
            taskFill = null;
            survivalFill = null;
            resultEvidenceFill = null;
            resultTaskFill = null;
            resultSurvivalFill = null;
            taskProgressFill = null;
            taskMiniGameRoot = null;
            mapStaticRoot = null;
            mapMarkerRoot = null;
            playerNameInput = null;
            roomNameInput = null;
            joinAddressInput = null;
            relayJoinInput = null;
            minPlayersSlider = null;
            maxPlayersSlider = null;
            evidenceTargetSlider = null;
            autoFillToggle = null;
            revealRoleToggle = null;
            proximityVoiceToggle = null;
            hostButton = null;
            clientButton = null;
            relayHostButton = null;
            relayClientButton = null;
            localPreviewButton = null;
            readyButton = null;
            startButton = null;
            fillBotsButton = null;
            shutdownButton = null;
            returnLobbyButton = null;
            restartButton = null;
            mapButton = null;
            intelButton = null;
            interactButton = null;
            reportButton = null;
            killButton = null;
            abilityButton = null;
            voteButtonRoot = null;
        }

        private void BuildLayout()
        {
            EnsureEventSystem();

            canvas = GetOrAddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 3000;
            canvas.planeDistance = 0.8f;
            SyncCanvasCamera();

            CanvasScaler scaler = GetOrAddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GetOrAddComponent<GraphicRaycaster>();

            hudBackdrop = CreatePanel("HUD Backdrop", transform, BackdropColor);
            Stretch(hudBackdrop.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Transform header = CreateHorizontalPanel(
                "Header",
                transform,
                new Color(0.022f, 0.031f, 0.034f, 0.93f),
                new Vector2(0.015f, 0.915f),
                new Vector2(0.985f, 0.985f),
                new RectOffset(16, 16, 10, 10),
                14f);
            headerGroup = header.gameObject;
            titleText = CreateText("Title", header, 24, TextAnchor.MiddleLeft, TextColor);
            AddLayout(titleText.gameObject, 0f, 0f, 2f);
            phaseText = CreateText("Phase", header, 16, TextAnchor.MiddleLeft, TextColor);
            AddLayout(phaseText.gameObject, 0f, 0f, 1.55f);
            voiceText = CreateText("Voice", header, 14, TextAnchor.MiddleRight, MutedTextColor);
            AddLayout(voiceText.gameObject, 0f, 0f, 1.75f);

            Transform leftDock = CreateVerticalPanel(
                "Left Dock",
                transform,
                DockColor,
                new Vector2(0.015f, 0.095f),
                new Vector2(0.295f, 0.895f),
                new RectOffset(12, 12, 12, 12),
                10f);
            leftDockGroup = leftDock.gameObject;
            BuildLeftDock(leftDock);

            Transform centerDock = CreateVerticalPanel(
                "Center Dock",
                transform,
                DockColor,
                new Vector2(0.31f, 0.095f),
                new Vector2(0.69f, 0.895f),
                new RectOffset(12, 12, 12, 12),
                10f);
            centerDockGroup = centerDock.gameObject;
            BuildCenterDock(centerDock);

            Transform rightDock = CreateVerticalPanel(
                "Right Dock",
                transform,
                DockColor,
                new Vector2(0.705f, 0.095f),
                new Vector2(0.985f, 0.895f),
                new RectOffset(12, 12, 12, 12),
                10f);
            rightDockGroup = rightDock.gameObject;
            BuildRightDock(rightDock);

            Transform footer = CreateHorizontalPanel(
                "Footer",
                transform,
                new Color(0.02f, 0.026f, 0.028f, 0.9f),
                new Vector2(0.015f, 0.015f),
                new Vector2(0.985f, 0.078f),
                new RectOffset(14, 14, 8, 8),
                10f);
            footerGroup = footer.gameObject;
            footerText = CreateText("Footer Text", footer, 14, TextAnchor.MiddleLeft, MutedTextColor);
            AddLayout(footerText.gameObject, 0f, 0f, 1f);

            BuildMapOverlay();
            BuildMeetingOverlay();
            BuildResultOverlay();
            BuildTaskOverlay();
            BuildCompactActionHud();
        }

        private void BuildLeftDock(Transform leftDock)
        {
            Transform connection = CreateSection("连接与开局", leftDock, 246f);
            connectionGroup = connection.gameObject;
            playerNameInput = CreateInputRow(connection, "玩家", "港区玩家", value => controller.SetLocalPlayerName(value));
            roomNameInput = CreateInputRow(connection, "房间", "九龙港区夜局", value => controller.SetRoomName(value));
            joinAddressInput = CreateInputRow(connection, "直连", "127.0.0.1", value => controller.SetJoinAddress(value));
            relayJoinInput = CreateInputRow(connection, "房间码", "Relay Code", value => controller.SetRelayJoinInput(value));

            Transform connectionRowA = CreateButtonRow(connection, 42f);
            hostButton = CreateButton("创建 Host", connectionRowA, 42f, () => controller.RequestHost());
            clientButton = CreateButton("加入 Client", connectionRowA, 42f, () => controller.RequestClient());

            Transform connectionRowB = CreateButtonRow(connection, 42f);
            relayHostButton = CreateButton("Relay 开房", connectionRowB, 42f, () => controller.RequestRelayHost());
            relayClientButton = CreateButton("Relay 加入", connectionRowB, 42f, () => controller.RequestRelayClient());

            Transform connectionRowC = CreateButtonRow(connection, 42f);
            localPreviewButton = CreateButton("本地完整局", connectionRowC, 42f, () => controller.RequestLocalPreview());
            shutdownButton = CreateButton("离开房间", connectionRowC, 42f, () => controller.RequestShutdown());
            connectionStatusText = CreateText("Connection Status", connection, 13, TextAnchor.UpperLeft, MutedTextColor);
            AddLayout(connectionStatusText.gameObject, 70f, 0f, 0f);

            Transform settings = CreateSection("房间规则", leftDock, 258f);
            settingsGroup = settings.gameObject;
            minPlayersSlider = CreateSliderRow(settings, "最少人数", controller.MinimumRoomPlayersValue, controller.MaximumRoomPlayersValue, value => controller.SetRoomMinPlayers(Mathf.RoundToInt(value)), out minPlayersText);
            maxPlayersSlider = CreateSliderRow(settings, "最大人数", controller.MinimumRoomPlayersValue, controller.MaximumRoomPlayersValue, value => controller.SetRoomMaxPlayers(Mathf.RoundToInt(value)), out maxPlayersText);
            evidenceTargetSlider = CreateSliderRow(settings, "证据目标", 34f, 56f, value => controller.SetEvidenceTarget(Mathf.RoundToInt(value)), out evidenceTargetText);
            autoFillToggle = CreateToggle("人数不足时 AI 补位", settings, value => controller.SetAutoFillAi(value));
            revealRoleToggle = CreateToggle("出局时公开身份", settings, value => controller.SetRevealRoleOnEject(value));
            proximityVoiceToggle = CreateToggle("行动阶段近距离语音", settings, value => controller.SetProximityVoiceEnabled(value));

            Transform lobby = CreateSection("房间流程", leftDock, 166f);
            lobbyGroup = lobby.gameObject;
            lobbyStatusText = CreateText("Lobby Status", lobby, 14, TextAnchor.UpperLeft, TextColor);
            AddLayout(lobbyStatusText.gameObject, 66f, 0f, 0f);
            Transform lobbyRow = CreateButtonRow(lobby, 42f);
            readyButton = CreateButton("Ready", lobbyRow, 42f, () => controller.ToggleLocalReady());
            startButton = CreateButton("开始", lobbyRow, 42f, () => controller.RequestStartMatch());
            fillBotsButton = CreateButton("补 AI 开局", lobby, 42f, () => controller.RequestFillBotsAndStart());

            Transform action = CreateSection("行动快捷", leftDock, 252f);
            actionGroup = action.gameObject;
            actionStatusText = CreateText("Action Status", action, 14, TextAnchor.UpperLeft, TextColor);
            AddLayout(actionStatusText.gameObject, 86f, 0f, 0f);
            Transform actionRowA = CreateButtonRow(action, 42f);
            interactButton = CreateButton("E 互动", actionRowA, 42f, () => controller.RequestAction(OnlineActionType.Interact));
            reportButton = CreateButton("R 报案", actionRowA, 42f, () => controller.RequestAction(OnlineActionType.Report));
            Transform actionRowB = CreateButtonRow(action, 42f);
            killButton = CreateButton("Q 击倒", actionRowB, 42f, () => controller.RequestAction(OnlineActionType.Kill));
            abilityButton = CreateButton("F 技能", actionRowB, 42f, () => controller.RequestAction(OnlineActionType.Ability));
            Transform actionRowC = CreateButtonRow(action, 42f);
            mapButton = CreateButton("M 大地图", actionRowC, 42f, () => controller.ToggleTacticalMap());
            intelButton = CreateButton("I 案情板", actionRowC, 42f, () => controller.ToggleIntelBoard());

            Transform result = CreateSection("结算控制", leftDock, 130f);
            resultGroup = result.gameObject;
            resultStatusText = CreateText("Result Status", result, 13, TextAnchor.UpperLeft, TextColor);
            AddLayout(resultStatusText.gameObject, 40f, 0f, 0f);
            Transform resultRow = CreateButtonRow(result, 42f);
            restartButton = CreateButton("重开", resultRow, 42f, () => controller.RequestRestartMatch());
            returnLobbyButton = CreateButton("返回房间", resultRow, 42f, () => controller.RequestReturnToLobby());
        }

        private void BuildCenterDock(Transform centerDock)
        {
            Transform headline = CreateSection("当前对局", centerDock, 120f);
            centerTitleText = CreateText("Center Title", headline, 22, TextAnchor.UpperLeft, TextColor);
            AddLayout(centerTitleText.gameObject, 42f, 0f, 0f);
            statusText = CreateText("Status", headline, 14, TextAnchor.UpperLeft, MutedTextColor);
            AddLayout(statusText.gameObject, 48f, 0f, 0f);

            Transform progress = CreateSection("进度", centerDock, 150f);
            evidenceFill = CreateProgressBar(progress, "证据链", BlueAccent);
            taskFill = CreateProgressBar(progress, "任务完成", AmberAccent);
            survivalFill = CreateProgressBar(progress, "存活人数", GreenAccent);

            Transform details = CreateSection("目标与局势", centerDock, 360f);
            centerBodyText = CreateScrollText("Center Body", details, 322f);
        }

        private void BuildRightDock(Transform rightDock)
        {
            Transform tabsSection = CreateSection("情报板", rightDock, 72f);
            Transform tabs = CreateButtonRow(tabsSection, 38f);
            CreateButton("人员", tabs, 38f, () => SelectNotebook(NotebookTab.Roster));
            CreateButton("案情", tabs, 38f, () => SelectNotebook(NotebookTab.Intel));
            CreateButton("日志", tabs, 38f, () => SelectNotebook(NotebookTab.Log));
            CreateButton("服务", tabs, 38f, () => SelectNotebook(NotebookTab.Services));

            Transform body = CreateSection("情报内容", rightDock, 554f);
            notebookTitleText = CreateText("Notebook Title", body, 17, TextAnchor.UpperLeft, TextColor);
            AddLayout(notebookTitleText.gameObject, 28f, 0f, 0f);
            notebookBodyText = CreateScrollText("Notebook Body", body, 500f);
        }

        private void BuildMeetingOverlay()
        {
            meetingOverlay = CreatePanel("Meeting Overlay", transform, new Color(0.008f, 0.01f, 0.012f, 0.82f));
            Stretch(meetingOverlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Transform modal = CreateVerticalPanel(
                "Meeting Panel",
                meetingOverlay.transform,
                new Color(0.038f, 0.048f, 0.052f, 0.97f),
                new Vector2(0.16f, 0.14f),
                new Vector2(0.84f, 0.86f),
                new RectOffset(22, 22, 18, 18),
                12f);

            meetingTitleText = CreateText("Meeting Title", modal, 28, TextAnchor.MiddleCenter, TextColor);
            AddLayout(meetingTitleText.gameObject, 48f, 0f, 0f);
            meetingBodyText = CreateText("Meeting Body", modal, 15, TextAnchor.UpperLeft, TextColor);
            AddLayout(meetingBodyText.gameObject, 118f, 0f, 0f);

            GameObject seatBoard = CreatePanel("Meeting Seat Board", modal, new Color(0.024f, 0.03f, 0.032f, 0.96f));
            AddLayout(seatBoard, 126f, 0f, 0f);
            meetingSeatRoot = seatBoard.GetComponent<RectTransform>();

            Text voteHeader = CreateText("Vote Header", modal, 17, TextAnchor.UpperLeft, AmberAccent);
            voteHeader.text = "投票面板";
            AddLayout(voteHeader.gameObject, 28f, 0f, 0f);

            GameObject voteScrollObject = CreatePanel("Vote Scroll", modal, PanelDeepColor);
            AddLayout(voteScrollObject, 250f, 0f, 1f, 1f);
            ScrollRect voteScroll = voteScrollObject.AddComponent<ScrollRect>();
            voteScroll.horizontal = false;
            voteScroll.vertical = true;

            GameObject voteViewport = CreatePanel("Vote Viewport", voteScrollObject.transform, new Color(0f, 0f, 0f, 0f));
            voteViewport.AddComponent<RectMask2D>();
            RectTransform voteViewportRect = voteViewport.GetComponent<RectTransform>();
            Stretch(voteViewportRect, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            GameObject voteContent = new GameObject("Vote Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            voteContent.transform.SetParent(voteViewport.transform, false);
            RectTransform voteContentRect = voteContent.GetComponent<RectTransform>();
            voteContentRect.anchorMin = new Vector2(0f, 1f);
            voteContentRect.anchorMax = new Vector2(1f, 1f);
            voteContentRect.pivot = new Vector2(0.5f, 1f);
            voteContentRect.anchoredPosition = Vector2.zero;
            voteContentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup voteLayout = voteContent.GetComponent<VerticalLayoutGroup>();
            voteLayout.spacing = 8;
            voteLayout.childControlHeight = true;
            voteLayout.childControlWidth = true;
            voteLayout.childForceExpandHeight = false;
            voteLayout.childForceExpandWidth = true;

            ContentSizeFitter voteFitter = voteContent.GetComponent<ContentSizeFitter>();
            voteFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            voteFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            voteScroll.viewport = voteViewportRect;
            voteScroll.content = voteContentRect;
            voteButtonRoot = voteContent.transform;
        }

        private void BuildResultOverlay()
        {
            resultOverlay = CreatePanel("Result Overlay", transform, new Color(0.008f, 0.01f, 0.012f, 0.8f));
            Stretch(resultOverlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Transform modal = CreateVerticalPanel(
                "Result Panel",
                resultOverlay.transform,
                new Color(0.04f, 0.047f, 0.044f, 0.98f),
                new Vector2(0.20f, 0.18f),
                new Vector2(0.80f, 0.82f),
                new RectOffset(24, 24, 22, 22),
                12f);

            resultTitleText = CreateText("Result Title", modal, 30, TextAnchor.MiddleCenter, TextColor);
            AddLayout(resultTitleText.gameObject, 52f, 0f, 0f);
            resultEvidenceFill = CreateProgressBar(modal, "证据链", BlueAccent);
            resultTaskFill = CreateProgressBar(modal, "任务完成", AmberAccent);
            resultSurvivalFill = CreateProgressBar(modal, "存活人数", GreenAccent);
            resultBodyText = CreateText("Result Body", modal, 15, TextAnchor.UpperLeft, TextColor);
            AddLayout(resultBodyText.gameObject, 180f, 0f, 0f);

            Transform resultButtons = CreateButtonRow(modal, 46f);
            CreateButton("重开同房间", resultButtons, 46f, () => controller.RequestRestartMatch());
            CreateButton("返回房间", resultButtons, 46f, () => controller.RequestReturnToLobby());
        }

        private void BuildTaskOverlay()
        {
            taskOverlay = CreatePanel("Task Overlay", transform, new Color(0.008f, 0.01f, 0.012f, 0.72f));
            Stretch(taskOverlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Transform modal = CreateVerticalPanel(
                "Task Panel",
                taskOverlay.transform,
                new Color(0.036f, 0.046f, 0.05f, 0.98f),
                new Vector2(0.25f, 0.20f),
                new Vector2(0.75f, 0.80f),
                new RectOffset(24, 24, 20, 20),
                12f);

            taskTitleText = CreateText("Task Title", modal, 26, TextAnchor.MiddleCenter, TextColor);
            AddLayout(taskTitleText.gameObject, 42f, 0f, 0f);
            taskBodyText = CreateText("Task Body", modal, 15, TextAnchor.UpperLeft, TextColor);
            AddLayout(taskBodyText.gameObject, 86f, 0f, 0f);

            GameObject miniGameObject = CreatePanel("Task MiniGame Board", modal, new Color(0.026f, 0.032f, 0.034f, 0.96f));
            AddLayout(miniGameObject, 138f, 0f, 0f);
            taskMiniGameRoot = miniGameObject.GetComponent<RectTransform>();

            taskProgressFill = CreateProgressBar(modal, "现场进度", BlueAccent);
            taskFeedbackText = CreateText("Task Feedback", modal, 15, TextAnchor.MiddleCenter, AmberAccent);
            AddLayout(taskFeedbackText.gameObject, 30f, 0f, 0f);

            Transform stepRow = CreateButtonRow(modal, 50f);
            CreateButton("键 1", stepRow, 50f, () => controller.RequestTaskStep(controller.ActiveTaskCorrectStepOne));
            CreateButton("键 2", stepRow, 50f, () => controller.RequestTaskStep(controller.ActiveTaskCorrectStepTwo));
            CreateButton("键 3", stepRow, 50f, () => controller.RequestTaskStep(controller.ActiveTaskCorrectStepThree));

            Transform taskButtons = CreateButtonRow(modal, 46f);
            Button chargeButton = CreateButton("按住扫描", taskButtons, 46f, () => { });
            ConfigureHoldButton(chargeButton);
            CreateButton("退出", taskButtons, 46f, () => controller.RequestCancelActiveTask());
        }

        private void BuildCompactActionHud()
        {
            compactActionHud = CreatePanel("Compact Action HUD", transform, new Color(0f, 0f, 0f, 0f));
            Stretch(compactActionHud.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Transform topLeft = CreateVerticalPanel(
                "Compact Top Left",
                compactActionHud.transform,
                new Color(0.022f, 0.03f, 0.032f, 0.84f),
                new Vector2(0.015f, 0.79f),
                new Vector2(0.31f, 0.975f),
                new RectOffset(12, 12, 10, 10),
                6f);
            compactTopText = CreateText("Compact Top Text", topLeft, 15, TextAnchor.UpperLeft, TextColor);
            AddLayout(compactTopText.gameObject, 78f, 0f, 0f);

            Transform bottomCenter = CreateVerticalPanel(
                "Compact Bottom Center",
                compactActionHud.transform,
                new Color(0.02f, 0.026f, 0.028f, 0.9f),
                new Vector2(0.25f, 0.015f),
                new Vector2(0.75f, 0.13f),
                new RectOffset(16, 16, 8, 8),
                6f);
            compactActionBarText = CreateText("Compact Action Bar", bottomCenter, 16, TextAnchor.MiddleCenter, TextColor);
            AddLayout(compactActionBarText.gameObject, 34f, 0f, 0f);
            compactPromptText = CreateText("Compact Prompt", bottomCenter, 13, TextAnchor.MiddleCenter, MutedTextColor);
            AddLayout(compactPromptText.gameObject, 32f, 0f, 0f);

            Transform bottomLeft = CreateVerticalPanel(
                "Compact Bottom Left",
                compactActionHud.transform,
                new Color(0.022f, 0.03f, 0.032f, 0.84f),
                new Vector2(0.015f, 0.015f),
                new Vector2(0.29f, 0.19f),
                new RectOffset(12, 12, 10, 10),
                6f);
            compactAbilityText = CreateText("Compact Ability", bottomLeft, 14, TextAnchor.UpperLeft, TextColor);
            AddLayout(compactAbilityText.gameObject, 60f, 0f, 0f);
        }

        private void BuildMapOverlay()
        {
            mapOverlay = CreatePanel("Tactical Map Overlay", transform, new Color(0.008f, 0.01f, 0.012f, 0.76f));
            Stretch(mapOverlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Transform modal = CreateVerticalPanel(
                "Tactical Map Panel",
                mapOverlay.transform,
                new Color(0.032f, 0.042f, 0.045f, 0.98f),
                new Vector2(0.10f, 0.10f),
                new Vector2(0.90f, 0.90f),
                new RectOffset(24, 24, 18, 18),
                10f);

            mapTitleText = CreateText("Map Title", modal, 24, TextAnchor.MiddleLeft, TextColor);
            AddLayout(mapTitleText.gameObject, 38f, 0f, 0f);

            GameObject mapObject = CreatePanel("Map Board", modal, new Color(0.032f, 0.04f, 0.044f, 1f));
            AddLayout(mapObject, 0f, 0f, 1f, 1f);
            RectTransform mapRect = mapObject.GetComponent<RectTransform>();

            GameObject staticRootObject = new GameObject("Static Map Layer", typeof(RectTransform));
            staticRootObject.transform.SetParent(mapObject.transform, false);
            mapStaticRoot = staticRootObject.GetComponent<RectTransform>();
            Stretch(mapStaticRoot, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));

            GameObject markerRootObject = new GameObject("Map Markers", typeof(RectTransform));
            markerRootObject.transform.SetParent(mapObject.transform, false);
            mapMarkerRoot = markerRootObject.GetComponent<RectTransform>();
            Stretch(mapMarkerRoot, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));

            BuildStaticMapLayer();

            mapLegendText = CreateText("Map Legend", modal, 14, TextAnchor.MiddleLeft, MutedTextColor);
            mapLegendText.text = "黄: 玩家 | 青: 任务 | 橙框: 垂直切片核心区 | 红: 破坏/尸体 | 灰: 出局 | M 收起";
            AddLayout(mapLegendText.gameObject, 30f, 0f, 0f);

            Transform closeRow = CreateButtonRow(modal, 42f);
            CreateButton("收起大地图", closeRow, 42f, () => controller.ToggleTacticalMap());
            _ = mapRect;
        }

        private void Refresh(bool force)
        {
            if (controller == null)
            {
                return;
            }

            if (!HasRequiredLayoutReferences())
            {
                EnsureLayout();

                if (!HasRequiredLayoutReferences())
                {
                    return;
                }
            }

            string key = BuildDynamicKey();

            if (force || key != dynamicKey)
            {
                dynamicKey = key;
                RebuildVoteButtons();
            }

            bool online = controller.IsOnline;
            OnlineMatchPhase phase = controller.Phase;
            connectionGroup.SetActive(!online);
            bool compactAction = online && phase == OnlineMatchPhase.Action && !controller.IntelBoardOpen && !controller.TacticalMapOpen && !controller.LocalTaskInputGateActive;
            hudBackdrop.SetActive(!compactAction);
            headerGroup.SetActive(!compactAction);
            leftDockGroup.SetActive(!compactAction);
            centerDockGroup.SetActive(!compactAction);
            rightDockGroup.SetActive(!compactAction);
            footerGroup.SetActive(!compactAction);
            settingsGroup.SetActive(!online || phase == OnlineMatchPhase.Lobby);
            lobbyGroup.SetActive(online && phase == OnlineMatchPhase.Lobby);
            actionGroup.SetActive(online && phase != OnlineMatchPhase.Lobby && phase != OnlineMatchPhase.Result && !compactAction);
            resultGroup.SetActive(online && phase == OnlineMatchPhase.Result);
            compactActionHud.SetActive(compactAction);
            meetingOverlay.SetActive(online && (phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting));
            resultOverlay.SetActive(online && phase == OnlineMatchPhase.Result);
            taskOverlay.SetActive(controller.LocalTaskInputGateActive);
            mapOverlay.SetActive(online && phase == OnlineMatchPhase.Action && controller.TacticalMapOpen);

            RefreshInputs();
            RefreshButtons();
            RefreshTexts();
            RefreshProgressBars();

            if (meetingOverlay.activeSelf)
            {
                RefreshMeetingSeatBoard();
            }

            if (mapOverlay.activeSelf)
            {
                RefreshMapMarkers();
            }
        }

        private string BuildDynamicKey()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(controller.Phase)
                .Append('|').Append(controller.Players.Count)
                .Append('|').Append(controller.ReadyPlayerCountValue)
                .Append('|').Append(controller.BodyCount)
                .Append('|').Append(controller.CaseLogCount)
                .Append('|').Append(controller.LocalReady)
                .Append('|').Append(controller.CanStartMatch)
                .Append('|').Append(controller.EvidenceScore)
                .Append('|').Append(controller.CompletedTaskCount)
                .Append('|').Append(controller.SabotagedTaskCount)
                .Append('|').Append(controller.VoteTallySummary)
                .Append('|').Append(controller.LocalTaskInputGateActive);
            return builder.ToString();
        }

        private void RefreshInputs()
        {
            SyncInput(playerNameInput, controller.LocalPlayerName);
            SyncInput(roomNameInput, controller.RoomName);
            SyncInput(joinAddressInput, controller.JoinAddress);
            SyncInput(relayJoinInput, controller.RelayJoinInput);

            minPlayersSlider.SetValueWithoutNotify(controller.RoomMinPlayers);
            minPlayersSlider.interactable = CanEditRoomSettings();
            maxPlayersSlider.minValue = controller.RoomMinPlayers;
            maxPlayersSlider.SetValueWithoutNotify(controller.RoomMaxPlayers);
            maxPlayersSlider.interactable = CanEditRoomSettings();
            evidenceTargetSlider.SetValueWithoutNotify(controller.EvidenceTarget);
            evidenceTargetSlider.interactable = CanEditRoomSettings();

            autoFillToggle.SetIsOnWithoutNotify(controller.AutoFillAi);
            autoFillToggle.interactable = CanEditRoomSettings();
            revealRoleToggle.SetIsOnWithoutNotify(controller.RevealRoleOnEject);
            revealRoleToggle.interactable = CanEditRoomSettings();
            proximityVoiceToggle.SetIsOnWithoutNotify(controller.ProximityVoiceEnabled);
            proximityVoiceToggle.interactable = CanEditRoomSettings();

            minPlayersText.text = "最少 " + controller.RoomMinPlayers;
            maxPlayersText.text = "最大 " + controller.RoomMaxPlayers;
            evidenceTargetText.text = "目标 " + controller.EvidenceTarget;
        }

        private void RefreshButtons()
        {
            bool offline = !controller.IsOnline;
            bool relayReady = !controller.RelayOperationInProgress;
            hostButton.interactable = offline;
            clientButton.interactable = offline;
            relayHostButton.interactable = offline && relayReady;
            relayClientButton.interactable = offline && relayReady;
            localPreviewButton.interactable = offline;
            shutdownButton.interactable = controller.IsOnline;

            readyButton.interactable = controller.IsOnline && controller.Phase == OnlineMatchPhase.Lobby;
            SetButtonText(readyButton, controller.LocalReady ? "取消 Ready" : "Ready");
            startButton.interactable = controller.IsHost && controller.CanStartMatch;
            fillBotsButton.interactable = controller.IsHost && controller.Phase == OnlineMatchPhase.Lobby;

            bool actionPlayable = controller.Phase == OnlineMatchPhase.Action && controller.LocalAlive;
            interactButton.interactable = actionPlayable;
            reportButton.interactable = actionPlayable;
            killButton.interactable = actionPlayable && controller.LocalRole == OnlineRole.Gang;
            abilityButton.interactable = actionPlayable;
            mapButton.interactable = controller.IsOnline;
            intelButton.interactable = controller.IsOnline;

            restartButton.interactable = controller.IsHost && controller.Phase == OnlineMatchPhase.Result;
            returnLobbyButton.interactable = controller.IsOnline && controller.Phase == OnlineMatchPhase.Result;
        }

        private void RefreshTexts()
        {
            string relayCode = string.IsNullOrWhiteSpace(controller.RelayJoinCode) ? string.Empty : " | Relay " + controller.RelayJoinCode;
            titleText.text = "港区潜线 | " + controller.RoomName + relayCode;
            phaseText.text = controller.PhaseDisplayName + " | " + controller.MatchTimeText + "/20:00 | 证据 " + controller.EvidenceScore + "/" + controller.EvidenceTarget + " | 存活 " + controller.AlivePlayerCount + "/" + Mathf.Max(1, controller.Players.Count);
            voiceText.text = controller.VoiceHudLine;
            statusText.text = controller.Status;

            connectionStatusText.text = controller.RelayStatus
                + "\n" + controller.LobbyReadinessSummary
                + "\nUnity Services: " + controller.VoiceStatus;

            lobbyStatusText.text = controller.LobbyReadinessSummary
                + "\n" + controller.LobbyRoadmap
                + "\n房间: " + controller.HumanPlayerCount + " 真人 / " + controller.BotCount + " AI";

            actionStatusText.text = controller.LocalObjectiveSummary
                + "\n" + controller.LocalActionHint
                + "\n技能冷却 " + Mathf.CeilToInt(controller.LocalAbilityCooldown) + "s | 击倒冷却 " + Mathf.CeilToInt(controller.LocalKillCooldown) + "s";

            resultStatusText.text = controller.ResultSummary;
            centerTitleText.text = BuildCenterTitle();
            centerBodyText.text = BuildCenterBody();
            notebookTitleText.text = NotebookTitle();
            notebookBodyText.text = NotebookBody();
            compactTopText.text = controller.RoomName
                + "\n" + controller.PhaseDisplayName
                + " | " + controller.MatchTimeText + "/20:00"
                + "\n证据 " + controller.EvidenceScore + "/" + controller.EvidenceTarget
                + " | 存活 " + controller.AlivePlayerCount + "/" + Mathf.Max(1, controller.Players.Count)
                + "\n" + controller.HazardSummary
                + "\n" + controller.VoiceHudLine;
            compactActionBarText.text = BuildCompactActionBar();
            compactPromptText.text = controller.LocalActionHint;
            compactAbilityText.text = "身份: " + controller.RoleDisplayName(controller.LocalRole)
                + "\n职责: " + controller.LocalProfessionDisplayName
                + "\n任务: " + controller.LocalObjectiveSummary
                + "\n技能 " + Mathf.CeilToInt(controller.LocalAbilityCooldown) + "s | 击倒 " + Mathf.CeilToInt(controller.LocalKillCooldown) + "s";
            footerText.text = controller.Status
                + " | WASD 移动 | E 互动 | Q 击倒 | R 报案/紧急 | F 技能 | M 大地图 | I 案情板";

            meetingTitleText.text = "九龙港城会议 | " + (controller.Phase == OnlineMatchPhase.Meeting ? "讨论" : "投票")
                + " " + Mathf.CeilToInt(controller.PhaseTimer) + "s";
            meetingBodyText.text = "会议原因: " + controller.LastMeetingReason
                + "\n证据墙: " + controller.MeetingEvidenceDigest
                + "\n票型: " + controller.VoteTallySummary
                + "\n语音: " + controller.VoiceHudLine
                + "\n上轮结论: " + controller.LastVoteOutcome;

            resultTitleText.text = "行动结算";
            resultBodyText.text = controller.ResultSummary + "\n\n" + controller.ResultRosterLine + "\n\n" + controller.MatchPressureSummary;

            taskTitleText.text = "现场任务 | " + controller.ActiveTaskNameText;
            taskBodyText.text = controller.ActiveTaskTemplateTitleText
                + " | " + controller.ActiveTaskTemplateSubtitleText
                + "\n" + controller.ActiveTaskInstructionText
                + "\n" + controller.ActiveTaskProgressText
                + "\n" + controller.ActiveTaskFooterText;
            RefreshTaskMiniGameBoard();
            taskFeedbackText.text = controller.ActiveTaskFeedbackTimerValue > 0f
                ? controller.ActiveTaskFeedbackPositiveValue ? "校验通过" : "输入不匹配"
                : "按顺序校验，再按住扫描推进现场结果";
            taskFeedbackText.color = controller.ActiveTaskFeedbackTimerValue > 0f
                ? controller.ActiveTaskFeedbackPositiveValue ? GreenAccent : RedAccent
                : MutedTextColor;

            mapTitleText.text = "九龙港区战术地图 | " + controller.MatchTimeText + " | 证据 " + controller.EvidenceScore + "/" + controller.EvidenceTarget;
        }

        private void RefreshProgressBars()
        {
            float evidenceRatio = controller.EvidenceScore / (float)Mathf.Max(1, controller.EvidenceTarget);
            float taskRatio = controller.CompletedTaskCount / (float)Mathf.Max(1, controller.TaskCount);
            float survivalRatio = controller.AlivePlayerCount / (float)Mathf.Max(1, controller.Players.Count);
            SetProgress(evidenceFill, evidenceRatio);
            SetProgress(taskFill, taskRatio);
            SetProgress(survivalFill, survivalRatio);
            SetProgress(resultEvidenceFill, evidenceRatio);
            SetProgress(resultTaskFill, taskRatio);
            SetProgress(resultSurvivalFill, survivalRatio);
            SetProgress(taskProgressFill, controller.ActiveTaskChargeValue);
        }

        private string BuildCompactActionBar()
        {
            bool actionPlayable = controller.Phase == OnlineMatchPhase.Action && controller.LocalAlive;
            string interact = actionPlayable ? "[E] 查证" : "[E] -";
            string kill = actionPlayable && controller.LocalRole == OnlineRole.Gang
                ? "[Q] 击倒 " + Mathf.CeilToInt(controller.LocalKillCooldown) + "s"
                : "[Q] -";
            string report = actionPlayable ? "[R] 报案" : "[R] -";
            string ability = actionPlayable ? "[F] 技能 " + Mathf.CeilToInt(controller.LocalAbilityCooldown) + "s" : "[F] -";
            return interact + "    " + kill + "    " + report + "    " + ability + "    [M] 地图    [I] 案情";
        }

        private string BuildCenterTitle()
        {
            if (!controller.IsOnline)
            {
                return "开始界面";
            }

            switch (controller.Phase)
            {
                case OnlineMatchPhase.Lobby:
                    return "房间准备";
                case OnlineMatchPhase.Opening:
                    return "身份简报";
                case OnlineMatchPhase.Action:
                    return "港区行动";
                case OnlineMatchPhase.Meeting:
                case OnlineMatchPhase.Voting:
                    return "会议与投票";
                case OnlineMatchPhase.Result:
                    return "结算";
                default:
                    return "对局";
            }
        }

        private string BuildCenterBody()
        {
            if (!controller.IsOnline)
            {
                return "选择本地完整局可立刻进入 10-20 分钟标准局；联网时可直连或 Relay 开房。\n\n"
                    + controller.ReleaseReadinessText;
            }

            switch (controller.Phase)
            {
                case OnlineMatchPhase.Lobby:
                    return controller.LobbyReadinessSummary
                        + "\n" + controller.LobbyRoadmap
                        + "\n房间规则: " + (controller.AutoFillAi ? "AI 补位" : "真人优先")
                        + " | " + (controller.RevealRoleOnEject ? "出局公开身份" : "身份隐藏")
                        + " | " + (controller.ProximityVoiceEnabled ? "近距离语音" : "会议语音");
                case OnlineMatchPhase.Opening:
                    return "你的身份: " + controller.RoleDisplayName(controller.LocalRole)
                        + " | 职责: " + controller.LocalProfessionDisplayName
                        + "\n" + controller.LocalObjectiveSummary
                        + "\n开局倒计时 " + Mathf.CeilToInt(controller.PhaseTimer) + "s。";
                case OnlineMatchPhase.Action:
                    return controller.MatchPressureSummary
                        + "\n\n你的任务: " + controller.LocalObjectiveSummary
                        + "\n行动提示: " + controller.LocalActionHint
                        + "\n最近证据: " + controller.LastEvidenceEvent
                        + "\n最近破坏: " + controller.LastSabotageEvent
                        + "\n危机: " + controller.HazardSummary;
                case OnlineMatchPhase.Meeting:
                case OnlineMatchPhase.Voting:
                    return "会议原因: " + controller.LastMeetingReason
                        + "\n证据墙: " + controller.MeetingEvidenceDigest
                        + "\n票型: " + controller.VoteTallySummary
                        + "\n语音: " + controller.VoiceHudLine;
                case OnlineMatchPhase.Result:
                    return controller.ResultSummary + "\n\n" + controller.ResultRosterLine;
                default:
                    return controller.Status;
            }
        }

        private string NotebookTitle()
        {
            switch (notebookTab)
            {
                case NotebookTab.Intel:
                    return "案情与任务";
                case NotebookTab.Log:
                    return "案情日志";
                case NotebookTab.Services:
                    return "发行与服务状态";
                default:
                    return "玩家名单";
            }
        }

        private string NotebookBody()
        {
            switch (notebookTab)
            {
                case NotebookTab.Intel:
                    return controller.FocusedIntelText + "\n\n" + controller.TaskListText;
                case NotebookTab.Log:
                    return controller.CaseLogText;
                case NotebookTab.Services:
                    return controller.ReleaseReadinessText + "\n\nVivox: " + controller.VoiceStatus;
                default:
                    return controller.PlayerListText;
            }
        }

        private bool CanEditRoomSettings()
        {
            return !controller.IsOnline || controller.IsHost && controller.Phase == OnlineMatchPhase.Lobby;
        }

        private void SelectNotebook(NotebookTab tab)
        {
            notebookTab = tab;
            Refresh(true);
        }

        private void RebuildVoteButtons()
        {
            if (voteButtonRoot == null)
            {
                return;
            }

            ClearGameObjects(voteButtons);
            bool canVote = controller.LocalAlive && (controller.Phase == OnlineMatchPhase.Meeting || controller.Phase == OnlineMatchPhase.Voting);

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in controller.Players)
            {
                OnlinePlayerState state = pair.Value;

                if (!state.Alive || pair.Key == controller.LocalClientIdValue)
                {
                    continue;
                }

                ulong captured = pair.Key;
                Button voteButton = CreateButton(state.DisplayName + (state.IsBot ? " [AI]" : string.Empty) + " | 嫌疑 " + state.Suspicion, voteButtonRoot, 40f, () => controller.RequestVote(captured));
                voteButton.interactable = canVote;
                voteButtons.Add(voteButton.gameObject);
            }

            Button skipButton = CreateButton("跳过投票", voteButtonRoot, 42f, () => controller.RequestSkipVote());
            skipButton.interactable = canVote;
            voteButtons.Add(skipButton.gameObject);
        }

        private void RefreshMeetingSeatBoard()
        {
            if (meetingSeatRoot == null)
            {
                return;
            }

            ClearChildren(meetingSeatRoot);
            CreateMeetingBoardBlock("Meeting Seat Round Table", new Vector2(0.28f, 0.18f), new Vector2(0.72f, 0.82f), new Color(0.10f, 0.11f, 0.10f, 0.96f), "会议圆桌");
            CreateMeetingBoardBlock("Meeting Seat Evidence Wall", new Vector2(0.03f, 0.12f), new Vector2(0.23f, 0.88f), new Color(0.055f, 0.066f, 0.07f, 0.98f), "证据墙");
            CreateMeetingBoardBlock("Meeting Seat Voice Channel", new Vector2(0.77f, 0.62f), new Vector2(0.97f, 0.88f), BlueAccent, "语音全员");
            CreateMeetingBoardBlock("Meeting Seat Vote Clock", new Vector2(0.77f, 0.12f), new Vector2(0.97f, 0.56f), AmberAccent, Mathf.CeilToInt(controller.PhaseTimer) + "s");

            int index = 0;
            int total = Mathf.Max(1, controller.Players.Count);

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in controller.Players)
            {
                OnlinePlayerState state = pair.Value;
                float angle = Mathf.PI * 2f * index / total - Mathf.PI * 0.5f;
                Vector2 center = new Vector2(0.5f + Mathf.Cos(angle) * 0.18f, 0.5f + Mathf.Sin(angle) * 0.28f);
                Vector2 size = new Vector2(0.078f, 0.128f);
                Color color = state.Alive
                    ? pair.Key == controller.LocalClientIdValue ? GreenAccent : state.IsBot ? new Color(0.52f, 0.58f, 0.58f, 0.98f) : BlueAccent
                    : new Color(0.19f, 0.2f, 0.2f, 0.92f);
                string label = state.Alive ? ShortName(state.DisplayName) : "出局";
                CreateMeetingBoardBlock("Meeting Seat Player " + pair.Key, center - size * 0.5f, center + size * 0.5f, color, label);
                index++;
            }
        }

        private void CreateMeetingBoardBlock(string name, Vector2 anchorMin, Vector2 anchorMax, Color color, string label)
        {
            GameObject block = CreatePanel(name, meetingSeatRoot, color);
            Stretch(block.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            Text text = CreateText("Label", block.transform, 11, TextAnchor.MiddleCenter, TextColor);
            text.text = label;
            Stretch(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(2f, 1f), new Vector2(-2f, -1f));
        }

        private static string ShortName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "?";
            }

            string trimmed = value.Trim();
            return trimmed.Length <= 3 ? trimmed : trimmed.Substring(0, 3);
        }

        private void RefreshMapMarkers()
        {
            if (Time.unscaledTime < nextMapRefreshTime)
            {
                return;
            }

            nextMapRefreshTime = Time.unscaledTime + 0.18f;
            ClearGameObjects(mapMarkers);

            foreach (OnlineTaskState task in controller.Tasks)
            {
                Color color = task.Completed ? GreenAccent : task.Sabotaged ? RedAccent : BlueAccent;
                AddMapMarker(task.Position, controller.TaskMapCodeDisplayName(task.Id), color, 12f);
            }

            foreach (OnlineBodyState body in controller.Bodies)
            {
                if (!body.Reported)
                {
                    AddMapMarker(body.Position, "尸", RedAccent, 15f);
                }
            }

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in controller.Players)
            {
                OnlinePlayerState state = pair.Value;
                Color color = pair.Key == controller.LocalClientIdValue
                    ? AmberAccent
                    : state.Alive ? new Color(0.92f, 0.9f, 0.48f, 1f) : new Color(0.42f, 0.44f, 0.44f, 1f);
                string label = pair.Key == controller.LocalClientIdValue ? "你" : ShortLabel(state.DisplayName, 3);
                AddMapMarker(state.Position, label, color, 14f);
            }
        }

        private void AddMapMarker(Vector3 worldPosition, string label, Color color, float size)
        {
            GameObject marker = CreatePanel("Map Marker " + label, mapMarkerRoot, color);
            RectTransform markerRect = marker.GetComponent<RectTransform>();
            Vector2 anchor = WorldToMapAnchor(worldPosition);
            markerRect.anchorMin = anchor;
            markerRect.anchorMax = anchor;
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(size, size);
            markerRect.anchoredPosition = Vector2.zero;

            Text markerLabel = CreateText("Label", marker.transform, 11, TextAnchor.MiddleLeft, TextColor);
            markerLabel.text = label;
            RectTransform labelRect = markerLabel.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(1f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.sizeDelta = new Vector2(84f, 18f);
            labelRect.anchoredPosition = new Vector2(4f, 0f);
            mapMarkers.Add(marker);
        }

        private void BuildStaticMapLayer()
        {
            CreateMapRoute("Main Spine", new Vector3(0f, -0.18f * DesignScaleY, 0f), new Vector3(15.5f * DesignScaleX, 1.2f * DesignScaleY, 0f), new Color(0.22f, 0.26f, 0.27f, 0.95f));
            CreateMapRoute("North Spine", new Vector3(0f, 3.65f * DesignScaleY, 0f), new Vector3(16.4f * DesignScaleX, 1.04f * DesignScaleY, 0f), new Color(0.18f, 0.22f, 0.24f, 0.92f));
            CreateMapRoute("South Spine", new Vector3(0.12f * DesignScaleX, -3.9f * DesignScaleY, 0f), new Vector3(15.4f * DesignScaleX, 1.04f * DesignScaleY, 0f), new Color(0.18f, 0.22f, 0.24f, 0.92f));
            CreateMapRoute("West Spine", new Vector3(-6.85f * DesignScaleX, 0.15f * DesignScaleY, 0f), new Vector3(1.08f * DesignScaleX, 8.35f * DesignScaleY, 0f), new Color(0.18f, 0.22f, 0.24f, 0.92f));
            CreateMapRoute("East Spine", new Vector3(7.05f * DesignScaleX, 0.08f * DesignScaleY, 0f), new Vector3(1.08f * DesignScaleX, 8.18f * DesignScaleY, 0f), new Color(0.18f, 0.22f, 0.24f, 0.92f));
            CreateMapRoute("Center North", new Vector3(0f, 1.85f * DesignScaleY, 0f), new Vector3(1.08f * DesignScaleX, 3.15f * DesignScaleY, 0f), new Color(0.2f, 0.24f, 0.25f, 0.92f));
            CreateMapRoute("Center South", new Vector3(0f, -2.35f * DesignScaleY, 0f), new Vector3(1.08f * DesignScaleX, 3.05f * DesignScaleY, 0f), new Color(0.2f, 0.24f, 0.25f, 0.92f));
            CreateVerticalSliceStaticMapLayer();
        }

        private void CreateMapRoute(string name, Vector3 worldCenter, Vector3 worldSize, Color color)
        {
            GameObject route = CreatePanel(name, mapStaticRoot, color);
            RectTransform routeRect = route.GetComponent<RectTransform>();
            Rect anchors = WorldRectToAnchors(worldCenter, worldSize);
            routeRect.anchorMin = new Vector2(anchors.xMin, anchors.yMin);
            routeRect.anchorMax = new Vector2(anchors.xMax, anchors.yMax);
            routeRect.offsetMin = Vector2.zero;
            routeRect.offsetMax = Vector2.zero;
        }

        private void CreateVerticalSliceStaticMapLayer()
        {
            Color roomColor = new Color(0.22f, 0.13f, 0.08f, 0.78f);
            Color routeColor = new Color(0.5f, 0.32f, 0.12f, 0.88f);

            CreateMapRoute("Vertical Slice Core", ScaleDesignPoint(new Vector3(-1.25f, -0.56f, 0f)), ScaleDesignSize(new Vector3(5.7f, 2.78f, 0f)), new Color(0.3f, 0.22f, 0.12f, 0.5f));
            CreateMapRoute("Vertical Slice CCTV Corridor", ScaleDesignPoint(new Vector3(-6.6f, 0.68f, 0f)), ScaleDesignSize(new Vector3(3.8f, 1.18f, 0f)), routeColor);
            CreateMapRoute("Vertical Slice Market Bend", ScaleDesignPoint(new Vector3(-0.4f, 2.56f, 0f)), ScaleDesignSize(new Vector3(4.65f, 1.24f, 0f)), routeColor);
            CreateMapRoute("Vertical Slice Alley Approach", ScaleDesignPoint(new Vector3(3.88f, -1.25f, 0f)), ScaleDesignSize(new Vector3(3.9f, 1.12f, 0f)), routeColor);

            CreateMapRoute("Vertical Slice Room CCTV", ScaleDesignPoint(new Vector3(-8.88f, 1.72f, 0f)), ScaleDesignSize(new Vector3(2.35f, 1.48f, 0f)), roomColor);
            CreateMapRoute("Vertical Slice Room Cafe", ScaleDesignPoint(new Vector3(-4.64f, 1.58f, 0f)), ScaleDesignSize(new Vector3(2.52f, 1.46f, 0f)), roomColor);
            CreateMapRoute("Vertical Slice Room Market", ScaleDesignPoint(new Vector3(-0.72f, 2.88f, 0f)), ScaleDesignSize(new Vector3(3.64f, 1.3f, 0f)), roomColor);
            CreateMapRoute("Vertical Slice Room Alley", ScaleDesignPoint(new Vector3(4.92f, -1.48f, 0f)), ScaleDesignSize(new Vector3(2.72f, 1.34f, 0f)), roomColor);
            CreateMapRoute("Vertical Slice Room Power", ScaleDesignPoint(new Vector3(7.88f, 4.82f, 0f)), ScaleDesignSize(new Vector3(2.28f, 1.52f, 0f)), roomColor);

            CreateMapText("Vertical Slice Label Core", ScaleDesignPoint(new Vector3(-1.18f, -0.72f, 0f)), "集合", VerticalSliceAccent);
            CreateMapText("Vertical Slice Label CCTV", ScaleDesignPoint(new Vector3(-8.88f, 1.72f, 0f)), "监控", BlueAccent);
            CreateMapText("Vertical Slice Label Cafe", ScaleDesignPoint(new Vector3(-4.64f, 1.58f, 0f)), "茶餐厅", AmberAccent);
            CreateMapText("Vertical Slice Label Market", ScaleDesignPoint(new Vector3(-0.72f, 2.88f, 0f)), "夜市", VerticalSliceAccent);
            CreateMapText("Vertical Slice Label Alley", ScaleDesignPoint(new Vector3(4.92f, -1.48f, 0f)), "后巷", RedAccent);
            CreateMapText("Vertical Slice Label Power", ScaleDesignPoint(new Vector3(7.88f, 4.82f, 0f)), "电房", GreenAccent);
        }

        private void CreateMapText(string name, Vector3 worldPosition, string text, Color color)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(mapStaticRoot, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            Vector2 anchor = WorldToMapAnchor(worldPosition);
            labelRect.anchorMin = anchor;
            labelRect.anchorMax = anchor;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(82f, 20f);
            labelRect.anchoredPosition = Vector2.zero;

            Text label = CreateText("Label", labelObject.transform, 11, TextAnchor.MiddleCenter, color);
            label.text = text;
            Stretch(label.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static Vector3 ScaleDesignPoint(Vector3 designPoint)
        {
            return new Vector3(designPoint.x * DesignScaleX, designPoint.y * DesignScaleY, designPoint.z);
        }

        private static Vector3 ScaleDesignSize(Vector3 designSize)
        {
            return new Vector3(designSize.x * DesignScaleX, designSize.y * DesignScaleY, designSize.z);
        }

        private Vector2 WorldToMapAnchor(Vector3 worldPosition)
        {
            float x = Mathf.InverseLerp(-controller.MapHalfWidthValue, controller.MapHalfWidthValue, worldPosition.x);
            float y = Mathf.InverseLerp(-controller.MapHalfHeightValue, controller.MapHalfHeightValue, worldPosition.y);
            return new Vector2(x, y);
        }

        private Rect WorldRectToAnchors(Vector3 worldCenter, Vector3 worldSize)
        {
            Vector2 min = WorldToMapAnchor(worldCenter - worldSize * 0.5f);
            Vector2 max = WorldToMapAnchor(worldCenter + worldSize * 0.5f);
            return Rect.MinMaxRect(Mathf.Clamp01(min.x), Mathf.Clamp01(min.y), Mathf.Clamp01(max.x), Mathf.Clamp01(max.y));
        }

        private void RefreshTaskMiniGameBoard()
        {
            if (taskMiniGameRoot == null)
            {
                return;
            }

            string key = controller.ActiveTaskIdValue
                + ":"
                + controller.ActiveTaskStepValue
                + ":"
                + controller.ActiveTaskStepOneDone
                + ":"
                + controller.ActiveTaskStepTwoDone
                + ":"
                + controller.ActiveTaskStepThreeDone;

            if (key == taskMiniGameKey)
            {
                return;
            }

            taskMiniGameKey = key;
            ClearChildren(taskMiniGameRoot);

            int taskId = controller.ActiveTaskIdValue;
            int mode = TaskCanvasTemplateMode(taskId);

            if (mode == 0)
            {
                BuildCctvTaskCanvas();
            }
            else if (mode == 1)
            {
                BuildRecordingTaskCanvas();
            }
            else if (mode == 2)
            {
                BuildBreakerTaskCanvas();
            }
            else if (mode == 3)
            {
                BuildPlateTaskCanvas();
            }
            else
            {
                BuildGenericTaskCanvas();
            }

            BuildTaskMiniGameStatusRail();
        }

        private void BuildTaskMiniGameStatusRail()
        {
            CreateTaskMiniGameBlock("Task MiniGame Step Rail", new Vector2(0.948f, 0.18f), new Vector2(0.984f, 0.72f), new Color(0.018f, 0.024f, 0.026f, 0.92f), string.Empty);

            for (int i = 0; i < 3; i++)
            {
                bool completed = i == 0 && controller.ActiveTaskStepOneDone
                    || i == 1 && controller.ActiveTaskStepTwoDone
                    || i == 2 && controller.ActiveTaskStepThreeDone;
                bool current = controller.ActiveTaskStepValue == i;
                Color color = completed ? GreenAccent : current ? AmberAccent : new Color(0.16f, 0.2f, 0.21f, 0.96f);
                float y = 0.22f + (2 - i) * 0.16f;
                CreateTaskMiniGameBlock("Task MiniGame Step Chip " + i, new Vector2(0.938f, y), new Vector2(0.992f, y + 0.08f), color, (i + 1).ToString());
            }

            float charge = Mathf.Clamp01(controller.ActiveTaskChargeValue);
            CreateTaskMiniGameBlock("Task MiniGame Charge Track", new Vector2(0.06f, 0.015f), new Vector2(0.58f, 0.05f), new Color(0.018f, 0.024f, 0.026f, 0.94f), "扫描");

            if (charge > 0.01f)
            {
                CreateTaskMiniGameBlock("Task MiniGame Charge Fill", new Vector2(0.06f, 0.015f), new Vector2(0.06f + 0.52f * charge, 0.05f), BlueAccent, string.Empty);
            }
        }

        private void BuildCctvTaskCanvas()
        {
            CreateTaskMiniGameText("Task MiniGame CCTV Header", "监控片段比对", new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.96f), BlueAccent);

            for (int i = 0; i < 9; i++)
            {
                int row = i / 3;
                int col = i % 3;
                bool selected = i == controller.ActiveTaskCorrectStepOne - 1 || i == controller.ActiveTaskCorrectStepTwo + 2 || i == controller.ActiveTaskCorrectStepThree + 5;
                Color color = selected ? new Color(0.12f, 0.7f, 0.94f, 0.95f) : new Color(0.05f, 0.12f, 0.15f, 0.95f);
                CreateTaskMiniGameBlock("Task MiniGame CCTV screen " + i, new Vector2(0.06f + col * 0.3f, 0.18f + (2 - row) * 0.18f), new Vector2(0.28f + col * 0.3f, 0.32f + (2 - row) * 0.18f), color, "C" + (i + 1));
            }

            CreateTaskMiniGameBlock("Task MiniGame CCTV timeline", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.11f), new Color(0.9f, 0.68f, 0.12f, 0.92f), "可疑时间轴");
        }

        private void BuildRecordingTaskCanvas()
        {
            CreateTaskMiniGameText("Task MiniGame Recording Header", "线人录音过滤", new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.96f), AmberAccent);

            for (int i = 0; i < 14; i++)
            {
                float x0 = 0.06f + i * 0.064f;
                float height = 0.12f + Mathf.Abs(Mathf.Sin(i * 1.31f)) * 0.42f;
                bool hot = i == controller.ActiveTaskCorrectStepOne + 1 || i == controller.ActiveTaskCorrectStepTwo + 5 || i == controller.ActiveTaskCorrectStepThree + 8;
                CreateTaskMiniGameBlock("Task MiniGame Recording waveform " + i, new Vector2(x0, 0.16f), new Vector2(x0 + 0.04f, 0.16f + height), hot ? GreenAccent : new Color(0.12f, 0.46f, 0.42f, 0.95f), string.Empty);
            }

            CreateTaskMiniGameBlock("Task MiniGame Recording keyword A", new Vector2(0.08f, 0.61f), new Vector2(0.32f, 0.72f), new Color(0.18f, 0.12f, 0.08f, 0.96f), "码头");
            CreateTaskMiniGameBlock("Task MiniGame Recording keyword B", new Vector2(0.38f, 0.61f), new Vector2(0.62f, 0.72f), new Color(0.18f, 0.12f, 0.08f, 0.96f), "车牌");
            CreateTaskMiniGameBlock("Task MiniGame Recording keyword C", new Vector2(0.68f, 0.61f), new Vector2(0.92f, 0.72f), new Color(0.18f, 0.12f, 0.08f, 0.96f), "暗号");
        }

        private void BuildBreakerTaskCanvas()
        {
            CreateTaskMiniGameText("Task MiniGame Breaker Header", "电闸接线", new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.96f), GreenAccent);

            Color[] wireColors =
            {
                new Color(0.92f, 0.12f, 0.08f, 0.96f),
                new Color(0.08f, 0.54f, 0.92f, 0.96f),
                new Color(0.92f, 0.76f, 0.12f, 0.96f),
                new Color(0.16f, 0.74f, 0.32f, 0.96f)
            };

            for (int i = 0; i < 4; i++)
            {
                float y = 0.2f + i * 0.13f;
                CreateTaskMiniGameBlock("Task MiniGame Breaker left socket " + i, new Vector2(0.08f, y), new Vector2(0.18f, y + 0.08f), wireColors[i], (i + 1).ToString());
                CreateTaskMiniGameBlock("Task MiniGame Breaker wire " + i, new Vector2(0.2f, y + 0.03f), new Vector2(0.78f, y + 0.05f), wireColors[(i + controller.ActiveTaskStepValue) % wireColors.Length], string.Empty);
                CreateTaskMiniGameBlock("Task MiniGame Breaker right socket " + i, new Vector2(0.8f, y), new Vector2(0.9f, y + 0.08f), new Color(0.05f, 0.07f, 0.07f, 0.96f), (4 - i).ToString());
            }

            CreateTaskMiniGameBlock("Task MiniGame Breaker warning strip", new Vector2(0.08f, 0.08f), new Vector2(0.9f, 0.14f), new Color(0.88f, 0.18f, 0.08f, 0.9f), "断电风险");
        }

        private void BuildPlateTaskCanvas()
        {
            CreateTaskMiniGameText("Task MiniGame Plate Header", "车牌路线比对", new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.96f), BlueAccent);
            CreateTaskMiniGameBlock("Task MiniGame Plate road", new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.38f), new Color(0.08f, 0.09f, 0.09f, 0.96f), string.Empty);
            CreateTaskMiniGameBlock("Task MiniGame Plate suspect car", new Vector2(0.16f, 0.42f), new Vector2(0.42f, 0.62f), new Color(0.76f, 0.62f, 0.12f, 0.96f), "HK 7X2");
            CreateTaskMiniGameBlock("Task MiniGame Plate patrol car", new Vector2(0.58f, 0.42f), new Vector2(0.84f, 0.62f), new Color(0.08f, 0.26f, 0.68f, 0.96f), "巡逻");

            for (int i = 0; i < 6; i++)
            {
                bool hot = i == controller.ActiveTaskStepValue + 2;
                CreateTaskMiniGameBlock("Task MiniGame Plate digit " + i, new Vector2(0.14f + i * 0.12f, 0.08f), new Vector2(0.22f + i * 0.12f, 0.16f), hot ? RedAccent : new Color(0.9f, 0.88f, 0.76f, 0.96f), i.ToString());
            }
        }

        private void BuildGenericTaskCanvas()
        {
            CreateTaskMiniGameText("Task MiniGame Generic Header", controller.ActiveTaskTemplateTitleText, new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.96f), AmberAccent);

            for (int i = 0; i < 3; i++)
            {
                bool completed = i == 0 && controller.ActiveTaskStepOneDone
                    || i == 1 && controller.ActiveTaskStepTwoDone
                    || i == 2 && controller.ActiveTaskStepThreeDone;
                CreateTaskMiniGameBlock("Task MiniGame Generic step " + i, new Vector2(0.14f + i * 0.27f, 0.22f), new Vector2(0.34f + i * 0.27f, 0.58f), completed ? GreenAccent : new Color(0.12f, 0.16f, 0.17f, 0.96f), "步骤 " + (i + 1));
            }
        }

        private static int TaskCanvasTemplateMode(int taskId)
        {
            if (taskId == 0)
            {
                return 0;
            }

            if (taskId == 5 || taskId == 21)
            {
                return 1;
            }

            if (taskId == 2 || taskId == 14 || taskId == 24)
            {
                return 2;
            }

            if (taskId == 18)
            {
                return 3;
            }

            return 4;
        }

        private void CreateTaskMiniGameBlock(string name, Vector2 anchorMin, Vector2 anchorMax, Color color, string label)
        {
            GameObject block = CreatePanel(name, taskMiniGameRoot, color);
            RectTransform rect = block.GetComponent<RectTransform>();
            Stretch(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            Text text = CreateText("Label", block.transform, 12, TextAnchor.MiddleCenter, TextColor);
            text.text = label;
            Stretch(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void CreateTaskMiniGameText(string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            Text text = CreateText(name, taskMiniGameRoot, 14, TextAnchor.MiddleLeft, color);
            text.text = label;
            Stretch(text.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        }

        private void ConfigureHoldButton(Button chargeButton)
        {
            EventTrigger trigger = chargeButton.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, () => taskChargeHeld = true);
            AddTrigger(trigger, EventTriggerType.PointerUp, () => taskChargeHeld = false);
            AddTrigger(trigger, EventTriggerType.PointerExit, () => taskChargeHeld = false);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void SyncCanvasCamera()
        {
            if (canvas != null && Camera.main != null && canvas.worldCamera != Camera.main)
            {
                canvas.worldCamera = Camera.main;
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            T component = GetComponent<T>();

            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        private Transform CreateSection(string title, Transform parent, float preferredHeight)
        {
            Transform section = CreateVerticalPanel(
                title + " Section",
                parent,
                PanelColor,
                Vector2.zero,
                Vector2.one,
                new RectOffset(12, 12, 10, 10),
                8f,
                false);
            AddLayout(section.gameObject, preferredHeight, 0f, 0f);

            Text titleText = CreateText(title + " Title", section, 17, TextAnchor.UpperLeft, AmberAccent);
            titleText.text = title;
            AddLayout(titleText.gameObject, 24f, 0f, 0f);
            return section;
        }

        private Transform CreateVerticalPanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, RectOffset padding, float spacing, bool stretch = true)
        {
            GameObject panel = CreatePanel(name, parent, color);
            RectTransform rect = panel.GetComponent<RectTransform>();

            if (stretch)
            {
                Stretch(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            }

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return panel.transform;
        }

        private Transform CreateHorizontalPanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, RectOffset padding, float spacing)
        {
            GameObject panel = CreatePanel(name, parent, color);
            Stretch(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

            HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            return panel.transform;
        }

        private Transform CreateButtonRow(Transform parent, float height)
        {
            GameObject row = new GameObject("Button Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            AddLayout(row, height, 0f, 0f);
            return row.transform;
        }

        private Button CreateButton(string label, Transform parent, float height, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.17f, 0.19f, 0.18f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.17f, 0.19f, 0.18f, 1f);
            colors.highlightedColor = new Color(0.24f, 0.29f, 0.28f, 1f);
            colors.pressedColor = new Color(0.09f, 0.12f, 0.13f, 1f);
            colors.disabledColor = new Color(0.09f, 0.1f, 0.1f, 0.58f);
            button.colors = colors;
            button.onClick.AddListener(onClick);

            Text text = CreateText("Label", buttonObject.transform, 14, TextAnchor.MiddleCenter, TextColor);
            text.text = label;
            Stretch(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));

            AddLayout(buttonObject, height, 0f, 1f);
            return button;
        }

        private InputField CreateInputRow(Transform parent, string label, string placeholder, UnityEngine.Events.UnityAction<string> onEndEdit)
        {
            GameObject row = new GameObject(label + " Input Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            AddLayout(row, 34f, 0f, 0f);

            Text labelText = CreateText(label + " Label", row.transform, 13, TextAnchor.MiddleLeft, MutedTextColor);
            labelText.text = label;
            AddLayout(labelText.gameObject, 0f, 58f, 0f);

            GameObject fieldObject = new GameObject(label + " Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            fieldObject.transform.SetParent(row.transform, false);
            fieldObject.GetComponent<Image>().color = new Color(0.02f, 0.026f, 0.028f, 0.96f);
            AddLayout(fieldObject, 0f, 0f, 1f);

            InputField input = fieldObject.GetComponent<InputField>();
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 24;

            Text text = CreateText("Text", fieldObject.transform, 13, TextAnchor.MiddleLeft, TextColor);
            Stretch(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));

            Text placeholderText = CreateText("Placeholder", fieldObject.transform, 13, TextAnchor.MiddleLeft, new Color(MutedTextColor.r, MutedTextColor.g, MutedTextColor.b, 0.55f));
            placeholderText.text = placeholder;
            Stretch(placeholderText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));

            input.textComponent = text;
            input.placeholder = placeholderText;
            input.onEndEdit.AddListener(onEndEdit);
            return input;
        }

        private Slider CreateSliderRow(Transform parent, string label, float min, float max, UnityEngine.Events.UnityAction<float> onChanged, out Text valueText)
        {
            GameObject row = new GameObject(label + " Slider Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            AddLayout(row, 34f, 0f, 0f);

            Text labelText = CreateText(label + " Label", row.transform, 13, TextAnchor.MiddleLeft, MutedTextColor);
            labelText.text = label;
            AddLayout(labelText.gameObject, 0f, 68f, 0f);

            GameObject sliderObject = new GameObject(label + " Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(row.transform, false);
            AddLayout(sliderObject, 0f, 0f, 1f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;

            GameObject background = CreatePanel("Background", sliderObject.transform, new Color(0.02f, 0.026f, 0.028f, 0.96f));
            Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(4f, 6f), new Vector2(-4f, -6f));

            GameObject fill = CreatePanel("Fill", fillArea.transform, BlueAccent);
            Stretch(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            slider.fillRect = fill.GetComponent<RectTransform>();

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));

            GameObject handle = CreatePanel("Handle", handleArea.transform, AmberAccent);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(14f, 24f);
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.onValueChanged.AddListener(onChanged);

            valueText = CreateText(label + " Value", row.transform, 13, TextAnchor.MiddleRight, TextColor);
            AddLayout(valueText.gameObject, 0f, 54f, 0f);
            return slider;
        }

        private Toggle CreateToggle(string label, Transform parent, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            GameObject toggleObject = new GameObject(label + " Toggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = toggleObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            AddLayout(toggleObject, 28f, 0f, 0f);

            GameObject box = CreatePanel("Box", toggleObject.transform, new Color(0.02f, 0.026f, 0.028f, 0.96f));
            AddLayout(box, 0f, 22f, 0f);
            GameObject check = CreatePanel("Checkmark", box.transform, GreenAccent);
            Stretch(check.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            Text text = CreateText("Label", toggleObject.transform, 13, TextAnchor.MiddleLeft, TextColor);
            text.text = label;
            AddLayout(text.gameObject, 0f, 0f, 1f);

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        private RectTransform CreateProgressBar(Transform parent, string label, Color color)
        {
            GameObject bar = CreatePanel(label + " Bar", parent, new Color(0.02f, 0.026f, 0.028f, 0.96f));
            AddLayout(bar, 30f, 0f, 0f);

            GameObject fill = CreatePanel("Fill", bar.transform, color);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            Text labelText = CreateText("Label", bar.transform, 13, TextAnchor.MiddleLeft, TextColor);
            labelText.text = label;
            Stretch(labelText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            return fillRect;
        }

        private Text CreateScrollText(string name, Transform parent, float preferredHeight)
        {
            GameObject scrollRoot = CreatePanel(name + " Scroll", parent, PanelDeepColor);
            AddLayout(scrollRoot, preferredHeight, 0f, 1f);
            ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24f;

            GameObject viewport = CreatePanel(name + " Viewport", scrollRoot.transform, new Color(0f, 0f, 0f, 0f));
            viewport.AddComponent<RectMask2D>();
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            GameObject content = new GameObject(name + " Content", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text text = content.GetComponent<Text>();
            ConfigureText(text, 14, TextAnchor.UpperLeft, TextColor);
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            return text;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            return panel;
        }

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            ConfigureText(text, fontSize, alignment, color);
            return text;
        }

        private static void ConfigureText(Text text, int fontSize, TextAnchor alignment, Color color)
        {
            text.font = LoadBuiltinFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
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

        private static void AddLayout(GameObject gameObject, float preferredHeight, float preferredWidth, float flexibleWidth)
        {
            AddLayout(gameObject, preferredHeight, preferredWidth, flexibleWidth, 0f);
        }

        private static void AddLayout(GameObject gameObject, float preferredHeight, float preferredWidth, float flexibleWidth, float flexibleHeight)
        {
            LayoutElement layout = gameObject.GetComponent<LayoutElement>();

            if (layout == null)
            {
                layout = gameObject.AddComponent<LayoutElement>();
            }

            if (preferredHeight > 0f)
            {
                layout.preferredHeight = preferredHeight;
            }

            if (preferredWidth > 0f)
            {
                layout.preferredWidth = preferredWidth;
            }

            if (flexibleWidth > 0f)
            {
                layout.flexibleWidth = flexibleWidth;
            }

            if (flexibleHeight > 0f)
            {
                layout.flexibleHeight = flexibleHeight;
            }
        }

        private static int CountNamedChildren(Transform root, string nameFragment)
        {
            if (root == null)
            {
                return 0;
            }

            int count = root.name.Contains(nameFragment) ? 1 : 0;

            foreach (Transform child in root)
            {
                count += CountNamedChildren(child, nameFragment);
            }

            return count;
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void SetProgress(RectTransform fill, float ratio)
        {
            if (fill == null)
            {
                return;
            }

            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            fill.offsetMax = Vector2.zero;
        }

        private static void SetButtonText(Button button, string label)
        {
            Text text = button == null ? null : button.GetComponentInChildren<Text>();

            if (text != null)
            {
                text.text = label;
            }
        }

        private static void SyncInput(InputField input, string value)
        {
            if (input == null || input.isFocused || input.text == value)
            {
                return;
            }

            input.SetTextWithoutNotify(value);
        }

        private static string ShortLabel(string value, int length)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= length ? value : value.Substring(0, length);
        }

        private static void ClearGameObjects(List<GameObject> objects)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(objects[i]);
                }
                else
                {
                    DestroyImmediate(objects[i]);
                }
            }

            objects.Clear();
        }
    }
}
