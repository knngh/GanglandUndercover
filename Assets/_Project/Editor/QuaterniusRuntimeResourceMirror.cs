using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    public static class QuaterniusRuntimeResourceMirror
    {
        private const string SourceRootAssetPath = "Assets/_Project/Art/ThirdParty/Quaternius/ModularSciFiMegaKit/";
        private const string RuntimeRootAssetPath = "Assets/_Project/Resources/Quaternius/ModularSciFiMegaKit/";

        [MenuItem("Gangland/Sync Quaternius Runtime Resources")]
        public static void SyncMenuItem()
        {
            SyncRuntimeResources();
        }

        public static void SyncRuntimeResources()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourceRoot = EnsureTrailingSeparator(Path.GetFullPath(Path.Combine(projectRoot, SourceRootAssetPath)));
            string runtimeRoot = EnsureTrailingSeparator(Path.GetFullPath(Path.Combine(projectRoot, RuntimeRootAssetPath)));
            string sourceRootAsset = NormalizeAssetPath(SourceRootAssetPath);
            string runtimeRootAsset = NormalizeAssetPath(RuntimeRootAssetPath);

            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogWarning("Quaternius runtime resource mirror skipped: missing source folder " + SourceRootAssetPath);
                return;
            }

            Directory.CreateDirectory(runtimeRoot);

            int copied = 0;
            int skipped = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string sourceFolder in new[] { "FBX", "Textures" })
                {
                    string absoluteFolder = Path.Combine(sourceRoot, sourceFolder);
                    if (!Directory.Exists(absoluteFolder))
                    {
                        continue;
                    }

                    foreach (string sourceFile in Directory.GetFiles(absoluteFolder, "*.*", SearchOption.AllDirectories))
                    {
                        if (sourceFile.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string extension = Path.GetExtension(sourceFile);
                        if (!IsSupportedAssetExtension(extension))
                        {
                            continue;
                        }

                        string relativePath = sourceFile.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        string sourceAssetPath = NormalizeAssetPath(Path.Combine(sourceRootAsset, relativePath));
                        string destinationAssetPath = NormalizeAssetPath(Path.Combine(runtimeRootAsset, relativePath));
                        string destinationFile = Path.Combine(runtimeRoot, relativePath);

                        if (!ShouldCopy(sourceFile, destinationFile))
                        {
                            skipped++;
                            continue;
                        }

                        string destinationDirectory = Path.GetDirectoryName(destinationFile);
                        if (!string.IsNullOrEmpty(destinationDirectory))
                        {
                            Directory.CreateDirectory(destinationDirectory);
                        }

                        if (AssetDatabase.AssetPathToGUID(destinationAssetPath) != string.Empty)
                        {
                            AssetDatabase.DeleteAsset(destinationAssetPath);
                        }

                        if (!AssetDatabase.CopyAsset(sourceAssetPath, destinationAssetPath))
                        {
                            File.Copy(sourceFile, destinationFile, true);
                        }

                        copied++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Quaternius runtime resources synced: copied " + copied + ", skipped " + skipped + " into " + RuntimeRootAssetPath);
        }

        private static bool IsSupportedAssetExtension(string extension)
        {
            return extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tga", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".exr", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldCopy(string sourceFile, string destinationFile)
        {
            if (!File.Exists(destinationFile))
            {
                return true;
            }

            FileInfo sourceInfo = new FileInfo(sourceFile);
            FileInfo destinationInfo = new FileInfo(destinationFile);
            return sourceInfo.Length != destinationInfo.Length || sourceInfo.LastWriteTimeUtc > destinationInfo.LastWriteTimeUtc.AddMilliseconds(1);
        }

        private static string NormalizeAssetPath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
