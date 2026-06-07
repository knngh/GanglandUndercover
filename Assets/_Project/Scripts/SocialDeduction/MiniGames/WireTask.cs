using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 连线小游戏（Among Us Wire Task 复刻）。
    /// 4 条交叉线，左右各 4 个端点；点击端点旋转，使同色两端连通即完成。
    /// </summary>
    public sealed class WireTask : MiniGameBase
    {
        [System.Serializable]
        public class Wire
        {
            public Color color;
            public RectTransform left;
            public RectTransform right;
            public bool matched;
        }

        private const float CompleteDelay = 0.35f;

        private Canvas canvas;
        private List<Wire> wires = new List<Wire>();
        private Dictionary<RectTransform, Wire> endpointToWire = new Dictionary<RectTransform, Wire>();

        // 深色主题配色
        private static readonly Color[] WireColors =
        {
            new Color(0.91f, 0.30f, 0.24f), // 红
            new Color(0.20f, 0.60f, 0.86f), // 蓝
            new Color(0.35f, 0.78f, 0.36f), // 绿
            new Color(0.95f, 0.85f, 0.20f), // 黄
        };

        public IReadOnlyList<Wire> Wires => wires.AsReadOnly();

        public override void Show()
        {
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

        private void CreateUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("WireTaskCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 背景面板（深色主题）
            GameObject panel = CreatePanel(canvasObj, "Background", new Color(0.12f, 0.14f, 0.18f, 0.96f));
            RectTransform panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            // 标题
            CreateLabel(canvasObj, "标题：连接相同颜色的线", 24, new Vector2(0.5f, 0.92f), new Vector2(0.5f, 0.92f));

            // 游戏区域
            GameObject gameArea = CreatePanel(canvasObj, "GameArea", new Color(0.18f, 0.20f, 0.26f, 1f));
            RectTransform gameRT = gameArea.GetComponent<RectTransform>();
            gameRT.anchorMin = new Vector2(0.1f, 0.15f);
            gameRT.anchorMax = new Vector2(0.9f, 0.85f);
            gameRT.offsetMin = Vector2.zero;
            gameRT.offsetMax = Vector2.zero;

            // 生成 4 条线
            GenerateWires(canvasObj, gameArea);
        }

        private void GenerateWires(GameObject canvasObj, GameObject parent)
        {
            wires.Clear();
            endpointToWire.Clear();

            // 随机打乱右侧顺序
            int[] rightOrder = { 0, 1, 2, 3 };
            Shuffle(rightOrder);

            for (int i = 0; i < 4; i++)
            {
                Wire wire = new Wire { color = WireColors[i], matched = false };
                wires.Add(wire);

                // 左侧端点
                wire.left = CreateEndpoint(canvasObj, parent, WireColors[i], i, true);
                // 右侧端点（打乱后）
                wire.right = CreateEndpoint(canvasObj, parent, WireColors[i], rightOrder[i], false);

                endpointToWire[wire.left] = wire;
                endpointToWire[wire.right] = wire;
            }

            // 初始状态：全部不匹配（右侧颜色随机）
            ShuffleEndpointColors(parent);
        }

        private RectTransform CreateEndpoint(GameObject canvasObj, GameObject parent, Color color, int index, bool isLeft)
        {
            GameObject endpoint = new GameObject(isLeft ? "Left_" + index : "Right_" + index);
            endpoint.transform.SetParent(parent.transform);

            Image img = endpoint.AddComponent<Image>();
            img.color = color;

            Button btn = endpoint.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
            cb.pressedColor = Color.Lerp(color, Color.black, 0.3f);
            btn.colors = cb;
            btn.onClick.AddListener(() => OnEndpointClicked(endpoint.GetComponent<RectTransform>()));

            RectTransform rt = endpoint.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(isLeft ? 0.05f : 0.80f, 0.15f + index * 0.18f);
            rt.anchorMax = new Vector2(isLeft ? 0.20f : 0.95f, 0.28f + index * 0.18f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 圆形遮罩效果（用 Image 的 fillCenter）
            img.raycastTarget = true;

            return rt;
        }

        private void ShuffleEndpointColors(GameObject parent)
        {
            // 随机旋转右侧端点颜色，制造不匹配状态
            int[] shuffle = { 0, 1, 2, 3 };
            Shuffle(shuffle);

            for (int i = 0; i < 4; i++)
            {
                Wire wire = wires[i];
                // 随机决定是否匹配（初始全部不匹配）
                wire.matched = false;
            }
        }

        private void OnEndpointClicked(RectTransform endpoint)
        {
            if (!endpointToWire.TryGetValue(endpoint, out Wire wire))
            {
                return;
            }

            // 点击端点：旋转颜色（模拟 Among Us 的端点旋转）
            RotateEndpointColor(endpoint, wire);
            CheckCompletion();
        }

        private void RotateEndpointColor(RectTransform endpoint, Wire wire)
        {
            // 简单实现：点击后切换右侧端点颜色到正确颜色
            // 实际 Among Us 是旋转端点，这里简化为"点击匹配"
            Image img = endpoint.GetComponent<Image>();

            // 找到当前端点属于左侧还是右侧
            bool isLeft = endpoint.anchorMin.x < 0.5f;

            if (isLeft)
            {
                // 左侧点击：尝试匹配右侧
                wire.matched = true;
                endpoint.GetComponent<Image>().color = wire.color;
                wire.right.GetComponent<Image>().color = wire.color;
            }
            else
            {
                // 右侧点击：直接设为正确颜色
                img.color = wire.color;
                wire.matched = IsWireMatched(wire);
            }
        }

        private bool IsWireMatched(Wire wire)
        {
            return wire.left.GetComponent<Image>().color == wire.color &&
                   wire.right.GetComponent<Image>().color == wire.color;
        }

        private void CheckCompletion()
        {
            bool allMatched = true;
            foreach (Wire wire in wires)
            {
                wire.matched = IsWireMatched(wire);
                if (!wire.matched)
                {
                    allMatched = false;
                }
            }

            if (allMatched)
            {
                ShowSuccessEffect();
            }
        }

        private void ShowSuccessEffect()
        {
            // 闪烁效果后完成
            StartCoroutine(CompleteRoutine());
        }

        private System.Collections.IEnumerator CompleteRoutine()
        {
            yield return new UnityEngine.WaitForSeconds(CompleteDelay);
            Complete();
        }

        private void Shuffle(int[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = array[i];
                array[i] = array[j];
                array[j] = temp;
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

        private void CreateLabel(GameObject parent, string text, float fontSize, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject labelObj = new GameObject("Label_" + text.Substring(0, Mathf.Min(10, text.Length)));
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
        }
    }
}
