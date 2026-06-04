using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 监控摄像头系统：4 路摄像头 + 监控站，支持锥形检测 / Impostor 红灯 / 视角切换。
    /// Among Us 风格：玩家走到监控站，按 E 循环查看各摄像头画面。
    /// </summary>
    public sealed class SecurityCamera : MonoBehaviour
    {
        [Header("Camera Nodes + Visuals")]
        private readonly List<CameraNode> nodes = new List<CameraNode>();
        private readonly List<GameObject> cameraModels = new List<GameObject>();
        private readonly List<GameObject> indicatorLights = new List<GameObject>();

        [Header("Monitor Station")]
        private GameObject monitorStation;
        private int activeViewIndex = -1;
        private bool isViewing;

        // ─── 配置常量 ────────────────────────────────

        private const float DetectionRange = 3.5f;
        private const float DetectionHalfAngle = 55f; // 锥形半角 55°，总 FOV 110°
        private const float FloorZ = 0.08f;
        private const float MonitorInteractRange = 1.3f;

        // ─── 外部绑定 ─────────────────────────────────

        private SocialPrototypeController controller;
        private List<GameObject> generatedObjects;

        /// <summary>
        /// 摄像头节点定义。
        /// </summary>
        public struct CameraNode
        {
            public string Name;
            public Vector3 Position;
            public Vector3 LookDirection; // 归一化后的朝向
            public Transform ScreenModel;
            public Transform IndicatorLight;
        }

        // ─── 公共属性 ─────────────────────────────────

        public bool IsViewing => isViewing;
        public int ActiveViewIndex => activeViewIndex;
        public Vector3 MonitorStationPosition =>
            monitorStation != null ? monitorStation.transform.position : Vector3.zero;

        public IReadOnlyList<CameraNode> Nodes => nodes;

        // ─── 初始化 ────────────────────────────────────

        public void Initialize(SocialPrototypeController owner, List<GameObject> genObjects)
        {
            controller = owner;
            generatedObjects = genObjects;

            CreateCameraNodes();
            CreateMonitorStation();
        }

        private void CreateCameraNodes()
        {
            // 4 个摄像头，按区域放置（朝向区域中心或走廊）
            var configs = new List<(string name, Vector3 pos, Vector3 lookDir)>
            {
                // 货柜码头 — 朝右下，望货柜区
                ("码头监控", new Vector3(-4.05f, 1.55f, 0f), new Vector3(0.9f, -0.45f, 0f).normalized),
                // 夜市巷 — 朝左下，望夜市
                ("夜市监控", new Vector3(0.65f, 3.05f, 0f), new Vector3(-0.55f, -0.85f, 0f).normalized),
                // 专案办公室 — 朝左，望办公区
                ("办公室监控", new Vector3(4.55f, 1.5f, 0f), new Vector3(-0.95f, -0.3f, 0f).normalized),
                // 主街/竖巷交汇 — 朝下，监视走廊
                ("走廊监控", new Vector3(-0.55f, 0.85f, 0f), new Vector3(0f, -1f, 0f).normalized),
            };

            foreach (var cfg in configs)
            {
                CreateCameraNode(cfg.name, cfg.pos, cfg.lookDir);
            }
        }

        private void CreateCameraNode(string name, Vector3 position, Vector3 lookDir)
        {
            // ─── 屏幕模型（SM_Gen_Prop_Screen_01） ───
            GameObject propPrefab = Resources.Load<GameObject>(
                "AssetStore/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Screen_01");
            GameObject model;

            if (propPrefab != null)
            {
                model = Instantiate(propPrefab);
                model.name = name + " CameraBody";
                model.transform.position = new Vector3(position.x, position.y, FloorZ - 0.22f);
                model.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                model.transform.localScale = Vector3.one * 0.65f;
            }
            else
            {
                // 回退：Cube 屏幕
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.name = name + " CameraBody";
                model.transform.position = new Vector3(position.x, position.y, FloorZ - 0.22f);
                model.transform.localScale = new Vector3(0.35f, 0.08f, 0.22f);
                SetCubeColor(model, new Color(0.05f, 0.08f, 0.12f, 1f));
            }

            generatedObjects.Add(model);
            cameraModels.Add(model);

            // ─── 支架（Cylinder） ───
            GameObject mount = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mount.name = name + " Mount";
            mount.transform.SetParent(model.transform, false);
            mount.transform.localPosition = new Vector3(0f, -0.35f, -0.42f);
            mount.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            mount.transform.localScale = new Vector3(0.2f, 0.22f, 0.2f);
            SetCubeColor(mount, new Color(0.15f, 0.15f, 0.15f, 1f));
            generatedObjects.Add(mount);

            // ─── 指示灯球（绿=正常，红=警报） ───
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = name + " Indicator";
            indicator.transform.SetParent(model.transform, false);
            indicator.transform.localPosition = new Vector3(0.22f, 0f, -0.62f);
            indicator.transform.localScale = Vector3.one * 0.18f;
            SetCubeColor(indicator, Color.green);
            generatedObjects.Add(indicator);
            indicatorLights.Add(indicator);

            // ─── 锥形视野指示器（半透明扇形，用三个小 Cube 模拟） ───
            CreateConeVisual(name, position, lookDir);

            nodes.Add(new CameraNode
            {
                Name = name,
                Position = position,
                LookDirection = lookDir,
                ScreenModel = model.transform,
                IndicatorLight = indicator.transform,
            });
        }

        private void CreateConeVisual(string name, Vector3 origin, Vector3 lookDir)
        {
            float angleRad = DetectionHalfAngle * Mathf.Deg2Rad;
            float leftAngle = Mathf.Atan2(lookDir.y, lookDir.x) - angleRad;
            float rightAngle = Mathf.Atan2(lookDir.y, lookDir.x) + angleRad;

            // 三条线：左边界、中心线、右边界
            CreateConeLine(name + " ConeLeft", origin, leftAngle, DetectionRange);
            CreateConeLine(name + " ConeCenter", origin, Mathf.Atan2(lookDir.y, lookDir.x), DetectionRange);
            CreateConeLine(name + " ConeRight", origin, rightAngle, DetectionRange);
        }

        private void CreateConeLine(string objName, Vector3 origin, float angle, float length)
        {
            Vector3 end = origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * length * 0.55f;
            Vector3 mid = (origin + end) * 0.5f;
            float dist = Vector3.Distance(origin, end);

            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = objName;
            line.transform.position = new Vector3(mid.x, mid.y, FloorZ - 0.08f);
            line.transform.localScale = new Vector3(dist, 0.02f, 0.02f);
            line.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            SetCubeColor(line, new Color(0f, 0.85f, 0.95f, 0.13f));
            generatedObjects.Add(line);
        }

        private void CreateMonitorStation()
        {
            Vector3 stationPos = new Vector3(0.55f, -1.55f, 0f);

            // ─── 监控台底座 ───
            monitorStation = new GameObject("Security Monitor Station");
            monitorStation.transform.position = stationPos;
            generatedObjects.Add(monitorStation);

            GameObject desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "Monitor Desk";
            desk.transform.SetParent(monitorStation.transform, false);
            desk.transform.localPosition = new Vector3(0f, 0f, FloorZ - 0.08f);
            desk.transform.localScale = new Vector3(0.95f, 0.52f, 0.22f);
            SetCubeColor(desk, new Color(0.18f, 0.16f, 0.24f, 1f));
            generatedObjects.Add(desk);

            // ─── 监控屏幕组（3 个 Screen 模型） ───
            for (int i = 0; i < 3; i++)
            {
                GameObject screenPrefab = Resources.Load<GameObject>(
                    "AssetStore/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Screen_01");
                GameObject screen;

                if (screenPrefab != null)
                {
                    screen = Instantiate(screenPrefab);
                    screen.name = "MonitorScreen_" + i;
                    screen.transform.SetParent(monitorStation.transform, false);
                    screen.transform.localPosition = new Vector3(-0.28f + i * 0.28f, 0f, -0.42f);
                    screen.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    screen.transform.localScale = Vector3.one * 0.32f;
                }
                else
                {
                    screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    screen.name = "MonitorScreen_" + i;
                    screen.transform.SetParent(monitorStation.transform, false);
                    screen.transform.localPosition = new Vector3(-0.28f + i * 0.28f, 0f, -0.42f);
                    screen.transform.localScale = new Vector3(0.2f, 0.04f, 0.14f);
                    SetCubeColor(screen, new Color(0.02f, 0.2f, 0.35f, 1f));
                }

                generatedObjects.Add(screen);
            }

            // ─── 标签 ───
            GameObject labelObj = new GameObject("Monitor Label");
            labelObj.transform.SetParent(monitorStation.transform, false);
            labelObj.transform.localPosition = new Vector3(0f, 0.35f, -0.55f);
            labelObj.transform.localRotation = Quaternion.Euler(58f, 0f, 0f);

            TextMesh label = labelObj.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.085f;
            label.fontSize = 48;
            label.color = new Color(0.55f, 0.82f, 0.92f, 1f);
            label.text = "监控站\nE 查看";
            generatedObjects.Add(labelObj);
        }

        // ─── 每帧更新 ─────────────────────────────────

        /// <summary>检测器：对每个摄像头检查角色是否在锥形视野内。</summary>
        public void TickDetection(List<SocialCharacter> characters)
        {
            if (nodes.Count == 0) return;

            // 收集所有 Gang / Undercover 角色位置
            var targets = characters
                .Where(c => c.IsAlive && c.Role != SocialRole.Police)
                .Select(c => c.transform.position)
                .ToList();

            for (int i = 0; i < nodes.Count; i++)
            {
                CameraNode node = nodes[i];
                bool impostorDetected = false;

                foreach (Vector3 targetPos in targets)
                {
                    Vector3 toTarget = new Vector3(
                        targetPos.x - node.Position.x,
                        targetPos.y - node.Position.y,
                        0f);

                    float dist = toTarget.magnitude;
                    if (dist > DetectionRange) continue;

                    float angle = Vector3.Angle(node.LookDirection, toTarget.normalized);
                    if (angle <= DetectionHalfAngle)
                    {
                        impostorDetected = true;
                        break;
                    }
                }

                // 更新指示灯颜色
                if (i < indicatorLights.Count && indicatorLights[i] != null)
                {
                    SetCubeColor(indicatorLights[i], impostorDetected ? Color.red : Color.green);
                }
            }
        }

        // ─── 监控站交互 ────────────────────────────────

        /// <summary>检查玩家是否在监控站交互范围。</summary>
        public bool IsPlayerNearMonitor(Vector3 playerPosition)
        {
            return Vector3.Distance(playerPosition, MonitorStationPosition) <= MonitorInteractRange;
        }

        /// <summary>激活监控查看模式，返回当前摄像头名称。</summary>
        public string ActivateViewing()
        {
            isViewing = true;
            activeViewIndex = 0;
            return GetViewDescription();
        }

        /// <summary>退出监控查看。</summary>
        public void DeactivateViewing()
        {
            isViewing = false;
            activeViewIndex = -1;
        }

        /// <summary>切换到下一个摄像头视角。如果超出范围则退出。</summary>
        public string CycleNextView()
        {
            if (!isViewing || nodes.Count == 0)
            {
                ActivateViewing();
                return GetViewDescription();
            }

            activeViewIndex = (activeViewIndex + 1) % (nodes.Count + 1); // +1 预留退出

            if (activeViewIndex >= nodes.Count)
            {
                // 最后一个 = 退出
                DeactivateViewing();
                return "退出监控查看。";
            }

            return GetViewDescription();
        }

        private string GetViewDescription()
        {
            if (activeViewIndex < 0 || activeViewIndex >= nodes.Count)
                return "无可用摄像头。";

            CameraNode node = nodes[activeViewIndex];
            bool impostorNear = false;

            // 检查该摄像头视野内是否有可疑角色
            if (controller != null)
            {
                var characters = controller.Characters;
                impostorNear = characters
                    .Where(c => c.IsAlive && c.Role != SocialRole.Police)
                    .Any(c =>
                    {
                        Vector3 pos = c.transform.position;
                        Vector3 toTarget = new Vector3(pos.x - node.Position.x, pos.y - node.Position.y, 0f);
                        float dist = toTarget.magnitude;
                        if (dist > DetectionRange) return false;
                        float angle = Vector3.Angle(node.LookDirection, toTarget.normalized);
                        return angle <= DetectionHalfAngle;
                    });
            }

            string status = impostorNear
                ? " 🔴 可疑活动"
                : " ● 正常";

            return node.Name + " | " + status
                + " | (V 切换镜头 / E 退出)";
        }

        // ─── 辅助 ─────────────────────────────────────

        private static void SetCubeColor(GameObject obj, Color color)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            Material mat = new Material(shader);
            mat.color = color;
            renderer.sharedMaterial = mat;
        }

        public void Cleanup()
        {
            nodes.Clear();
            cameraModels.Clear();
            indicatorLights.Clear();
            monitorStation = null;
            isViewing = false;
            activeViewIndex = -1;
        }
    }
}