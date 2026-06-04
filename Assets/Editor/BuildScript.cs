using System;
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
        private const string ProductName      = "GanglandUndercover";
        private const string CompanyName      = "GanglandUndercover";
        private const string OutputRoot       = "Builds";
        private const string macOSBundleId    = "com.gangland.undercover";
        private const string WindowsProductGuid = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

        private static readonly string[] MainScenes =
        {
            "Assets/_Project/Scenes/PrototypeBootstrap.unity",
        };

        // ─── 公共入口 ───────────────────────────────────────

        [MenuItem("Gangland/Build/macOS Release")]
        public static void BuildMacOS()
        {
            string path = Path.Combine(OutputRoot, "macOS", ProductName + ".app");
            Build(BuildTarget.StandaloneOSX, path, macOSBundleId);
        }

        [MenuItem("Gangland/Build/Windows Release")]
        public static void BuildWindows()
        {
            string path = Path.Combine(OutputRoot, "Windows", ProductName + ".exe");
            Build(BuildTarget.StandaloneWindows64, path, WindowsProductGuid);
        }

        [MenuItem("Gangland/Build/All Platforms")]
        public static void BuildAll()
        {
            BuildMacOS();
            BuildWindows();
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
            string bundleId = target == BuildTarget.StandaloneOSX ? macOSBundleId : WindowsProductGuid;
            string ext = target == BuildTarget.StandaloneOSX ? ".app" : ".exe";
            string path = Path.Combine(outDir, target.ToString(), ProductName + ext);
            Build(target, path, bundleId);
        }

        // ─── 核心逻辑 ───────────────────────────────────────

        private static void Build(BuildTarget target, string outputPath, string bundleId)
        {
            Debug.Log($"[BuildScript] Starting build for {target} → {outputPath}");

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

        [Serializable]
        private struct BuildInfo
        {
            public string version;
            public string unityVersion;
            public string target;
            public string buildTime;
            public string gitCommit;
        }
    }
}
