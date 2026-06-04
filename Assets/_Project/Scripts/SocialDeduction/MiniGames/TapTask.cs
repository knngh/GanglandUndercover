using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 快速点击小游戏（Among Us 反应速度）。
    /// 屏幕随机位置出现 8 个目标圆圈，限定时间内全部点击。
    /// </summary>
    public sealed class TapTask : MiniGameBase
    {
        private const int TotalTargets = 8;
        private const float TimeLimit = 6.5f;        // 总时间限制（秒）
        private const float TargetLifetime = 1.2f;   // 单个目标出现持续（秒）
        private const float SpawnInterval = 0.65f;   // 目标出现间隔（秒）
        private const float TargetSize = 60f;        // 目标大小（像素）
        private const float SuccessDelay = 0.35f;

        // 目标出现区域（屏幕百分比）
        private const float MarginX = 0.08f;
        private const float MarginY = 0.12f;
        private const float HeaderY = 0.18f; // 顶部留给标题
        private const float FooterY = 0.15f; // 底部留给计时器

        private Canvas canvas;
        private Text timerText;
        private Text scoreText;
        private float remainingTime;
        private float spawnTimer;
        private int targetsClicked;
        private int targetsSpawned;
        private bool isComplete;

        private List<TapTarget> activeTargets = new List<TapTarget>();

        private class TapTarget
        {
            public GameObject obj;
            public RectTransform rt;
            public float lifetime;
            public bool isClicked;
        }

        public override void Show()
        {
            remainingTime = TimeLimit;
            spawnTimer = 0f;
            targetsClicked = 0;
            targetsSpawned = 0;
            isComplete = false;
            activeTargets.Clear();
            CreateUI();
            gameObject.SetActive(true);
        }

        public override void Hide()
        {
            isComplete = true;
            foreach (TapTarget target in activeTargets)
            {
                if (target.obj != null) Destroy(target.obj);
            }
            activeTargets.Clear();

            if (canvas != null)
            {
                Destroy(canvas.gameObject);
                canvas = null;
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (isComplete) return;

            // 计时
            remainingTime -= Time.deltaTime;

            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                isComplete = true;
                StartCoroutine(TimeOutRoutine());
                return;
            }

            // 刷新计时器显示
            timerText.text = string.Format("{0:F1}s", remainingTime);
            if (remainingTime < 2f)
            {
                timerText.color = new Color(0.90f, 0.20f, 0.20f);
            }

            // 生成新目标
            if (targetsSpawned < TotalTargets)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f || (targetsSpawned == 0 && spawnTimer <= 0.1f))
                {
                    SpawnTarget();
                    spawnTimer = SpawnInterval;
                }
            }

            // 刷新现有目标的生命周期
            for (int i = activeTargets.Count - 1; i >= 0; i--)
            {
                TapTarget target = activeTargets[i];
                if (target.isClicked) continue;

                target.lifetime -= Time.deltaTime;

                if (target.lifetime <= 0f)
                {
                    // 目标过期消失
                    RemoveTarget(target, i);
                }
                else
                {
                    // 脉冲动画（大小正弦波）
                    float scale = 1f + Mathf.Sin(Time.time * 8f) * 0.12f;
                    target.rt.localScale = new Vector3(scale, scale, 1f);

                    // 接近消失时变红
                    if (target.lifetime < 0.3f)
                    {
                        Image img = target.obj.GetComponent<Image>();
                        img.color = Color.Lerp(
                            new Color(0.35f, 0.65f, 0.80f, 0.85f),
                            new Color(0.90f, 0.20f, 0.20f, 0.85f),
                            1f - target.lifetime / 0.3f);
                    }
                }
            }
        }

        private void CreateUI()
        {
            GameObject canvasObj = new GameObject("TapTaskCanvas");
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
            CreateLabel(canvasObj, "快速点击：点掉所有目标！", 22,
                new Vector2(0.5f, 0.93f), new Vector2(0.5f, 0.93f));

            // 分数
            scoreText = CreateLabel(canvasObj, "0 / " + TotalTargets, 18,
                new Vector2(0.5f, 0.87f), new Vector2(0.5f, 0.87f));
            scoreText.color = new Color(0.35f, 0.78f, 0.36f);

            // 计时器
            timerText = CreateLabel(canvasObj, TimeLimit.ToString("F1") + "s", 24,
                new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f));
            timerText.color = new Color(0.88f, 0.90f, 0.92f);
        }

        private void SpawnTarget()
        {
            targetsSpawned++;

            GameObject targetObj = new GameObject("Target_" + targetsSpawned);
            targetObj.transform.SetParent(canvas.transform);

            Image img = targetObj.AddComponent<Image>();
            img.color = new Color(0.35f, 0.65f, 0.80f, 0.85f);

            Button btn = targetObj.AddComponent<Button>();
            btn.onClick.AddListener(() => OnTargetClicked(targetObj));

            RectTransform rt = targetObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(TargetSize, TargetSize);

            // 随机位置（避开标题和底部区域）
            float x = Random.Range(MarginX, 1f - MarginX);
            float y = Random.Range(MarginY + FooterY, 1f - MarginY - HeaderY);
            rt.anchorMin = new Vector2(x, y);
            rt.anchorMax = new Vector2(x, y);
            rt.anchoredPosition = Vector2.zero;

            TapTarget target = new TapTarget
            {
                obj = targetObj,
                rt = rt,
                lifetime = TargetLifetime,
                isClicked = false
            };

            activeTargets.Add(target);
        }

        private void OnTargetClicked(GameObject targetObj)
        {
            TapTarget target = activeTargets.Find(t => t.obj == targetObj);
            if (target == null || target.isClicked || isComplete) return;

            target.isClicked = true;
            targetsClicked++;

            // 点击效果：放大并淡出
            Image img = target.obj.GetComponent<Image>();
            img.color = new Color(0.18f, 0.72f, 0.32f, 1f);
            StartCoroutine(PopEffect(target.obj));

            // 更新分数
            scoreText.text = targetsClicked + " / " + TotalTargets;

            if (targetsClicked >= TotalTargets)
            {
                isComplete = true;
                scoreText.color = new Color(0.18f, 0.82f, 0.32f);
                timerText.color = new Color(0.18f, 0.82f, 0.32f);
                timerText.text = "完成！";
                StartCoroutine(SuccessRoutine());
            }
        }

        private IEnumerator PopEffect(GameObject obj)
        {
            float elapsed = 0f;
            float duration = 0.25f;
            RectTransform rt = obj.GetComponent<RectTransform>();
            Vector3 originalScale = rt.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = Mathf.Lerp(1.4f, 0f, t * t);
                rt.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            activeTargets.RemoveAll(t => t.obj == obj);
            Destroy(obj);
        }

        private void RemoveTarget(TapTarget target, int index)
        {
            activeTargets.RemoveAt(index);
            StartCoroutine(FadeOutTarget(target.obj));
        }

        private IEnumerator FadeOutTarget(GameObject obj)
        {
            Image img = obj.GetComponent<Image>();
            float elapsed = 0f;
            float duration = 0.2f;
            Color originalColor = img.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                img.color = Color.Lerp(originalColor, Color.clear, elapsed / duration);
                yield return null;
            }

            Destroy(obj);
        }

        private IEnumerator SuccessRoutine()
        {
            yield return new WaitForSeconds(SuccessDelay);
            Complete();
        }

        private IEnumerator TimeOutRoutine()
        {
            timerText.text = "时间到！";
            yield return new WaitForSeconds(0.5f);
            Cancel();
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
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            RectTransform rt = labelObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return txt;
        }
    }
}
