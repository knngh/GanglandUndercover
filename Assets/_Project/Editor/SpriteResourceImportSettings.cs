using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    /// <summary>
    /// Keeps runtime 2D Resources sprites importable as Unity Sprite assets.
    /// Scope is intentionally limited to Assets/_Project/Resources/Sprites.
    /// </summary>
    public static class SpriteResourceImportSettings
    {
        public const string SpriteResourcesRoot = "Assets/_Project/Resources/Sprites";
        public const string AiReviewedBackgroundRoot = SpriteResourcesRoot + "/AIReviewed/Backgrounds";
        public const string AiReviewedMapPreviewRoot = SpriteResourcesRoot + "/AIReviewed/MapPreviews";

        [MenuItem("Gangland/Art/Validate Resource Sprite Imports")]
        public static void ValidateResourceSpriteImports()
        {
            List<string> invalid = FindMisconfiguredSpritePngs();
            if (invalid.Count == 0)
            {
                Debug.Log("[Gangland] Resource sprite imports are configured.");
                return;
            }

            Debug.LogError("[Gangland] Misconfigured resource sprite imports:\n" + string.Join("\n", invalid));
        }

        [MenuItem("Gangland/Art/Fix Resource Sprite Imports")]
        public static void FixResourceSpriteImports()
        {
            int changed = ConfigureAllSpritePngs();
            Debug.Log("[Gangland] Resource sprite import settings updated: " + changed);
        }

        public static List<string> GetSpritePngAssetPaths()
        {
            List<string> paths = new List<string>();
            if (!Directory.Exists(SpriteResourcesRoot))
            {
                return paths;
            }

            foreach (string path in Directory.GetFiles(SpriteResourcesRoot, "*.png", SearchOption.AllDirectories))
            {
                paths.Add(path.Replace(Path.DirectorySeparatorChar, '/'));
            }

            paths.Sort(System.StringComparer.Ordinal);
            return paths;
        }

        public static List<string> FindMisconfiguredSpritePngs()
        {
            List<string> invalid = new List<string>();
            foreach (string path in GetSpritePngAssetPaths())
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || !IsConfigured(importer, path))
                {
                    invalid.Add(path);
                }
            }

            return invalid;
        }

        public static int ConfigureAllSpritePngs()
        {
            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in GetSpritePngAssetPaths())
                {
                    if (ConfigureSpritePng(path))
                    {
                        changed++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return changed;
        }

        public static bool ConfigureSpritePng(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            FilterMode expectedFilterMode = FilterModeFor(assetPath);
            if (importer.filterMode != expectedFilterMode)
            {
                importer.filterMode = expectedFilterMode;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            return changed;
        }

        public static bool IsConfigured(TextureImporter importer)
        {
            return IsConfigured(importer, null);
        }

        public static bool IsConfigured(TextureImporter importer, string assetPath)
        {
            return importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && !importer.mipmapEnabled
                && importer.filterMode == FilterModeFor(assetPath)
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.alphaIsTransparency
                && importer.npotScale == TextureImporterNPOTScale.None;
        }

        public static FilterMode FilterModeFor(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath))
            {
                string normalizedPath = assetPath.Replace(Path.DirectorySeparatorChar, '/');
                if (normalizedPath.StartsWith(AiReviewedBackgroundRoot + "/", System.StringComparison.Ordinal)
                    || normalizedPath.StartsWith(AiReviewedMapPreviewRoot + "/", System.StringComparison.Ordinal))
                {
                    return FilterMode.Bilinear;
                }
            }

            return FilterMode.Point;
        }
    }
}
