// Gangland Undercover — CI Runner
// 用法 (batchmode):
//   Unity -executeMethod CIRunner.Compile
//   Unity -executeMethod CIRunner.RunEditModeTests
//   Unity -executeMethod CIRunner.RunPlayModeTests
//   Unity -executeMethod CIRunner.BuildAll
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class CIRunner
{
    // ── Stage 1: Compile Check ──
    public static void Compile()
    {
        Debug.Log("[CI] Compile check starting...");

        // Force recompile to catch all errors
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        // Unity sets this after compilation; check for errors
        // Note: In batchmode, Unity exits with non-zero on compile errors before
        // reaching this point. This is a belt-and-suspenders check.
        if (EditorUtility.scriptCompilationFailed)
        {
            Debug.LogError("[CI] Compile FAILED — script compilation errors detected");
            EditorApplication.Exit(1);
        }

        // Log all compile warnings for awareness
        Debug.Log("[CI] Compile PASSED — no compilation errors");
    }

    // ── Stage 2: EditMode Tests ──
    public static void RunEditModeTests()
    {
        Debug.Log("[CI] EditMode tests starting...");

        var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = new[] { "GanglandUndercover" },
        };

        bool completed = false;
        int passCount = 0;
        int failCount = 0;
        int skipCount = 0;

        runner.RegisterCallbacks(new TestCallbacks(
            (result) =>
            {
                passCount = result.passCount;
                failCount = result.failCount;
                skipCount = result.skipCount;
                Debug.Log($"[CI] EditMode: {passCount} passed, {failCount} failed, {skipCount} skipped");
            },
            () => { completed = true; }
        ));

        runner.Execute(new ExecutionSettings(filter));

        // Wait for completion with 2-minute timeout
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!completed && sw.ElapsedMilliseconds < 120000)
        {
            System.Threading.Thread.Sleep(100);
        }

        if (!completed || failCount > 0)
        {
            Debug.LogError($"[CI] EditMode tests FAILED — {failCount} failures, timeout={!completed}");
            EditorApplication.Exit(1);
        }

        Debug.Log($"[CI] EditMode tests PASSED — {passCount} passed, {skipCount} skipped");
    }

    // ── Stage 3: PlayMode Tests ──
    public static void RunPlayModeTests()
    {
        Debug.Log("[CI] PlayMode tests starting...");

        var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter
        {
            testMode = TestMode.PlayMode,
            groupNames = new[] { "GanglandUndercover" },
        };

        bool completed = false;
        int passCount = 0;
        int failCount = 0;
        int skipCount = 0;

        runner.RegisterCallbacks(new TestCallbacks(
            (result) =>
            {
                passCount = result.passCount;
                failCount = result.failCount;
                skipCount = result.skipCount;
                Debug.Log($"[CI] PlayMode: {passCount} passed, {failCount} failed, {skipCount} skipped");
            },
            () => { completed = true; }
        ));

        runner.Execute(new ExecutionSettings(filter));

        // Wait for completion with 5-minute timeout
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!completed && sw.ElapsedMilliseconds < 300000)
        {
            System.Threading.Thread.Sleep(100);
        }

        if (!completed || failCount > 0)
        {
            Debug.LogError($"[CI] PlayMode tests FAILED — {failCount} failures, timeout={!completed}");
            EditorApplication.Exit(1);
        }

        Debug.Log($"[CI] PlayMode tests PASSED — {passCount} passed, {skipCount} skipped");
    }

    // ── Stage 4: Build All ──
    public static void BuildAll()
    {
        Debug.Log("[CI] Build starting...");
        InvokeBuildScriptMethod("BuildMacOS");
        InvokeBuildScriptMethod("BuildWindows");
        Debug.Log("[CI] Build PASSED");
    }

    // ── Helpers ──
    private static void InvokeBuildScriptMethod(string methodName)
    {
        var candidates = new[]
        {
            "BuildScript",
            "GanglandUndercover.Editor.BuildScript",
        };

        foreach (var typeName in candidates)
        {
            var type = FindType(typeName);
            if (type == null)
            {
                continue;
            }

            var method = type.GetMethod(
                methodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                continue;
            }

            method.Invoke(null, null);
            return;
        }

        Debug.LogError($"[CI] Build FAILED — no BuildScript.{methodName} entry point found");
        EditorApplication.Exit(1);
    }

    private static System.Type FindType(string typeName)
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private class TestCallbacks : ICallbacks
    {
        private readonly System.Action<TestRunResult> onResult;
        private readonly System.Action onFinish;

        public TestCallbacks(System.Action<TestRunResult> onResult, System.Action onFinish)
        {
            this.onResult = onResult;
            this.onFinish = onFinish;
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            onResult(new TestRunResult
            {
                passCount = result.PassCount,
                failCount = result.FailCount,
                skipCount = result.SkipCount,
                resultState = result.FailCount > 0 ? TestRunState.Fail : TestRunState.Pass,
            });
            onFinish();
        }

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

    }

    private struct TestRunResult
    {
        public int passCount;
        public int failCount;
        public int skipCount;
        public TestRunState resultState;
    }

    private enum TestRunState { Pass, Fail }
}
#endif
