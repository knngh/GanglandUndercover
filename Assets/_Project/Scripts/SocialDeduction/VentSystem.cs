using System;
using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 通风管节点：地图上的一个通风管入口/出口位置。
    /// </summary>
    [Serializable]
    public sealed class VentNode
    {
        public string Name;
        public Vector3 Position;
        public List<int> ConnectedNodeIndices = new List<int>();

        public VentNode() { }

        public VentNode(string name, Vector3 position, params int[] connected)
        {
            Name = name;
            Position = position;
            ConnectedNodeIndices = new List<int>(connected);
        }
    }

    /// <summary>
    /// 通风管系统：管理所有通风管节点，处理进出通风管逻辑。
    /// Among Us 核心机制：Impostor 通过通风管在地图两点间瞬移。
    /// 仅 Gang (Impostor) 阵营可用。
    /// </summary>
    public sealed class VentSystem : MonoBehaviour
    {
        [SerializeField] private List<VentNode> nodes = new List<VentNode>();
        [SerializeField] private float ventRange = 0.9f;
        [SerializeField] private float ventCooldown = 10f;
        [SerializeField] private float transitionDuration = 0.5f;

        private int? currentVentIndex;
        private float cooldownRemaining;
        private float transitionRemaining;
        private bool inTransition;
        private int targetNodeIndex;
        private Action<Vector3> onTeleport;
        private Func<Vector3> getPlayerPosition;
        private Func<bool> isPlayerGang;
        private Func<bool> isPlayerAlive;
        private Action<float> onSetBlackoutAlpha;

        private readonly List<GameObject> ventVisuals = new List<GameObject>();
        private readonly List<Material> ventMaterials = new List<Material>();

        public IReadOnlyList<VentNode> Nodes => nodes;
        public bool IsInVent => currentVentIndex.HasValue;
        public bool IsInTransition => inTransition;
        public float CooldownRemaining => cooldownRemaining;
        public float VentRange => ventRange;
        public int? CurrentVentIndex => currentVentIndex;
        public IReadOnlyList<int> AvailableDestinations
        {
            get
            {
                if (!currentVentIndex.HasValue) return Array.Empty<int>();
                return nodes[currentVentIndex.Value].ConnectedNodeIndices;
            }
        }

        public void Bind(
            Action<Vector3> onTeleport,
            Func<Vector3> getPlayerPosition,
            Func<bool> isPlayerGang,
            Func<bool> isPlayerAlive,
            Action<float> onSetBlackoutAlpha = null)
        {
            this.onTeleport = onTeleport;
            this.getPlayerPosition = getPlayerPosition;
            this.isPlayerGang = isPlayerGang;
            this.isPlayerAlive = isPlayerAlive;
            this.onSetBlackoutAlpha = onSetBlackoutAlpha;
        }

        /// <summary>
        /// 根据外部传入的 VentNode 数据创建 3D 可视化通风管。
        /// </summary>
        public void BuildVisuals(List<VentNode> externalNodes, float floorZ)
        {
            nodes = new List<VentNode>(externalNodes);

            for (int i = 0; i < nodes.Count; i++)
            {
                VentNode node = nodes[i];
                CreateVentVisual(i, node.Position, floorZ);
            }
        }

        private void CreateVentVisual(int index, Vector3 position, float floorZ)
        {
            // 通风管底座 — 圆形格栅
            GameObject ventBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ventBase.name = "Vent Base " + index;
            ventBase.transform.position = new Vector3(position.x, position.y, floorZ + 0.06f);
            ventBase.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ventBase.transform.localScale = new Vector3(0.52f, 0.06f, 0.52f);

            MeshRenderer baseRenderer = ventBase.GetComponent<MeshRenderer>();
            if (baseRenderer != null)
            {
                Material mat = new Material(FindColorShader());
                mat.color = new Color(0.18f, 0.2f, 0.22f, 1f);
                baseRenderer.sharedMaterial = mat;
                ventMaterials.Add(mat);
            }

            ventVisuals.Add(ventBase);

            // 格栅横条
            for (int j = 0; j < 3; j++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "Vent Bar " + index + "_" + j;
                bar.transform.SetParent(ventBase.transform, false);
                bar.transform.localPosition = new Vector3(0f, -0.22f + j * 0.22f, 0.15f);
                bar.transform.localScale = new Vector3(0.85f, 0.04f, 0.24f);

                MeshRenderer barRenderer = bar.GetComponent<MeshRenderer>();
                if (barRenderer != null)
                {
                    Material mat = new Material(FindColorShader());
                    mat.color = new Color(0.28f, 0.3f, 0.32f, 1f);
                    barRenderer.sharedMaterial = mat;
                    ventMaterials.Add(mat);
                }
            }

            // 发光边框（Gang阵营可用的视觉提示）
            GameObject glowRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glowRing.name = "Vent Glow " + index;
            glowRing.transform.SetParent(ventBase.transform, false);
            glowRing.transform.localPosition = new Vector3(0f, 0f, 0.08f);
            glowRing.transform.localScale = new Vector3(1.28f, 1.8f, 1.28f);

            MeshRenderer glowRenderer = glowRing.GetComponent<MeshRenderer>();
            if (glowRenderer != null)
            {
                Material mat = new Material(FindColorShader());
                mat.color = new Color(0.72f, 0.18f, 0.12f, 0.42f);
                glowRenderer.sharedMaterial = mat;
                ventMaterials.Add(mat);
            }
        }

        /// <summary>
        /// 每帧更新：冷却计时、过度动画。
        /// </summary>
        public void Tick()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= Time.deltaTime;
            }

            if (inTransition)
            {
                transitionRemaining -= Time.deltaTime;

                // 过渡中 — 短暂黑屏
                float progress = 1f - Mathf.Clamp01(transitionRemaining / transitionDuration);
                float alpha = progress < 0.5f
                    ? Mathf.Lerp(0f, 0.65f, progress * 2f)
                    : Mathf.Lerp(0.65f, 0f, (progress - 0.5f) * 2f);

                onSetBlackoutAlpha?.Invoke(alpha);

                // 在半程时执行瞬移
                if (transitionRemaining <= transitionDuration * 0.5f && targetNodeIndex >= 0)
                {
                    ExecuteTeleport(targetNodeIndex);
                    targetNodeIndex = -1;
                }

                if (transitionRemaining <= 0f)
                {
                    inTransition = false;
                    onSetBlackoutAlpha?.Invoke(0f);
                }
            }

            // 更新通风管可视颜色（冷却中 vs 就绪）
            RefreshVentColors();
        }

        /// <summary>
        /// 尝试进入最近的通风管。返回 true 表示成功进入。
        /// </summary>
        public bool TryEnterVent()
        {
            if (!isPlayerGang?.Invoke() ?? true) return false;
            if (!isPlayerAlive?.Invoke() ?? false) return false;
            if (cooldownRemaining > 0f) return false;
            if (inTransition) return false;
            if (IsInVent) return false;

            Vector3 playerPos = getPlayerPosition?.Invoke() ?? Vector3.zero;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (Vector2.Distance(
                    new Vector2(playerPos.x, playerPos.y),
                    new Vector2(nodes[i].Position.x, nodes[i].Position.y)) <= ventRange)
                {
                    currentVentIndex = i;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 从当前通风管瞬移到目标节点。
        /// </summary>
        public void TravelTo(int targetIndex)
        {
            if (!currentVentIndex.HasValue) return;
            if (!nodes[currentVentIndex.Value].ConnectedNodeIndices.Contains(targetIndex)) return;
            if (targetIndex == currentVentIndex.Value) return;

            targetNodeIndex = targetIndex;
            inTransition = true;
            transitionRemaining = transitionDuration;
            currentVentIndex = null;
            cooldownRemaining = ventCooldown;
        }

        /// <summary>
        /// 从通风管中退出（不瞬移）。
        /// </summary>
        public void ExitVent()
        {
            currentVentIndex = null;
            targetNodeIndex = -1;
            inTransition = false;
            transitionRemaining = 0f;
            onSetBlackoutAlpha?.Invoke(0f);
        }

        /// <summary>
        /// 撤离通风管后开始冷却。
        /// </summary>
        public void StartCooldown()
        {
            cooldownRemaining = ventCooldown;
        }

        /// <summary>
        /// 检测玩家是否在任意通风管附近。
        /// </summary>
        public int? GetNearestVentIndex()
        {
            Vector3 playerPos = getPlayerPosition?.Invoke() ?? Vector3.zero;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (Vector2.Distance(
                    new Vector2(playerPos.x, playerPos.y),
                    new Vector2(nodes[i].Position.x, nodes[i].Position.y)) <= ventRange)
                {
                    return i;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取指定通风管节点的名称。
        /// </summary>
        public string GetNodeName(int index)
        {
            if (index < 0 || index >= nodes.Count) return "未知通风管";
            return nodes[index].Name;
        }

        /// <summary>
        /// 清除所有可视化对象。
        /// </summary>
        public void ClearVisuals()
        {
            foreach (GameObject obj in ventVisuals)
            {
                if (obj != null)
                {
                    if (Application.isPlaying)
                        Destroy(obj);
                    else
                        DestroyImmediate(obj);
                }
            }

            foreach (Material mat in ventMaterials)
            {
                if (mat != null)
                {
                    if (Application.isPlaying)
                        Destroy(mat);
                    else
                        DestroyImmediate(mat);
                }
            }

            ventVisuals.Clear();
            ventMaterials.Clear();
        }

        private void ExecuteTeleport(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= nodes.Count) return;

            Vector3 destination = nodes[nodeIndex].Position;
            currentVentIndex = nodeIndex;

            // 瞬移到目标通风管出口
            onTeleport?.Invoke(destination);
        }

        private void RefreshVentColors()
        {
            for (int i = 0; i < ventVisuals.Count && i < nodes.Count; i++)
            {
                GameObject obj = ventVisuals[i];
                if (obj == null) continue;

                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color"))
                    {
                        Color current = renderer.sharedMaterial.color;
                        if (cooldownRemaining > 0f)
                        {
                            renderer.sharedMaterial.color = new Color(0.25f, 0.25f, 0.28f, current.a);
                        }
                        else
                        {
                            // 保持原始颜色，但发光环恢复
                        }
                    }
                }
            }
        }

        private static Shader FindColorShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private void OnDestroy()
        {
            ClearVisuals();
        }
    }
}