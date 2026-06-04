using GanglandUndercover.SocialDeduction;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 证据归档小游戏 — 拖拽证据条目到对应的案件槽中。
    /// 6-8 件证据、3 个案件槽，正确匹配所有证据即完成。
    /// </summary>
    public sealed class EvidenceArchiveTask : MiniGameBase
    {
        private const float SnapThreshold = 72f;
        private const float SuccessDelay = 0.35f;

        private Canvas canvas;
        private PoliceStationTasks.EvidenceItem[] evidenceItems;
        private PoliceStationTasks.CaseSlot[] caseSlots;

        private List<DraggableEvidence> draggables = new List<DraggableEvidence>();
        private List<CaseSlotView> slotViews = new List<CaseSlotView>();
        private int totalMatched;

        private class DraggableEvidence
        {
            public GameObject obj;
            public RectTransform rt;
            public PoliceStationTasks.EvidenceItem data;
            public Vector2 originalPosition;
            public bool isPlaced;
        }

        private class CaseSlotView
        {
            public GameObject obj;
            public RectTransform rt;
            public PoliceStationTasks.CaseSlot data;
            public int placedCount;
        }

        public override void Show()
        {
            GeneratePuzzle();
            CreateUI();
            gameObject.SetActive(true);
        }

        public override void Hide()
        {
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
                canvas = null;
            }

            draggables.Clear();
            slotViews.Clear();
            totalMatched = 0;
            gameObject.SetActive(false);
        }

        private void GeneratePuzzle()
        {
            var puzzle = PoliceStationTasks.GenerateEvidencePuzzle();
            evidenceItems = puzzle.items;
            caseSlots = puzzle.slots;
        }

        private void CreateUI()
        {
            canvas = CreateUICanvas("EvidenceArchiveCanvas");
            RectTransform root = canvas.GetComponent<RectTransform>();

            // 标题
            Text titleText = CreateText(root, "证据归档", 32, Color.white, TextAnchor.MiddleCenter);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, 380f);
            titleText.rectTransform.sizeDelta = new Vector2(400f, 48f);

            // 说明文字
            Text hintText = CreateText(root, "拖拽证据到对应案件档案袋", 18, new Color(0.7f, 0.7f, 0.7f, 1f), TextAnchor.MiddleCenter);
            hintText.rectTransform.anchoredPosition = new Vector2(0f, 342f);
            hintText.rectTransform.sizeDelta = new Vector2(600f, 28f);

            // ── 证据条目区（上方三行）─────────────────────
            const float evidenceStartX = -420f;
            const float evidenceStartY = 240f;
            const float evidenceGapX = 180f;
            const float evidenceGapY = 120f;
            const int itemsPerRow = 4;

            for (int i = 0; i < evidenceItems.Length; i++)
            {
                int row = i / itemsPerRow;
                int col = i % itemsPerRow;
                float x = evidenceStartX + col * evidenceGapX;
                float y = evidenceStartY - row * evidenceGapY;

                GameObject itemObj = CreateEvidenceItem(root, evidenceItems[i], new Vector2(x, y));
                DraggableEvidence de = new DraggableEvidence
                {
                    obj = itemObj,
                    rt = itemObj.GetComponent<RectTransform>(),
                    data = evidenceItems[i],
                    originalPosition = new Vector2(x, y),
                    isPlaced = false,
                };

                // 拖拽绑定
                SetupDrag(itemObj, de);
                draggables.Add(de);
            }

            // ── 案件槽区（下方）─────────────────────────
            const float slotStartX = -420f;
            const float slotY = 60f;
            const float slotGapX = 420f;

            for (int i = 0; i < caseSlots.Length; i++)
            {
                float x = slotStartX + i * slotGapX;
                GameObject slotObj = CreateCaseSlot(root, caseSlots[i], new Vector2(x, slotY));

                CaseSlotView sv = new CaseSlotView
                {
                    obj = slotObj,
                    rt = slotObj.GetComponent<RectTransform>(),
                    data = caseSlots[i],
                    placedCount = 0,
                };

                slotViews.Add(sv);
            }

            // ── 提交按钮 ──────────────────────────────────
            GameObject submitBtn = CreateButton(root, "确认归档", new Vector2(0f, -120f), new Vector2(180f, 44f));
        }

        // ─── UI 构建辅助 ──────────────────────────────
        private static Canvas CreateUICanvas(string name)
        {
            GameObject go = new GameObject(name);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            go.AddComponent<GraphicRaycaster>();

            // 半透明遮罩
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            bg.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            bg.GetComponent<RectTransform>().anchorMax = Vector2.one;
            bg.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            return canvas;
        }

        private static Text CreateText(Transform parent, string content, int fontSize, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject("Text_", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateEvidenceItem(Transform parent, PoliceStationTasks.EvidenceItem data, Vector2 position)
        {
            GameObject go = new GameObject("Evidence_" + data.ItemName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.22f, 0.18f, 0.08f, 1f); // 文件棕
            img.raycastTarget = true;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(152f, 52f);

            // 边框
            GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(go.transform, false);
            frame.GetComponent<Image>().color = new Color(0.62f, 0.52f, 0.32f, 1f);
            frame.GetComponent<Image>().raycastTarget = false;
            RectTransform frt = frame.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.sizeDelta = new Vector2(-4f, -4f);
            frame.transform.SetAsFirstSibling();

            // 内层
            GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(go.transform, false);
            inner.GetComponent<Image>().color = new Color(0.28f, 0.24f, 0.14f, 1f);
            inner.GetComponent<Image>().raycastTarget = false;
            RectTransform ert = inner.GetComponent<RectTransform>();
            ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one;
            ert.sizeDelta = new Vector2(-6f, -6f);

            // 名称标签
            Text label = CreateText(go.transform, data.DisplayText, 16, Color.white, TextAnchor.MiddleCenter);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = new Vector2(140f, 40f);

            return go;
        }

        private static GameObject CreateCaseSlot(Transform parent, PoliceStationTasks.CaseSlot slotData, Vector2 position)
        {
            GameObject go = new GameObject("Slot_" + slotData.CaseName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.10f, 0.14f, 0.20f, 1f); // 深档案蓝
            img.raycastTarget = true;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(340f, 140f);

            // 边框
            GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(go.transform, false);
            frame.GetComponent<Image>().color = new Color(0.42f, 0.48f, 0.62f, 1f);
            frame.GetComponent<Image>().raycastTarget = false;
            RectTransform frt = frame.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.sizeDelta = new Vector2(-4f, -4f);
            frame.transform.SetAsFirstSibling();

            // 案件名标签
            Text slotLabel = CreateText(go.transform, slotData.CaseName, 20, new Color(0.82f, 0.88f, 1f, 1f), TextAnchor.UpperCenter);
            slotLabel.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            slotLabel.rectTransform.sizeDelta = new Vector2(300f, 30f);

            // 容量标签
            Text capLabel = CreateText(go.transform, $"0 / {slotData.Capacity}", 14, new Color(0.5f, 0.5f, 0.5f, 1f), TextAnchor.MiddleCenter);
            capLabel.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            capLabel.rectTransform.sizeDelta = new Vector2(120f, 20f);
            capLabel.name = "CapacityLabel";

            return go;
        }

        private GameObject CreateButton(Transform parent, string label, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.18f, 0.48f, 0.72f, 1f);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            Text btnLabel = CreateText(go.transform, label, 20, Color.white, TextAnchor.MiddleCenter);
            btnLabel.rectTransform.anchorMin = Vector2.zero;
            btnLabel.rectTransform.anchorMax = Vector2.one;
            btnLabel.rectTransform.sizeDelta = Vector2.zero;

            go.GetComponent<Button>().onClick.AddListener(() => OnSubmit());
            return go;
        }

        // ─── 拖拽逻辑 ──────────────────────────────────

        private DraggableEvidence currentDrag;
        private Vector2 dragOffset;

        private void SetupDrag(GameObject obj, DraggableEvidence de)
        {
            EventTrigger trigger = obj.AddComponent<EventTrigger>();

            var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            beginDrag.callback.AddListener(_ => OnBeginDrag(de));
            trigger.triggers.Add(beginDrag);

            var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener(data => OnDrag(de, (PointerEventData)data));
            trigger.triggers.Add(dragEntry);

            var endDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            endDrag.callback.AddListener(_ => OnEndDrag(de));
            trigger.triggers.Add(endDrag);
        }

        private void OnBeginDrag(DraggableEvidence de)
        {
            if (de.isPlaced) return;
            currentDrag = de;
            de.rt.SetAsLastSibling();
            // 计算拖拽偏移
            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                Input.mousePosition, canvas.worldCamera, out mousePos);
            dragOffset = de.rt.anchoredPosition - mousePos;
        }

        private void OnDrag(DraggableEvidence de, PointerEventData data)
        {
            if (currentDrag != de || de.isPlaced) return;
            RectTransform parentRect = canvas.GetComponent<RectTransform>();
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, data.position, canvas.worldCamera, out localPoint);
            de.rt.anchoredPosition = localPoint + dragOffset;
        }

        private void OnEndDrag(DraggableEvidence de)
        {
            if (currentDrag != de) return;
            currentDrag = null;

            // 检测是否拖到正确的案件槽
            CaseSlotView matchedSlot = null;
            foreach (CaseSlotView sv in slotViews)
            {
                float dist = Vector2.Distance(de.rt.anchoredPosition, sv.rt.anchoredPosition);
                if (dist < SnapThreshold && sv.data.CaseTag == de.data.CaseTag && sv.placedCount < sv.data.Capacity)
                {
                    matchedSlot = sv;
                    break;
                }
            }

            if (matchedSlot != null)
            {
                // 吸附到槽
                de.rt.anchoredPosition = matchedSlot.rt.anchoredPosition;
                de.isPlaced = true;
                de.obj.GetComponent<Image>().color = new Color(0.18f, 0.52f, 0.22f, 1f); // 绿色表示已归档
                de.obj.GetComponent<Image>().raycastTarget = false;

                matchedSlot.placedCount++;
                totalMatched++;
                UpdateSlotCapacity(matchedSlot);

                // 检查是否全部完成
                if (totalMatched >= evidenceItems.Length)
                {
                    Invoke(nameof(OnComplete), SuccessDelay);
                }
            }
            else
            {
                // 回弹到原位
                de.rt.anchoredPosition = de.originalPosition;
            }
        }

        private void UpdateSlotCapacity(CaseSlotView sv)
        {
            Text capLabel = sv.obj.transform.Find("CapacityLabel")?.GetComponent<Text>();
            if (capLabel != null)
            {
                capLabel.text = $"{sv.placedCount} / {sv.data.Capacity}";
            }
        }

        private void OnSubmit()
        {
            // 手动提交：检查是否所有证据都已归档
            int correct = 0;
            foreach (DraggableEvidence de in draggables)
            {
                if (de.isPlaced)
                {
                    correct++;
                }
            }

            if (correct >= evidenceItems.Length)
            {
                OnComplete();
            }
        }

        private void OnComplete()
        {
            Complete();
            Invoke(nameof(Hide), 0.5f);
        }

        private void Update()
        {
            // 快捷键：空格提交
            if (Input.GetKeyDown(KeyCode.Space))
            {
                OnSubmit();
            }
        }
    }
}