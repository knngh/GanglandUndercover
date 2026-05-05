using System.Collections.Generic;
using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace GanglandUndercover.UI
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        private readonly List<Button> districtButtons = new List<Button>();
        private readonly List<Button> actionButtons = new List<Button>();

        private GameController controller;
        private Text headerText;
        private Text statsText;
        private Text logText;
        private Text roleIntroText;
        private Text languageButtonText;
        private Transform districtList;
        private Transform actionList;
        private GameObject rolePanel;
        private GameObject gamePanel;

        public void Bind(GameController gameController)
        {
            controller = gameController;
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

            GameObject root = CreatePanel("Root", transform, new Color(0.08f, 0.08f, 0.07f, 0f));
            Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            headerText = CreateText("Header", root.transform, 28, TextAnchor.MiddleCenter);
            Stretch(headerText.GetComponent<RectTransform>(), new Vector2(0f, 0.88f), Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, -8f));

            rolePanel = CreatePanel("RolePanel", root.transform, new Color(0.13f, 0.12f, 0.1f, 1f));
            Stretch(rolePanel.GetComponent<RectTransform>(), new Vector2(0.25f, 0.24f), new Vector2(0.75f, 0.82f), Vector2.zero, Vector2.zero);

            VerticalLayoutGroup roleLayout = rolePanel.AddComponent<VerticalLayoutGroup>();
            roleLayout.padding = new RectOffset(24, 24, 24, 24);
            roleLayout.spacing = 16;

            roleIntroText = CreateText("RoleIntro", rolePanel.transform, 20, TextAnchor.MiddleCenter);
            roleIntroText.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            CreateRoleButton(Faction.Gang);
            CreateRoleButton(Faction.Police);
            CreateRoleButton(Faction.Undercover);

            gamePanel = CreatePanel("GamePanel", root.transform, new Color(0.11f, 0.11f, 0.1f, 1f));
            Stretch(gamePanel.GetComponent<RectTransform>(), new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero);
            gamePanel.GetComponent<Image>().color = new Color(0.11f, 0.11f, 0.1f, 0f);

            districtList = CreateColumn("Districts", gamePanel.transform, new Vector2(0f, 0.1f), new Vector2(0.28f, 1f));
            actionList = CreateColumn("Actions", gamePanel.transform, new Vector2(0.72f, 0.42f), new Vector2(1f, 1f));

            statsText = CreateText("Stats", gamePanel.transform, 16, TextAnchor.UpperLeft);
            Stretch(statsText.GetComponent<RectTransform>(), new Vector2(0.72f, 0.12f), new Vector2(1f, 0.4f), new Vector2(8f, 8f), new Vector2(-8f, -8f));

            logText = CreateText("Log", gamePanel.transform, 15, TextAnchor.UpperLeft);
            Stretch(logText.GetComponent<RectTransform>(), new Vector2(0.3f, 0f), new Vector2(1f, 0.11f), new Vector2(8f, 8f), new Vector2(-8f, -8f));

            Button languageButton = CreateButton("Language", gamePanel.transform, 46f);
            languageButton.onClick.AddListener(() => controller.ToggleLanguage());
            languageButtonText = languageButton.GetComponentInChildren<Text>();
            Stretch(languageButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.28f, 0.085f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
        }

        private Transform CreateColumn(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject column = CreatePanel(name, parent, new Color(0.16f, 0.15f, 0.13f, 1f));
            Stretch(column.GetComponent<RectTransform>(), anchorMin, anchorMax, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8;
            return column.transform;
        }

        private void CreateRoleButton(Faction faction)
        {
            Button button = CreateButton(LocalizeFaction(faction), rolePanel.transform, 72f);
            button.onClick.AddListener(() => controller.SelectFaction(faction));
        }

        private void Rebuild()
        {
            if (controller == null)
            {
                return;
            }

            GameState state = controller.State;
            rolePanel.SetActive(state.Phase == GamePhase.RoleSelect);
            gamePanel.SetActive(state.Phase != GamePhase.RoleSelect);
            headerText.text = state.Phase == GamePhase.GameOver ? state.Result : T("game.title") + " - " + T("phase." + state.Phase);

            if (roleIntroText != null)
            {
                roleIntroText.text = T("role.choose");
            }

            if (languageButtonText != null)
            {
                languageButtonText.text = T("button.language");
            }

            ClearDynamicLists();

            if (state.Phase == GamePhase.RoleSelect)
            {
                return;
            }

            foreach (DistrictState district in state.Districts)
            {
                Button districtButton = CreateButton(district.DisplayName, districtList, 86f);
                districtButton.GetComponentInChildren<Text>().text = FormatDistrict(district);

                DistrictType type = district.Type;
                districtButton.onClick.AddListener(() =>
                {
                    controller.SelectDistrict(type);
                });

                districtButtons.Add(districtButton);
            }

            if (state.Phase == GamePhase.GameOver)
            {
                Button reset = CreateButton(T("button.restart"), actionList, 72f);
                reset.onClick.AddListener(controller.Reset);
                actionButtons.Add(reset);
            }
            else
            {
                foreach (PlayerAction action in controller.Actions.GetActionsFor(state.PlayerFaction))
                {
                    Button actionButton = CreateButton(LocalizeActionLabel(action), actionList, 78f);
                    actionButton.GetComponentInChildren<Text>().text = LocalizeActionLabel(action) + "\n" + LocalizeActionDescription(action);

                    PlayerAction captured = action;
                    actionButton.onClick.AddListener(() => controller.RunPlayerAction(controller.SelectedDistrict, captured));
                    actionButtons.Add(actionButton);
                }
            }

            statsText.text = FormatStats(state);
            logText.text = string.Join("\n", state.Log);
        }

        private string FormatDistrict(DistrictState district)
        {
            string selected = district.Type == controller.SelectedDistrict ? "> " : string.Empty;
            string witness = district.HasWitness ? " " + T("label.witness") : string.Empty;
            string lockdown = district.IsLockedDown ? " " + T("label.lockdown") : string.Empty;

            return selected + LocalizeDistrict(district.Type)
                + "\n" + T("short.gang") + " " + district.GangInfluence + " | " + T("short.police") + " " + district.PolicePresence + " | " + T("label.publicTrust") + " " + district.CivilianTrust
                + "\n" + T("label.control") + ": " + LocalizeFaction(district.Controller) + witness + lockdown;
        }

        private string FormatStats(GameState state)
        {
            return T("label.role") + ": " + LocalizeFaction(state.PlayerFaction)
                + "\n" + T("label.day") + ": " + state.Day
                + "\n" + T("label.evidence") + ": " + state.Evidence + "/8"
                + "\n" + T("label.policeHeat") + ": " + state.PoliceHeat + "/6"
                + "\n" + T("label.shipment") + ": " + state.ShipmentProgress + "/3"
                + "\n" + T("label.cover") + ": " + state.Cover
                + "\n" + T("label.suspicion") + ": " + state.Suspicion
                + "\n" + T("label.publicTrust") + ": " + state.PublicTrust
                + "\n" + T("label.gangDistricts") + ": " + state.GangControlledDistricts
                + "\n" + T("label.policeDistricts") + ": " + state.PoliceControlledDistricts
                + "\n" + T("label.contested") + ": " + state.ContestedDistricts;
        }

        private string LocalizeFaction(Faction faction)
        {
            return T("role." + faction);
        }

        private string LocalizeDistrict(DistrictType districtType)
        {
            return T("district." + districtType);
        }

        private string LocalizeActionLabel(PlayerAction action)
        {
            return T("action." + action.Id + ".label");
        }

        private string LocalizeActionDescription(PlayerAction action)
        {
            return T("action." + action.Id + ".desc");
        }

        private string T(string key)
        {
            return Localization.Text(controller.State.Language, key);
        }

        private void ClearDynamicLists()
        {
            foreach (Button button in districtButtons)
            {
                Destroy(button.gameObject);
            }

            foreach (Button button in actionButtons)
            {
                Destroy(button.gameObject);
            }

            districtButtons.Clear();
            actionButtons.Clear();
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

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = LoadBuiltinFont();
            text.fontSize = fontSize;
            text.color = new Color(0.92f, 0.88f, 0.78f, 1f);
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

        private static Button CreateButton(string label, Transform parent, float height)
        {
            GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.28f, 0.22f, 0.14f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.28f, 0.22f, 0.14f, 1f);
            colors.highlightedColor = new Color(0.43f, 0.32f, 0.18f, 1f);
            colors.pressedColor = new Color(0.18f, 0.14f, 0.1f, 1f);
            button.colors = colors;

            Text text = CreateText("Label", buttonObject.transform, 16, TextAnchor.MiddleCenter);
            text.text = label;
            Stretch(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            return button;
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
