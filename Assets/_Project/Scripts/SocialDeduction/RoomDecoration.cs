using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 程序化房间装饰生成器。
    /// 在 BuildWorld 阶段调用，为每个房间生成墙壁海报/通缉令、货柜堆叠（随机旋转偏移）、办公桌+椅子组合。
    /// 所有生成对象注册到 generatedObjects 以便 ClearWorld 统一清理。
    /// </summary>
    public sealed class RoomDecoration : MonoBehaviour
    {
        // ─── 配置 ────────────────────────────────────

        [Header("Poster / Wanted Sign")]
        [Tooltip("海报 Quad 尺寸")]
        public Vector2 PosterSize = new Vector2(0.52f, 0.38f);

        [Tooltip("海报颜色列表（纯色模拟）")]
        public Color[] PosterColors = new Color[]
        {
            new Color(0.72f, 0.18f, 0.14f, 1f), // 红色通缉令
            new Color(0.14f, 0.28f, 0.62f, 1f), // 蓝色公告
            new Color(0.82f, 0.72f, 0.18f, 1f), // 黄色警示
            new Color(0.18f, 0.62f, 0.32f, 1f), // 绿色通知
        };

        [Header("Container Stacking")]
        [Tooltip("货柜堆叠随机旋转偏移（度）")]
        public float ContainerRotationJitter = 4f;

        [Tooltip("货柜堆叠位置随机偏移")]
        public float ContainerPositionJitter = 0.08f;

        [Header("Desk + Chair")]
        [Tooltip("办公桌尺寸")]
        public Vector3 DeskSize = new Vector3(0.62f, 0.38f, 0.42f);

        [Tooltip("椅子尺寸")]
        public Vector3 ChairSize = new Vector3(0.22f, 0.22f, 0.38f);

        // ─── 公开接口 ─────────────────────────────────

        /// <summary>
        /// 为指定房间生成装饰。
        /// </summary>
        /// <param name="roomName">房间名（用于日志）</param>
        /// <param name="roomCenter">房间中心世界坐标</param>
        /// <param name="roomSize">房间尺寸（x, y）</param>
        /// <param name="roomColor">房间主色调（用于协调装饰色）</param>
        /// <param name="parent">装饰对象的父节点</param>
        /// <param name="generatedObjects">注册表（用于统一清理）</param>
        public void DecorateRoom(
            string roomName,
            Vector3 roomCenter,
            Vector2 roomSize,
            Color roomColor,
            Transform parent,
            List<GameObject> generatedObjects)
        {
            // 墙壁海报/通缉令
            PlaceWallPosters(roomName, roomCenter, roomSize, parent, generatedObjects);

            // 根据房间类型生成特定装饰
            string lowerName = roomName.ToLower();

            if (lowerName.Contains("货柜") || lowerName.Contains("dock") || lowerName.Contains("warehouse"))
            {
                PlaceContainerStack(roomCenter, roomSize, parent, generatedObjects);
            }

            if (lowerName.Contains("办公室") || lowerName.Contains("office") || lowerName.Contains("专案"))
            {
                PlaceDeskChairCombo(roomCenter, roomSize, parent, generatedObjects);
            }

            if (lowerName.Contains("诊所") || lowerName.Contains("clinic"))
            {
                PlaceClinicShelves(roomCenter, roomSize, parent, generatedObjects);
            }
        }

        // ─── 墙壁海报/通缉令 ──────────────────────────

        private void PlaceWallPosters(string roomName, Vector3 center, Vector2 size,
            Transform parent, List<GameObject> generatedObjects)
        {
            // 在房间北墙和南墙各放 1-2 张海报
            int posterCount = Random.Range(1, 3); // 1~2 张

            for (int i = 0; i < posterCount; i++)
            {
                // 随机选择北墙或南墙
                bool northWall = Random.value > 0.5f;
                float wallY = northWall ? center.y + size.y * 0.42f : center.y - size.y * 0.42f;
                float wallX = center.x + Random.Range(-size.x * 0.32f, size.x * 0.32f);

                GameObject poster = GameObject.CreatePrimitive(PrimitiveType.Quad);
                poster.name = $"{roomName}_Poster_{i}";
                generatedObjects.Add(poster);
                poster.transform.SetParent(parent, false);
                poster.transform.position = new Vector3(wallX, wallY, -0.68f);
                poster.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                poster.transform.localScale = new Vector3(PosterSize.x, PosterSize.y, 1f);

                // 纯色 Quad 模拟海报
                Color posterColor = PosterColors[Random.Range(0, PosterColors.Length)];
                SetColor(poster, posterColor);

                // 海报边框
                CreatePosterBorder(poster.transform, generatedObjects);
            }
        }

        private void CreatePosterBorder(Transform posterTransform, List<GameObject> generatedObjects)
        {
            GameObject border = GameObject.CreatePrimitive(PrimitiveType.Quad);
            border.name = posterTransform.gameObject.name + "_Border";
            generatedObjects.Add(border);
            border.transform.SetParent(posterTransform, false);
            border.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            border.transform.localRotation = Quaternion.identity;
            border.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
            SetColor(border, new Color(0.08f, 0.08f, 0.06f, 1f));
        }

        // ─── 货柜堆叠 ────────────────────────────────

        private void PlaceContainerStack(Vector3 center, Vector2 roomSize,
            Transform parent, List<GameObject> generatedObjects)
        {
            int stackCount = Random.Range(2, 5); // 2~4 个货柜

            for (int i = 0; i < stackCount; i++)
            {
                Vector3 basePos = new Vector3(
                    center.x + Random.Range(-roomSize.x * 0.28f, roomSize.x * 0.28f),
                    center.y + Random.Range(-roomSize.y * 0.28f, roomSize.y * 0.28f),
                    -0.42f - i * 0.38f);

                GameObject container = GameObject.CreatePrimitive(PrimitiveType.Cube);
                container.name = $"Container_{i}";
                generatedObjects.Add(container);
                container.transform.SetParent(parent, false);
                container.transform.position = basePos + new Vector3(
                    Random.Range(-ContainerPositionJitter, ContainerPositionJitter),
                    Random.Range(-ContainerPositionJitter, ContainerPositionJitter),
                    0f);
                container.transform.localScale = new Vector3(0.82f, 0.38f, 0.32f);
                container.transform.rotation = Quaternion.Euler(0f, 0f,
                    Random.Range(-ContainerRotationJitter, ContainerRotationJitter));

                // 货柜颜色：蓝/红/绿随机
                Color[] containerColors = new Color[]
                {
                    new Color(0.08f, 0.22f, 0.46f, 1f),
                    new Color(0.52f, 0.12f, 0.08f, 1f),
                    new Color(0.08f, 0.36f, 0.2f, 1f),
                };
                SetColor(container, containerColors[Random.Range(0, containerColors.Length)]);

                // 货柜条纹装饰
                CreateContainerStripe(container.transform, generatedObjects);
            }
        }

        private void CreateContainerStripe(Transform containerTransform, List<GameObject> generatedObjects)
        {
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = containerTransform.gameObject.name + "_Stripe";
            generatedObjects.Add(stripe);
            stripe.transform.SetParent(containerTransform, false);
            stripe.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            stripe.transform.localScale = new Vector3(0.92f, 0.08f, 0.08f);
            SetColor(stripe, new Color(0.86f, 0.82f, 0.62f, 1f));
        }

        // ─── 办公桌 + 椅子 ────────────────────────────

        private void PlaceDeskChairCombo(Vector3 center, Vector2 roomSize,
            Transform parent, List<GameObject> generatedObjects)
        {
            int deskCount = Random.Range(1, 3); // 1~2 组

            for (int i = 0; i < deskCount; i++)
            {
                Vector3 deskPos = new Vector3(
                    center.x + Random.Range(-roomSize.x * 0.32f, roomSize.x * 0.32f),
                    center.y + Random.Range(-roomSize.y * 0.32f, roomSize.y * 0.32f),
                    -0.18f);

                // 办公桌
                GameObject desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                desk.name = $"Desk_{i}";
                generatedObjects.Add(desk);
                desk.transform.SetParent(parent, false);
                desk.transform.position = deskPos;
                desk.transform.localScale = DeskSize;
                SetColor(desk, new Color(0.22f, 0.18f, 0.12f, 1f));

                // 桌面显示器（小 Quad）
                GameObject monitor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                monitor.name = $"Desk_{i}_Monitor";
                generatedObjects.Add(monitor);
                monitor.transform.SetParent(desk.transform, false);
                monitor.transform.localPosition = new Vector3(0f, 0.22f, -0.58f);
                monitor.transform.localScale = new Vector3(0.42f, 0.08f, 0.32f);
                SetColor(monitor, new Color(0.06f, 0.06f, 0.08f, 1f));

                // 椅子（放在桌子前方）
                Vector3 chairPos = new Vector3(deskPos.x, deskPos.y - 0.42f, -0.08f);
                GameObject chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chair.name = $"Chair_{i}";
                generatedObjects.Add(chair);
                chair.transform.SetParent(parent, false);
                chair.transform.position = chairPos;
                chair.transform.localScale = ChairSize;
                SetColor(chair, new Color(0.12f, 0.12f, 0.1f, 1f));
            }
        }

        // ─── 诊所货架 ────────────────────────────────

        private void PlaceClinicShelves(Vector3 center, Vector2 roomSize,
            Transform parent, List<GameObject> generatedObjects)
        {
            int shelfCount = Random.Range(1, 3);

            for (int i = 0; i < shelfCount; i++)
            {
                Vector3 shelfPos = new Vector3(
                    center.x + Random.Range(-roomSize.x * 0.28f, roomSize.x * 0.28f),
                    center.y + Random.Range(-roomSize.y * 0.28f, roomSize.y * 0.28f),
                    -0.18f);

                GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shelf.name = $"ClinicShelf_{i}";
                generatedObjects.Add(shelf);
                shelf.transform.SetParent(parent, false);
                shelf.transform.position = shelfPos;
                shelf.transform.localScale = new Vector3(0.38f, 0.52f, 0.28f);
                SetColor(shelf, new Color(0.18f, 0.22f, 0.16f, 1f));

                // 药瓶（小方块）
                for (int j = 0; j < 3; j++)
                {
                    GameObject bottle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bottle.name = $"ClinicShelf_{i}_Bottle_{j}";
                    generatedObjects.Add(bottle);
                    bottle.transform.SetParent(shelf.transform, false);
                    bottle.transform.localPosition = new Vector3(
                        Random.Range(-0.08f, 0.08f),
                        -0.12f + j * 0.12f,
                        -0.42f);
                    bottle.transform.localScale = new Vector3(0.06f, 0.06f, 0.12f);
                    SetColor(bottle, new Color(0.72f, 0.82f, 0.72f, 1f));
                }
            }
        }

        // ─── 工具方法 ────────────────────────────────

        private static void SetColor(GameObject target, Color color)
        {
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");

            Material mat = new Material(shader) { color = color };
            renderer.sharedMaterial = mat;
        }
    }
}
