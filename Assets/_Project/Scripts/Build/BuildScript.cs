// Gangland Undercover — Batchmode Build Script
// 用法: Unity -executeMethod BuildScript.BuildMacOS -buildVersion 0.1.0-beta.1
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    private static string BuildVersion =>
        System.Environment.GetCommandLineArgs()
            .GetArgument("-buildVersion", "0.1.0-dev");

    private static string BuildOutputPath =>
        System.Environment.GetCommandLineArgs()
            .GetArgument("-buildOutputPath", "Builds");

    private static readonly string[] ScenePaths = new[]
    {
        "Assets/_Project/Scenes/Stage1VerticalSlice.unity",
        "Assets/_Project/Scenes/Prototype.unity",
    };

    // ── macOS ──
    public static void BuildMacOS()
    {
        var options = new BuildPlayerOptions
        {
            scenes = ScenePaths,
            locationPathName = $"{BuildOutputPath}/GanglandUndercover.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None,
        };
        Build(options, "macOS");
    }

    // ── Windows ──
    public static void BuildWindows()
    {
        var options = new BuildPlayerOptions
        {
            scenes = ScenePaths,
            locationPathName = $"{BuildOutputPath}/GanglandUndercover.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        Build(options, "Windows");
    }

    // ── All ──
    public static void BuildAll()
    {
        BuildMacOS();
        BuildWindows();
    }

    private static void Build(BuildPlayerOptions options, string platformName)
    {
        PlayerSettings.bundleVersion = BuildVersion;
        PlayerSettings.productName = "Gangland Undercover";

        Debug.Log($"=== Building {platformName} v{BuildVersion} ===");
        Debug.Log($"Scenes: {string.Join(", ", options.scenes)}");
        Debug.Log($"Output: {options.locationPathName}");

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"=== BUILD SUCCESS: {platformName} ({summary.totalSize} bytes, {summary.totalTime}) ===");
        }
        else
        {
            Debug.LogError($"=== BUILD FAILED: {platformName} — Errors: {summary.totalErrors} ===");
            EditorApplication.Exit(1);
        }
    }

    private static string GetArgument(this string[] args, string key, string defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key) return args[i + 1];
        }
        return defaultValue;
    }
}
#endif
