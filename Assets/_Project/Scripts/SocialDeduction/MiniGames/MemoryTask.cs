using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 记忆小游戏（Among Us Memory Task 复刻）。
    /// 3 对符号闪烁显示 → 隐藏 → 点击匹配。
    /// </summary>
    public sealed class MemoryTask : MiniGameBase
    {
        private const int PairCount = 3;          // 3 对 = 6 个格子
        private const float ShowDuration = 1.8f; // 显示时间
        private const float HideDuration = 0.3f; // 隐藏动画时间
        private const float MatchDelay = 0.45f;  // 匹配后延迟

        private Canvas canvas;
        private List<MemoryCell> cells = new List<MemoryCell>();
        private int firstSelected = -1;   // 第一次选中的索引
        private int secondSelected = -1;   // 第二次选中的索引
        private int matchedPairs;
        private bool isProcessing;

        // 深色主题符号（用字符表示）
        private static readonly string[] Symbols =
        {
            "◆", "▲", "●",
            "◆", "▲", "●"
        };

        private class MemoryCell
        {
            public GameObject obj;
            public RectTransform rt;
            public Text label;
            public int symbolIndex;
            public bool isMatched;
            public bool isRevealed;
        }

        public override void Show()
        {
            CreateUI();
            gameObject.SetActive(true);
            StartCoroutine(ShowThenHideRoutine());
        }

        public override void Hide()
        {
            if (canvas != null)
            {
                DestroyRuntimeObject(canvas.gameObject);
                canvas = null;
            }
            cells.Clear();
            firstSelected = -1;
            secondSelected = -1;
            matchedPairs = 0;
            isProcessing = false;
            gameObject.SetActive(false);
        }

        private void CreateUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("MemoryTaskCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 背景（深色主题）
            GameObject bg = CreatePanel(canvasObj, "Background", new Color(0.12f, 0.14f, 0.18f, 0.96f));
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // 标题
            CreateLabel(canvasObj, "记忆匹配：记住符号位置", 22,
                new Vector2(0.5f, 0.90f), new Vector2(0.5f, 0.90f));

            // 提示文字
            CreateLabel(canvasObj, "符号会短暂显示，记住位置后点击匹配", 15,
                new Vector2(0.5f, 0.83f), new Vector2(0.5f, 0.83f));

            // 网格容器
            GameObject gridObj = new GameObject("Grid");
            gridObj.transform.SetParent(canvasObj.transform);
            GridLayoutGroup grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(120, 120);
            grid.spacing = new Vector2(12, 12);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            RectTransform gridRT = gridObj.GetComponent<RectTransform>();
            gridRT.anchorMin = new Vector2(0.20f, 0.20f);
            gridRT.anchorMax = new Vector2(0.80f, 0.72f);
            gridRT.offsetMin = Vector2.zero;
            gridRT.offsetMax = Vector2.zero;

            // 生成 6 个格子
            GenerateCells(gridObj);
        }

        private void GenerateCells(GameObject parent)
        {
            cells.Clear();

            // 打乱符号顺序
            int[] indices = { 0, 1, 2, 3, 4, 5 };
            Shuffle(indices);

            for (int i = 0; i < 6; i++)
            {
                MemoryCell cell = new MemoryCell
                {
                    symbolIndex = indices[i]  // 用打乱后的索引决定符号
                };

                GameObject cellObj = new GameObject("Cell_" + i);
                cellObj.transform.SetParent(parent.transform);

                // 背景图片
                Image img = cellObj.AddComponent<Image>();
                img.color = new Color(0.22f, 0.24f, 0.30f, 1f);

                // 按钮
                Button btn = cellObj.AddComponent<Button>();
                int capturedIndex = i;
                btn.onClick.AddListener(() => OnCellClicked(capturedIndex));
                ColorBlock cb = btn.colors;
                cb.highlightedColor = new Color(0.32f, 0.34f, 0.42f);
                cb.pressedColor = new Color(0.12f, 0.14f, 0.20f);
                btn.colors = cb;

                // 文字（符号）
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(cellObj.transform);
                Text txt = labelObj.AddComponent<Text>();
                txt.fontSize = 36;
                txt.color = new Color(0.92f, 0.94f, 0.96f);
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.text = "";  // 初始隐藏

                RectTransform labelRT = labelObj.GetComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;

                cell.obj = cellObj;
                cell.rt = cellObj.GetComponent<RectTransform>();
                cell.label = txt;
                cell.isMatched = false;
                cell.isRevealed = false;

                cells.Add(cell);
            }
        }

        private IEnumerator ShowThenHideRoutine()
        {
            // 显示所有符号
            foreach (MemoryCell cell in cells)
            {
                cell.label.text = Symbols[cell.symbolIndex];
                cell.isRevealed = true;
            }

            yield return new WaitForSeconds(ShowDuration);

            // 隐藏所有符号
            foreach (MemoryCell cell in cells)
            {
                cell.label.text = "";
                cell.isRevealed = false;
                // 恢复默认颜色
                Image img = cell.obj.GetComponent<Image>();
                img.color = new Color(0.22f, 0.24f, 0.30f, 1f);
            }
        }

        private void OnCellClicked(int index)
        {
            if (isProcessing) return;
            if (cells[index].isMatched) return;
            if (cells[index].isRevealed) return;

            MemoryCell cell = cells[index];

            // 显示符号
            cell.label.text = Symbols[cell.symbolIndex];
            cell.isRevealed = true;
            cell.obj.GetComponent<Image>().color = new Color(0.28f, 0.32f, 0.42f, 1f);

            if (firstSelected == -1)
            {
                // 第一次选择
                firstSelected = index;
            }
            else if (secondSelected == -1 && index != firstSelected)
            {
                // 第二次选择
                secondSelected = index;
                StartCoroutine(CheckMatchRoutine());
            }
        }

        private IEnumerator CheckMatchRoutine()
        {
            isProcessing = true;

            yield return new WaitForSeconds(MatchDelay);

            MemoryCell first = cells[firstSelected];
            MemoryCell second = cells[secondSelected];

            // 判断是否匹配（符号相同）
            if (Symbols[first.symbolIndex] == Symbols[second.symbolIndex])
            {
                // 匹配成功
                first.isMatched = true;
                second.isMatched = true;
                first.obj.GetComponent<Image>().color = new Color(0.18f, 0.62f, 0.32f, 0.6f);
                second.obj.GetComponent<Image>().color = new Color(0.18f, 0.62f, 0.32f, 0.6f);
                matchedPairs++;

                if (matchedPairs >= PairCount)
                {
                    yield return new WaitForSeconds(0.5f);
                    Complete();
                }
            }
            else
            {
                // 不匹配，隐藏
                first.label.text = "";
                second.label.text = "";
                first.isRevealed = false;
                second.isRevealed = false;
                first.obj.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.30f, 1f);
                second.obj.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.30f, 1f);
            }

            firstSelected = -1;
            secondSelected = -1;
            isProcessing = false;
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
        }
    }
}
