using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    /// <summary>
    /// M10.1 构建与分发 — macOS/Windows 双平台构建脚本。
    /// 可通过命令行调用：Unity -quit -batchmode -executeMethod BuildScript.Build
    /// </summary>
    public static class BuildScript
    {
        // ─── 配置常量 ───────────────────────────────────────
        private const string ExecutableName   = "GanglandUndercover";
        private const string ProductName      = "Gangland Undercover";
        private const string CompanyName      = "GanglandUndercover";
        private const string OutputRoot       = "Builds";
        private const string StandaloneAppId  = "com.gangland.undercover";
        private const long SteamVisualArchiveTargetBytes = 650L * 1024L * 1024L;

        private static readonly VisualArchiveSource[] SteamVisualArchiveSources =
        {
            new VisualArchiveSource(
                "street-and-road-kit",
                "Assets/_Project/Legacy3D/ModularLowpolyStreetsFree",
                "PC Steam depot art reference: roads, sidewalks, street props, and pavement textures for the港区 exterior pass."),
            new VisualArchiveSource(
                "synthetic-police-urban-kit",
                "Assets/_Project/Legacy3D/Synty",
                "PC Steam depot art reference: stylized character, prop, lens dirt, and generic scene material candidates."),
            new VisualArchiveSource(
                "city-crowd-animation-kit",
                "Assets/_Project/Legacy3D/DenysAlmaral",
                "PC Steam depot art reference: city crowd bodies, animation poses, and NPC staging candidates."),
            new VisualArchiveSource(
                "simple-poly-city-kit",
                "Assets/_Project/Legacy3D/SimplePoly City - Low Poly Assets",
                "PC Steam depot art reference: skyline, storefront, traffic, vehicle, and street silhouette candidates."),
            new VisualArchiveSource(
                "cinematic-audio-reference",
                "Assets/_Project/Legacy3D/FreePackUnused",
                "PC Steam depot audio reference: atmosphere, impact, alarm, and trailer sound candidates for visual presentation passes."),
            new VisualArchiveSource(
                "kenney-quaternius-source-library",
                "Assets/_Project/Art/ThirdParty",
                "PC Steam depot art reference: Kenney/Quaternius source packs for controls, city props, characters, vehicles, and future capsule/trailer material."),
            new VisualArchiveSource(
                "runtime-assetstore-reference",
                "Assets/_Project/Resources/AssetStore",
                "PC Steam depot art reference: local AssetStore resources kept for visual comparison and future runtime replacement candidates."),
            new VisualArchiveSource(
                "runtime-audio-ui-reference",
                "Assets/_Project/Audio",
                "PC Steam depot audio reference: curated UI, ambience, BGM, and gameplay SFX currently tracked in the project."),
            new VisualArchiveSource(
                "runtime-ui-source-library",
                "Assets/_Project/UI",
                "PC Steam depot UI reference: button sprites and UI skin source assets for the Steam UI polish pass."),
        };

        private static readonly string[] MainScenes =
        {
            // 引导场景：内含挂了 PrototypeBootstrap 的 GameObject，Awake 中程序化拉起主菜单与对局。
            // 与 EditorBuildSettings 启用的场景保持一致。
            "Assets/_Project/Scenes/Prototype.unity",
        };

        // ─── 公共入口 ───────────────────────────────────────

        [MenuItem("Gangland/Build/macOS Release")]
        public static void BuildMacOS()
        {
            string path = Path.Combine(OutputRoot, "macOS", ExecutableName + ".app");
            Build(BuildTarget.StandaloneOSX, path, StandaloneAppId);
        }

        [MenuItem("Gangland/Build/Windows Release")]
        public static void BuildWindows()
        {
            string path = Path.Combine(OutputRoot, "Windows", ExecutableName + ".exe");
            Build(BuildTarget.StandaloneWindows64, path, StandaloneAppId);
        }

        [MenuItem("Gangland/Build/All Platforms")]
        public static void BuildAll()
        {
            BuildMacOS();
            BuildWindows();
        }

        [MenuItem("Gangland/Build/Export Steam Visual Archive")]
        public static void ExportSteamVisualArchive()
        {
            string outDir = GetCommandLineArg("outputDir")
                ?? Path.Combine(OutputRoot, "SteamPC-20260610");
            string buildRoot = Path.Combine(outDir, BuildTarget.StandaloneWindows64.ToString());
            Directory.CreateDirectory(buildRoot);

            string placeholderPlayerPath = Path.Combine(buildRoot, ExecutableName + ".exe");
            AttachSteamVisualArchive(BuildTarget.StandaloneWindows64, placeholderPlayerPath);
        }

        /// <summary>命令行入口（CI 用）</summary>
        public static void Build()
        {
            string targetStr = GetCommandLineArg("buildTarget") ?? "StandaloneOSX";
            if (!Enum.TryParse(targetStr, out BuildTarget target))
            {
                Debug.LogError($"[BuildScript] Unknown build target: {targetStr}");
                EditorApplication.Exit(1);
                return;
            }
            string outDir = GetCommandLineArg("outputDir") ?? OutputRoot;
            string ext = target == BuildTarget.StandaloneOSX ? ".app" : ".exe";
            string path = Path.Combine(outDir, target.ToString(), ExecutableName + ext);
            Build(target, path, StandaloneAppId);
        }

        // ─── 核心逻辑 ───────────────────────────────────────

        private static void Build(BuildTarget target, string outputPath, string bundleId)
        {
            Debug.Log($"[BuildScript] Starting build for {target} → {outputPath}");

            if (!EnsureBuildTargetSupported(target))
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            // 确保输出目录存在
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 打印校验信息
            Debug.Log($"[BuildScript] Product: {Application.productName} v{Application.version}");
            Debug.Log($"[BuildScript] Unity: {Application.unityVersion}");
            Debug.Log($"[BuildScript] Scenes: {string.Join(", ", MainScenes)}");
            Debug.Log($"[BuildScript] Time: {DateTime.UtcNow:O}");

            // 写入构建信息文件（按版本号嵌入）
            WriteBuildInfo(target);

            // 配置 PlayerSettings
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;

            // 近距离语音（VoiceChatSystem）使用 Microphone API；macOS 构建必须提供用途说明，
            // 否则 Info.plist 校验会让构建失败（NSMicrophoneUsageDescription）。
            if (target == BuildTarget.StandaloneOSX
                && string.IsNullOrEmpty(PlayerSettings.macOS.microphoneUsageDescription))
            {
                PlayerSettings.macOS.microphoneUsageDescription =
                    "用于对局内的近距离语音通话。";
            }

            PlayerSettings.SetApplicationIdentifier(
                target == BuildTarget.StandaloneOSX
                    ? NamedBuildTarget.Standalone
                    : NamedBuildTarget.Standalone,
                bundleId);

            // 构建
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes           = MainScenes,
                locationPathName = outputPath,
                target           = target,
                options          = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                AttachSteamVisualArchive(target, outputPath);
                Debug.Log($"[BuildScript] BUILD SUCCESS: {summary.outputPath} ({summary.totalSize / 1024 / 1024} MB, {summary.totalTime})");
            }
            else
            {
                Debug.LogError($"[BuildScript] BUILD FAILED: {summary.result} — {summary.totalErrors} errors, {summary.totalWarnings} warnings");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static void AttachSteamVisualArchive(BuildTarget target, string outputPath)
        {
            if (target != BuildTarget.StandaloneWindows64)
            {
                return;
            }

            if (IsCommandLineFlagFalse("includeSteamVisualArchive"))
            {
                Debug.Log("[BuildScript] Steam visual archive skipped by -includeSteamVisualArchive false.");
                return;
            }

            string buildRoot = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(buildRoot))
            {
                Debug.LogWarning("[BuildScript] Steam visual archive skipped: build root is empty.");
                return;
            }

            string archiveRoot = Path.Combine(buildRoot, "SteamVisualArchive");
            if (Directory.Exists(archiveRoot))
            {
                Directory.Delete(archiveRoot, true);
            }

            Directory.CreateDirectory(archiveRoot);

            long copiedBytes = 0L;
            List<string> manifestLines = new List<string>
            {
                "# Gangland Undercover Steam Visual Archive",
                string.Empty,
                "Purpose: PC/Steam candidate package art source archive for review, trailer still selection, and downstream visual replacement work.",
                "Runtime: not loaded automatically by gameplay; ships beside the Windows player for PC-first review builds.",
                "Policy: real project-owned/local third-party art references only; no filler files.",
                string.Empty,
                "Included sources:",
            };

            foreach (VisualArchiveSource source in SteamVisualArchiveSources)
            {
                string sourcePath = Path.Combine(ProjectRootPath(), source.SourcePath);
                if (!Directory.Exists(sourcePath))
                {
                    Debug.LogWarning("[BuildScript] Steam visual archive source missing: " + source.SourcePath);
                    manifestLines.Add("- Missing: " + source.SourcePath);
                    continue;
                }

                string destinationPath = Path.Combine(archiveRoot, source.Label);
                long sourceBytes = CopyDirectoryForArchive(sourcePath, destinationPath);
                copiedBytes += sourceBytes;
                manifestLines.Add("- " + source.Label + ": " + source.SourcePath + " (" + FormatBytes(sourceBytes) + ")");
                manifestLines.Add("  " + source.Description);
            }

            manifestLines.Add(string.Empty);
            manifestLines.Add("Total copied bytes: " + copiedBytes + " (" + FormatBytes(copiedBytes) + ")");
            manifestLines.Add("Build target minimum: " + SteamVisualArchiveTargetBytes + " (" + FormatBytes(SteamVisualArchiveTargetBytes) + ")");

            File.WriteAllLines(Path.Combine(archiveRoot, "MANIFEST.md"), manifestLines);

            if (copiedBytes < SteamVisualArchiveTargetBytes)
            {
                Debug.LogWarning("[BuildScript] Steam visual archive is below target: " + FormatBytes(copiedBytes));
            }
            else
            {
                Debug.Log("[BuildScript] Steam visual archive attached: " + FormatBytes(copiedBytes));
            }
        }

        private static long CopyDirectoryForArchive(string sourceRoot, string destinationRoot)
        {
            Directory.CreateDirectory(destinationRoot);
            long copiedBytes = 0L;

            foreach (string directoryPath in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceRoot, directoryPath);
                Directory.CreateDirectory(Path.Combine(destinationRoot, relativePath));
            }

            foreach (string filePath in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(sourceRoot, filePath);
                string destinationPath = Path.Combine(destinationRoot, relativePath);
                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(filePath, destinationPath, true);
                copiedBytes += new FileInfo(filePath).Length;
            }

            return copiedBytes;
        }

        private static string FormatBytes(long bytes)
        {
            double mebibytes = bytes / 1024d / 1024d;
            return mebibytes.ToString("0.0") + " MiB";
        }

        private static string ProjectRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static bool EnsureBuildTargetSupported(BuildTarget target)
        {
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            if (BuildPipeline.IsBuildTargetSupported(targetGroup, target))
            {
                return true;
            }

            string moduleHint = target == BuildTarget.StandaloneWindows64
                ? "Install Windows Build Support (Mono) for Unity 6000.4.9f1 in Unity Hub."
                : "Install the required platform support module for Unity 6000.4.9f1.";
            Debug.LogError("[BuildScript] Build target unsupported: " + target + ". " + moduleHint);
            return false;
        }

        // ─── 辅助 ───────────────────────────────────────

        private static void WriteBuildInfo(BuildTarget target)
        {
            string dir = Path.Combine(Application.dataPath, "Resources");
            Directory.CreateDirectory(dir);

            var info = new BuildInfo
            {
                version      = Application.version,
                unityVersion = Application.unityVersion,
                target       = target.ToString(),
                buildTime    = DateTime.UtcNow.ToString("O"),
                gitCommit    = GetGitCommitHash(),
            };

            string json = JsonUtility.ToJson(info, prettyPrint: true);
            File.WriteAllText(Path.Combine(dir, "build_info.json"), json);
            Debug.Log($"[BuildScript] Build info written: {json}");
        }

        private static string GetGitCommitHash()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    WorkingDirectory       = Application.dataPath + "/..",
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                string hash = proc?.StandardOutput.ReadToEnd()?.Trim();
                proc?.WaitForExit(3000);
                return string.IsNullOrEmpty(hash) ? "unknown" : hash;
            }
            catch
            {
                return "unknown";
            }
        }

        private static string GetCommandLineArg(string key)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == $"-{key}" || args[i] == $"--{key}")
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static bool IsCommandLineFlagFalse(string key)
        {
            string value = GetCommandLineArg(key);
            return !string.IsNullOrEmpty(value)
                && (value.Equals("false", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("0", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("no", StringComparison.OrdinalIgnoreCase));
        }

        [Serializable]
        private struct BuildInfo
        {
            public string version;
            public string unityVersion;
            public string target;
            public string buildTime;
            public string gitCommit;
        }

        private readonly struct VisualArchiveSource
        {
            public VisualArchiveSource(string label, string sourcePath, string description)
            {
                Label = label;
                SourcePath = sourcePath;
                Description = description;
            }

            public readonly string Label;
            public readonly string SourcePath;
            public readonly string Description;
        }
    }
}
