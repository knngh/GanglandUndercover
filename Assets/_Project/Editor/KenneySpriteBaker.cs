using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GanglandUndercover.Editor
{
    /// <summary>
    /// Kenney 3D FBX 资产 → 2D 俯视 Sprite 烘焙管线。
    ///
    /// 使用方式：菜单栏 Tools → Bake Kenney Sprites
    ///
    /// 管线流程：
    /// 1. 扫描 Kenney 三个 kit 的 FBX 模型
    /// 2. 在临时场景中用正交俯视相机渲染
    /// 3. 输出 256x256 PNG 到 Assets/_Project/Sprites/Kenney/
    /// 4. 自动配置 TextureImporter 为 Sprite 模式
    /// </summary>
    public class KenneySpriteBaker : EditorWindow
    {
        // ── 配置 ──────────────────────────────────────────────
        private const string SourceRoot = "Assets/_Project/Art/ThirdParty/Kenney";
        private const string OutputRoot = "Assets/_Project/Sprites/Kenney";
        private const int SpriteResolution = 256;
        private const float CameraHeight = 10f;   // 相机高于模型的距离
        private const float OrthoSize = 4f;        // 正交相机半高
        private static readonly Color ClearColor = new Color(0, 0, 0, 0); // 透明背景

        // ── Kit 定义 ──────────────────────────────────────────
        private static readonly KitDefinition[] Kits =
        {
            new KitDefinition
            {
                KitName  = "Buildings",
                FbxDir   = "CityKitCommercial/Models/FBX format",
                OutputSubDir = "Buildings",
                Filter   = m => !m.Contains("detail-") && !m.Contains("low-detail-"),
                IsCharacter = false,
            },
            new KitDefinition
            {
                KitName  = "BuildingDetails",
                FbxDir   = "CityKitCommercial/Models/FBX format",
                OutputSubDir = "Buildings/Details",
                Filter   = m => m.Contains("detail-"),
                IsCharacter = false,
            },
            new KitDefinition
            {
                KitName  = "LowPolyBldg",
                FbxDir   = "CityKitCommercial/Models/FBX format",
                OutputSubDir = "Buildings/LowPoly",
                Filter   = m => m.Contains("low-detail-"),
                IsCharacter = false,
            },
            new KitDefinition
            {
                KitName  = "Characters",
                FbxDir   = "MiniCharacters/Models/FBX format",
                OutputSubDir = "Characters",
                Filter   = m => m.StartsWith("character-"),
                IsCharacter = true,
            },
            new KitDefinition
            {
                KitName  = "Accessories",
                FbxDir   = "MiniCharacters/Models/FBX format",
                OutputSubDir = "Characters/Accessories",
                Filter   = m => !m.StartsWith("character-"),
                IsCharacter = true,
            },
            new KitDefinition
            {
                KitName  = "Roads",
                FbxDir   = "CityKitRoads/Models/FBX format",
                OutputSubDir = "Roads",
                Filter   = m => true,
                IsCharacter = false,
            },
        };

        // ── 烘焙上下文 ────────────────────────────────────────
        private struct KitDefinition
        {
            public string KitName;
            public string FbxDir;
            public string OutputSubDir;
            public System.Func<string, bool> Filter;
            public bool IsCharacter;
        }

        private int _processedCount;
        private int _errorCount;
        private readonly List<string> _log = new List<string>();

        // ══════════════════════════════════════════════════════
        // Editor Window
        // ══════════════════════════════════════════════════════

        [MenuItem("Tools/Bake Kenney Sprites")]
        public static void ShowWindow()
        {
            var window = GetWindow<KenneySpriteBaker>("Kenney Sprite Baker");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Kenney 3D → 2D Sprite 烘焙管线", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                $"扫描 {SourceRoot} 下的 FBX 模型\n" +
                $"俯视渲染 → {SpriteResolution}×{SpriteResolution} PNG\n" +
                $"输出到 {OutputRoot}",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Bake All Kits", GUILayout.Height(40)))
            {
                BakeAllKits();
            }

            EditorGUILayout.Space(5);

            if (_log.Count > 0)
            {
                EditorGUILayout.LabelField(
                    $"完成: {_processedCount} / 失败: {_errorCount}",
                    EditorStyles.boldLabel);

                EditorGUILayout.Space(5);
                foreach (var l in _log)
                    EditorGUILayout.LabelField(l, EditorStyles.wordWrappedLabel);
            }
        }

        // ══════════════════════════════════════════════════════
        // 主流程
        // ══════════════════════════════════════════════════════

        private void BakeAllKits()
        {
            _log.Clear();
            _processedCount = 0;
            _errorCount = 0;

            // 确保输出目录存在
            EnsureDirectory(OutputRoot);

            foreach (var kit in Kits)
            {
                string fbxDir = $"{SourceRoot}/{kit.FbxDir}";
                if (!Directory.Exists(fbxDir))
                {
                    _log.Add($"[SKIP] {kit.KitName}: 目录不存在 {fbxDir}");
                    continue;
                }

                string[] fbxFiles = Directory.GetFiles(fbxDir, "*.fbx");
                _log.Add($"── {kit.KitName} ({fbxFiles.Length} models) ──");

                foreach (var fbxPath in fbxFiles)
                {
                    string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
                    if (!kit.Filter(fbxName)) continue;

                    try
                    {
                        BakeSingleModel(fbxPath, kit);
                        _processedCount++;
                    }
                    catch (System.Exception e)
                    {
                        _errorCount++;
                        _log.Add($"  ✗ {fbxName}: {e.Message}");
                    }
                }
            }

            AssetDatabase.Refresh();
            _log.Add($"\n✓ 完成: {_processedCount} sprites / {_errorCount} errors");
            Repaint();
        }

        // ══════════════════════════════════════════════════════
        // 单模型烘焙
        // ══════════════════════════════════════════════════════

        private void BakeSingleModel(string fbxPath, KitDefinition kit)
        {
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
            string relPath = fbxPath;

            // 加载 FBX 模型
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(relPath);
            if (prefab == null)
            {
                _log.Add($"  ✗ {fbxName}: 无法加载 FBX");
                _errorCount++;
                return;
            }

            // 临时实例化
            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            instance.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                // 确保模型在原点和可见
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                // 计算包围盒，决定相机位置
                Bounds bounds = CalculateBounds(instance);
                Vector3 boundsCenter = bounds.center;
                Vector3 boundsSize = bounds.size;
                float maxExtent = Mathf.Max(boundsSize.x, boundsSize.z);

                // 正交俯视相机
                GameObject camObj = new GameObject("_BakerCam", typeof(Camera))
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                Camera cam = camObj.GetComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(maxExtent * 0.6f, OrthoSize);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = ClearColor;
                cam.transform.position = new Vector3(boundsCenter.x, CameraHeight, boundsCenter.z);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 俯视
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = CameraHeight + 10f;

                // RenderTexture
                RenderTexture rt = RenderTexture.GetTemporary(
                    SpriteResolution, SpriteResolution, 24,
                    RenderTextureFormat.ARGB32);
                rt.antiAliasing = 4;
                cam.targetTexture = rt;

                // 渲染
                cam.Render();

                // 读像素
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(SpriteResolution, SpriteResolution,
                    TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, SpriteResolution, SpriteResolution), 0, 0);
                tex.Apply();

                // 保存 PNG
                string outputDir = $"{OutputRoot}/{kit.OutputSubDir}";
                EnsureDirectory(outputDir);
                string pngPath = $"{outputDir}/{fbxName}.png";
                File.WriteAllBytes(pngPath, tex.EncodeToPNG());

                // 清理
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                DestroyImmediate(tex);
                DestroyImmediate(camObj);

                // 刷新 AssetDatabase 让 Unity 生成 .meta
                AssetDatabase.ImportAsset(pngPath);

                // 配置 TextureImporter
                ConfigureSpriteImport(pngPath, kit);

                _log.Add($"  ✓ {fbxName}");
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        // ══════════════════════════════════════════════════════
        // 辅助方法
        // ══════════════════════════════════════════════════════

        /// <summary>递归计算 GameObject 及其子物体的渲染包围盒</summary>
        private static Bounds CalculateBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>配置导入设置：Sprite 模式、pivot 中心、point filter</summary>
        private static void ConfigureSpriteImport(string assetPath, KitDefinition kit)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SpriteResolution;

            // 俯视角色和建筑：pivot 在中心
            importer.spritePivot = new Vector2(0.5f, 0.5f);

            // 像素风格点采样
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // 透明背景
            importer.alphaIsTransparency = true;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
