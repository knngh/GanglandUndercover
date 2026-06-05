using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 清理陨石小游戏。屏幕随机出现 5 个陨石（圆形），
    /// 点击陨石将其击碎。限时 8 秒。
    /// </summary>
    public sealed class AsteroidTask : MiniGameBase
    {
        private const int TotalAsteroids = 5;
        private const float TimeLimit = 8f;
        private const float AsteroidMinSize = 55f;
        private const float AsteroidMaxSize = 85f;
        private const float SpawnMarginX = 0.06f;
        private const float SpawnMarginTop = 0.22f;
        private const float SpawnMarginBottom = 0.18f;
        private const float SuccessDelay = 0.4f;

        private Canvas canvas;
        private Text timerText;
        private Text scoreText;
        private float remainingTime;
        private int asteroidsDestroyed;
        private bool isComplete;
        private List<Asteroid> asteroids = new List<Asteroid>();

        private class Asteroid
        {
            public GameObject obj;
            public RectTransform rt;
            public bool isDestroyed;
            public float rotationSpeed;
        }

        public override void Show()
        {
            remainingTime = TimeLimit;
            asteroidsDestroyed = 0;
            isComplete = false;
            asteroids.Clear();
            CreateUI();
            SpawnAllAsteroids();
            gameObject.SetActive(true);
        }

        public override void Hide()
        {
            isComplete = true;
            StopAllCoroutines();
            foreach (Asteroid a in asteroids)
            {
                if (a.obj != null) Destroy(a.obj);
            }
            asteroids.Clear();

            if (canvas != null)
            {
                Destroy(canvas.gameObject);
                canvas = null;
            }
            gameObject.SetActive(false);
        }

        private void CreateUI()
        {
            GameObject canvasObj = new GameObject("AsteroidTaskCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 背景
            CreatePanel(canvasObj, "Background", new Color(0.08f, 0.10f, 0.20f, 0.97f));

            // 标题
            CreateLabel(canvasObj, "清理陨石：点击击碎所有陨石！", 22,
                new Vector2(0.5f, 0.94f), new Vector2(0.5f, 0.94f));

            // 分数
            scoreText = CreateLabel(canvasObj, "0 / 5", 18,
                new Vector2(0.5f, 0.89f), new Vector2(0.5f, 0.89f));
            scoreText.color = new Color(0.35f, 0.78f, 0.36f);

            // 计时器
            timerText = CreateLabel(canvasObj, TimeLimit.ToString("F1") + "s", 24,
                new Vector2(0.5f, 0.06f), new Vector2(0.5f, 0.06f));
            timerText.color = new Color(0.88f, 0.90f, 0.92f);
        }

        private void SpawnAllAsteroids()
        {
            for (int i = 0; i < TotalAsteroids; i++)
            {
                SpawnAsteroid(i);
            }
        }

        private void SpawnAsteroid(int index)
        {
            GameObject asteroidObj = new GameObject("Asteroid_" + index);
            asteroidObj.transform.SetParent(canvas.transform);

            Image img = asteroidObj.AddComponent<Image>();
            img.sprite = null; // 无sprite，用颜色填充圆形效果
            img.color = new Color(0.55f, 0.40f, 0.28f, 1f);
            img.raycastTarget = true;

            Button btn = asteroidObj.AddComponent<Button>();
            int capturedIndex = index;
            btn.onClick.AddListener(() => OnAsteroidClicked(capturedIndex));

            RectTransform rt = asteroidObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);

            float size = Random.Range(AsteroidMinSize, AsteroidMaxSize);
            rt.sizeDelta = new Vector2(size, size);

            float x = Random.Range(SpawnMarginX, 1f - SpawnMarginX);
            float y = Random.Range(SpawnMarginBottom, 1f - SpawnMarginTop);
            Vector2 screenPos = new Vector2(
                (x - 0.5f) * Screen.width,
                (y - 0.5f) * Screen.height);
            rt.anchoredPosition = screenPos;

            // 陨石不规则外观：添加多个小圆形子对象制造碎石感
            for (int j = 0; j < 3; j++)
            {
                GameObject chunk = new GameObject("Chunk_" + j);
                chunk.transform.SetParent(asteroidObj.transform);
                Image chunkImg = chunk.AddComponent<Image>();
                chunkImg.color = new Color(0.48f, 0.35f, 0.22f, 0.7f);
                chunkImg.raycastTarget = false;

                RectTransform chunkRT = chunk.GetComponent<RectTransform>();
                chunkRT.anchorMin = new Vector2(0.5f, 0.5f);
                chunkRT.anchorMax = new Vector2(0.5f, 0.5f);
                float chunkSize = size * Random.Range(0.22f, 0.38f);
                chunkRT.sizeDelta = new Vector2(chunkSize, chunkSize);
                chunkRT.anchoredPosition = new Vector2(
                    Random.Range(-size * 0.32f, size * 0.32f),
                    Random.Range(-size * 0.32f, size * 0.32f));
            }

            Asteroid asteroid = new Asteroid
            {
                obj = asteroidObj,
                rt = rt,
                isDestroyed = false,
                rotationSpeed = Random.Range(-30f, 30f)
            };

            asteroids.Add(asteroid);
        }

        private void Update()
        {
            if (isComplete) return;

            remainingTime -= Time.deltaTime;

            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                isComplete = true;
                StartCoroutine(TimeOutRoutine());
                return;
            }

            // 更新计时器
            timerText.text = string.Format("{0:F1}s", remainingTime);
            if (remainingTime < 2f)
            {
                timerText.color = new Color(0.90f, 0.20f, 0.20f);
            }

            // 旋转存活陨石
            foreach (Asteroid a in asteroids)
            {
                if (a.isDestroyed) continue;
                a.rt.Rotate(0f, 0f, a.rotationSpeed * Time.deltaTime);
            }
        }

        private void OnAsteroidClicked(int index)
        {
            if (isComplete) return;
            if (index < 0 || index >= asteroids.Count) return;

            Asteroid asteroid = asteroids[index];
            if (asteroid.isDestroyed) return;

            asteroid.isDestroyed = true;
            asteroidsDestroyed++;

            // 击碎效果
            StartCoroutine(DestroyEffect(asteroid.obj));

            scoreText.text = asteroidsDestroyed + " / " + TotalAsteroids;

            if (asteroidsDestroyed >= TotalAsteroids)
            {
                isComplete = true;
                scoreText.color = new Color(0.18f, 0.82f, 0.32f);
                timerText.color = new Color(0.18f, 0.82f, 0.32f);
                timerText.text = "完成!";
                StartCoroutine(SuccessRoutine());
            }
        }

        private IEnumerator DestroyEffect(GameObject asteroidObj)
        {
            // 碎片爆炸效果
            for (int i = 0; i < 6; i++)
            {
                GameObject particle = new GameObject("Particle_" + i);
                particle.transform.SetParent(canvas.transform);
                Image particleImg = particle.AddComponent<Image>();
                particleImg.color = new Color(
                    Random.Range(0.4f, 0.7f),
                    Random.Range(0.25f, 0.45f),
                    Random.Range(0.1f, 0.3f),
                    1f);
                particleImg.raycastTarget = false;

                RectTransform particleRT = particle.GetComponent<RectTransform>();
                particleRT.anchorMin = new Vector2(0.5f, 0.5f);
                particleRT.anchorMax = new Vector2(0.5f, 0.5f);
                particleRT.sizeDelta = new Vector2(8f, 8f);
                particleRT.anchoredPosition = asteroidObj.GetComponent<RectTransform>().anchoredPosition;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(60f, 180f);
                Vector2 velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);

                StartCoroutine(AnimateParticle(particle, velocity, 0.4f));
            }

            // 原陨石缩小消失
            Image asteroidImg = asteroidObj.GetComponent<Image>();
            RectTransform asteroidRT = asteroidObj.GetComponent<RectTransform>();
            Vector3 originalScale = asteroidRT.localScale;
            float elapsed = 0f;
            float duration = 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                asteroidRT.localScale = Vector3.Lerp(originalScale, Vector3.zero, t * t);
                asteroidImg.color = new Color(asteroidImg.color.r, asteroidImg.color.g, asteroidImg.color.b,
                    Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            Destroy(asteroidObj);
        }

        private IEnumerator AnimateParticle(GameObject particle, Vector2 velocity, float duration)
        {
            float elapsed = 0f;
            Image img = particle.GetComponent<Image>();
            RectTransform rt = particle.GetComponent<RectTransform>();
            Vector2 startPos = rt.anchoredPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.anchoredPosition = startPos + velocity * t;
                img.color = new Color(img.color.r, img.color.g, img.color.b,
                    Mathf.Lerp(1f, 0f, t * t));
                yield return null;
            }

            Destroy(particle);
        }

        private IEnumerator SuccessRoutine()
        {
            yield return new WaitForSeconds(SuccessDelay);
            Complete();
        }

        private IEnumerator TimeOutRoutine()
        {
            timerText.text = "时间到!";
            yield return new WaitForSeconds(0.5f);
            Cancel();
        }

        private GameObject CreatePanel(GameObject parent, string name, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent.transform);
            Image img = panel.AddComponent<Image>();
            img.color = color;
            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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