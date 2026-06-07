using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 分类排序小游戏（档案/垃圾分类）。
    /// 4 个可拖拽物品，拖到 4 个目标槽正确匹配即完成。
    /// </summary>
    public sealed class SortTask : MiniGameBase
    {
        private const int ItemCount = 4;
        private const float SnapThreshold = 60f;
        private const float SuccessDelay = 0.35f;

        // 物品标签（代表不同类别）
        private static readonly string[] ItemLabels = { "机密", "公开", "销毁", "归档" };
        private static readonly string[] SlotLabels = { "机密柜", "公告栏", "碎纸机", "档案室" };

        // 正确映射：itemIndex → 该物品应去的 slotIndex（初始化时打乱）
        private int[] correctMapping;

        private Canvas canvas;
        private List<SortItem> items = new List<SortItem>();
        private List<SortSlot> slots = new List<SortSlot>();
        private int matchedCount;

        private class SortItem
        {
            public GameObject obj;
            public RectTransform rt;
            public int itemIndex;
            public bool isPlaced;
        }

        private class SortSlot
        {
            public GameObject obj;
            public RectTransform rt;
            public Text label;
            public int slotIndex;
            public int expectedItemIndex; // 此槽期望的物品
            public bool isOccupied;
        }

        public override void Show()
        {
            GenerateMapping();
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
            items.Clear();
            slots.Clear();
            matchedCount = 0;
            gameObject.SetActive(false);
        }

        private void GenerateMapping()
        {
            // 正确映射：物品 i → 槽 slotIndex
            correctMapping = new int[ItemCount];
            int[] slotOrder = { 0, 1, 2, 3 };
            Shuffle(slotOrder);

            for (int i = 0; i < ItemCount; i++)
            {
                correctMapping[i] = slotOrder[i];
            }
        }

        private void CreateUI()
        {
            GameObject canvasObj = new GameObject("SortTaskCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 背景
            GameObject bg = CreatePanel(canvasObj, "Background", new Color(0.12f, 0.14f, 0.18f, 0.96f));
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // 标题
            CreateLabel(canvasObj, "档案分类：将文件拖入正确的柜子", 20,
                new Vector2(0.5f, 0.93f), new Vector2(0.5f, 0.93f));

            // 创建物品区域（上方，可拖拽）
            CreateItems(canvasObj);

            // 创建目标槽（下方）
            CreateSlots(canvasObj);
        }

        private void CreateItems(GameObject parent)
        {
            items.Clear();
            int[] shuffledItems = { 0, 1, 2, 3 };
            Shuffle(shuffledItems);

            for (int i = 0; i < ItemCount; i++)
            {
                int itemIndex = shuffledItems[i];
                SortItem item = new SortItem { itemIndex = itemIndex, isPlaced = false };

                GameObject itemObj = CreatePanel(parent, "Item_" + itemIndex, new Color(0.32f, 0.28f, 0.48f, 1f));
                RectTransform rt = itemObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.08f + i * 0.22f, 0.50f);
                rt.anchorMax = new Vector2(0.26f + i * 0.22f, 0.68f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                // Item 标签
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(itemObj.transform);
                Text txt = labelObj.AddComponent<Text>();
                txt.text = ItemLabels[itemIndex];
                txt.fontSize = 16;
                txt.color = new Color(0.92f, 0.92f, 0.96f);
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                RectTransform labelRT = labelObj.GetComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;

                // 可拖拽
                AddDragEvents(itemObj, item);

                item.obj = itemObj;
                item.rt = rt;
                items.Add(item);
            }
        }

        private void CreateSlots(GameObject parent)
        {
            slots.Clear();

            for (int i = 0; i < ItemCount; i++)
            {
                SortSlot slot = new SortSlot { slotIndex = i, expectedItemIndex = -1, isOccupied = false };

                // 确定此槽期望的物品
                for (int j = 0; j < ItemCount; j++)
                {
                    if (correctMapping[j] == i)
                    {
                        slot.expectedItemIndex = j;
                        break;
                    }
                }

                GameObject slotObj = CreatePanel(parent, "Slot_" + i, new Color(0.18f, 0.20f, 0.26f, 1f));
                RectTransform rt = slotObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.08f + i * 0.22f, 0.22f);
                rt.anchorMax = new Vector2(0.26f + i * 0.22f, 0.40f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                // Slot 标签
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(slotObj.transform);
                Text txt = labelObj.AddComponent<Text>();
                txt.text = SlotLabels[i];
                txt.fontSize = 14;
                txt.color = new Color(0.60f, 0.62f, 0.66f);
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                RectTransform labelRT = labelObj.GetComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;

                slot.obj = slotObj;
                slot.rt = rt;
                slot.label = txt;
                slots.Add(slot);
            }
        }

        private void AddDragEvents(GameObject itemObj, SortItem item)
        {
            // 使用 EventTrigger 实现拖拽

            // BeginDrag
            EventTrigger trigger = itemObj.AddComponent<EventTrigger>();
            EventTrigger.Entry beginDrag = new EventTrigger.Entry();
            beginDrag.eventID = EventTriggerType.BeginDrag;
            beginDrag.callback.AddListener((data) =>
            {
                if (item.isPlaced) return;
                // 提升层级
                item.rt.SetAsLastSibling();
                item.obj.GetComponent<Image>().color = new Color(0.42f, 0.38f, 0.58f, 1f);
            });
            trigger.triggers.Add(beginDrag);

            // Drag
            EventTrigger.Entry dragEntry = new EventTrigger.Entry();
            dragEntry.eventID = EventTriggerType.Drag;
            dragEntry.callback.AddListener((data) =>
            {
                if (item.isPlaced) return;
                PointerEventData pointerData = (PointerEventData)data;
                // 在 Overlay Canvas 中直接跟随鼠标
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.GetComponent<RectTransform>(),
                    pointerData.position,
                    null,
                    out localPoint);
                item.rt.localPosition = localPoint;
            });
            trigger.triggers.Add(dragEntry);

            // EndDrag
            EventTrigger.Entry endDrag = new EventTrigger.Entry();
            endDrag.eventID = EventTriggerType.EndDrag;
            endDrag.callback.AddListener((data) =>
            {
                if (item.isPlaced) return;
                item.obj.GetComponent<Image>().color = new Color(0.32f, 0.28f, 0.48f, 1f);
                TrySnapToSlot(item);
            });
            trigger.triggers.Add(endDrag);
        }

        private void TrySnapToSlot(SortItem item)
        {
            Vector2 itemScreenPos = RectTransformUtility.WorldToScreenPoint(null, item.rt.position);

            // 找最近的槽
            SortSlot nearestSlot = null;
            float nearestDist = float.MaxValue;

            foreach (SortSlot slot in slots)
            {
                if (slot.isOccupied) continue;

                Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(null, slot.rt.position);
                float dist = Vector2.Distance(itemScreenPos, slotScreenPos);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestSlot = slot;
                }
            }

            if (nearestSlot != null && nearestDist < SnapThreshold)
            {
                // 吸附到槽中
                item.rt.SetParent(nearestSlot.rt);
                item.rt.anchorMin = Vector2.zero;
                item.rt.anchorMax = Vector2.one;
                item.rt.offsetMin = new Vector2(4, 4);
                item.rt.offsetMax = new Vector2(-4, -4);
                item.rt.localPosition = Vector3.zero;

                item.isPlaced = true;
                nearestSlot.isOccupied = true;

                // 检查是否正确
                if (item.itemIndex == nearestSlot.expectedItemIndex)
                {
                    // 正确！
                    item.obj.GetComponent<Image>().color = new Color(0.18f, 0.62f, 0.32f, 0.7f);
                    matchedCount++;
                    nearestSlot.obj.GetComponent<Image>().color = new Color(0.18f, 0.62f, 0.32f, 0.4f);
                    nearestSlot.label.text = "✓ " + SlotLabels[nearestSlot.slotIndex];
                    nearestSlot.label.color = new Color(0.35f, 0.78f, 0.36f);

                    if (matchedCount >= ItemCount)
                    {
                        StartCoroutine(CompleteRoutine());
                    }
                }
                else
                {
                    // 错误！放回原处
                    StartCoroutine(WrongPlacementRoutine(item, nearestSlot));
                }
            }
            else
            {
                // 回弹到原始位置
                ResetItemPosition(item);
            }
        }

        private IEnumerator WrongPlacementRoutine(SortItem item, SortSlot slot)
        {
            // 红色闪烁
            item.obj.GetComponent<Image>().color = new Color(0.90f, 0.20f, 0.20f, 0.7f);
            slot.obj.GetComponent<Image>().color = new Color(0.90f, 0.20f, 0.20f, 0.4f);
            yield return new WaitForSeconds(0.35f);

            // 恢复
            slot.obj.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.26f, 1f);
            slot.isOccupied = false;
            item.isPlaced = false;

            // 放回物品列表
            item.rt.SetParent(canvas.transform);
            item.obj.GetComponent<Image>().color = new Color(0.32f, 0.28f, 0.48f, 1f);
            ResetItemPosition(item);
        }

        private void ResetItemPosition(SortItem item)
        {
            // 放回默认位置
            item.rt.anchorMin = new Vector2(0.08f + items.IndexOf(item) * 0.22f, 0.50f);
            item.rt.anchorMax = new Vector2(0.26f + items.IndexOf(item) * 0.22f, 0.68f);
            item.rt.offsetMin = Vector2.zero;
            item.rt.offsetMax = Vector2.zero;
        }

        private IEnumerator CompleteRoutine()
        {
            yield return new WaitForSeconds(SuccessDelay);
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
