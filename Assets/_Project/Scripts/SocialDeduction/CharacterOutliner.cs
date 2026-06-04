using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 卡通描边效果 — 对角色根节点下所有 MeshRenderer 添加描边。
    /// 使用第二个略大的黑色 Mesh 拷贝（Shell 法），不依赖 Shader 第二 Pass。
    /// </summary>
    public sealed class CharacterOutliner : MonoBehaviour
    {
        private const float OutlineWidth     = 0.02f;
        private static readonly Color OutlineColor = HexColor("#1a1a2e");

        private readonly List<GameObject> outlineShells = new List<GameObject>();

        /// <summary>
        /// 为目标角色生成所有描边壳。传入角色根节点。
        /// </summary>
        public void BuildOutlines(GameObject characterRoot)
        {
            ClearOutlines();

            MeshRenderer[] renderers = characterRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer source in renderers)
            {
                MeshFilter meshFilter = source.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                GameObject shell = new GameObject(source.name + " Outline");
                shell.transform.SetParent(source.transform, false);
                shell.transform.localPosition = Vector3.zero;
                shell.transform.localRotation = Quaternion.identity;

                // 缩放：略大于原始模型
                Vector3 sourceScale = source.transform.lossyScale;
                float avgScale = (sourceScale.x + sourceScale.y + sourceScale.z) / 3f;
                float scaleFactor = 1f + OutlineWidth / Mathf.Max(avgScale, 0.001f);
                shell.transform.localScale = Vector3.one * scaleFactor;

                MeshFilter shellFilter = shell.AddComponent<MeshFilter>();
                shellFilter.sharedMesh = meshFilter.sharedMesh;

                MeshRenderer shellRenderer = shell.AddComponent<MeshRenderer>();
                shellRenderer.sharedMaterial = CreateOutlineMaterial();

                outlineShells.Add(shell);
            }
        }

        /// <summary>
        /// 清除所有描边壳。
        /// </summary>
        public void ClearOutlines()
        {
            foreach (GameObject shell in outlineShells)
            {
                if (shell != null) Destroy(shell);
            }
            outlineShells.Clear();
        }

        private void OnDestroy()
        {
            ClearOutlines();
        }

        private static Material CreateOutlineMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");

            Material mat = new Material(shader);
            mat.color = OutlineColor;
            // 翻转面法线效果：通过修改顶点法线在 shader 中实现背面渲染
            // 简化方案：直接使用普通着色，通过位置偏移模拟描边
            return mat;
        }

        private static Color HexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                return color;
            return Color.black;
        }
    }
}