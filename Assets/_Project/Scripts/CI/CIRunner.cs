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
    private const string PlayModeWatchdogActiveKey = "GanglandUndercover.CI.PlayModeWatchdogActive";
    private const string PlayModeWatchdogStartTicksKey = "GanglandUndercover.CI.PlayModeWatchdogStartTicks";
    private const string PlayModeWatchdogTimeoutKey = "GanglandUndercover.CI.PlayModeWatchdogTimeout";

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
        RunTests(TestMode.EditMode, "EditMode", 120.0);
    }

    // ── Stage 3: PlayMode Tests ──
    public static void RunPlayModeTests()
    {
        RunTests(TestMode.PlayMode, "PlayMode", 300.0);
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

    private static void RunTests(TestMode mode, string label, double timeoutSeconds)
    {
        Debug.Log($"[CI] {label} tests starting...");

        var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter { testMode = mode };
        double deadline = EditorApplication.timeSinceStartup + timeoutSeconds;
        bool completed = false;
        int passCount = 0;
        int failCount = 0;
        int skipCount = 0;

        var callbacks = new TestCallbacks(
            (result) =>
            {
                passCount = result.passCount;
                failCount = result.failCount;
                skipCount = result.skipCount;
                completed = true;
                Debug.Log($"[CI] {label}: {passCount} passed, {failCount} failed, {skipCount} skipped");
            });

        TestRunnerApi.RegisterTestCallback(callbacks);

        if (mode == TestMode.PlayMode)
        {
            SessionState.SetBool(PlayModeWatchdogActiveKey, true);
            SessionState.SetString(PlayModeWatchdogStartTicksKey, System.DateTime.UtcNow.Ticks.ToString());
            SessionState.SetFloat(PlayModeWatchdogTimeoutKey, (float)timeoutSeconds);
            RegisterPlayModeResultWatchdog();
        }

        runner.Execute(new ExecutionSettings(filter));

        void Watchdog()
        {
            if (completed)
            {
                EditorApplication.update -= Watchdog;
                TestRunnerApi.UnregisterTestCallback(callbacks);
                Object.DestroyImmediate(runner);
                if (mode == TestMode.PlayMode)
                {
                    SessionState.SetBool(PlayModeWatchdogActiveKey, false);
                    EditorApplication.update -= PlayModeResultWatchdog;
                }

                if (failCount > 0)
                {
                    Debug.LogError($"[CI] {label} tests FAILED — {failCount} failures");
                    EditorApplication.Exit(1);
                    return;
                }

                Debug.Log($"[CI] {label} tests PASSED — {passCount} passed, {skipCount} skipped");
                EditorApplication.Exit(0);
                return;
            }

            if (EditorApplication.timeSinceStartup >= deadline)
            {
                EditorApplication.update -= Watchdog;
                TestRunnerApi.UnregisterTestCallback(callbacks);
                Object.DestroyImmediate(runner);
                if (mode == TestMode.PlayMode)
                {
                    SessionState.SetBool(PlayModeWatchdogActiveKey, false);
                    EditorApplication.update -= PlayModeResultWatchdog;
                }
                Debug.LogError($"[CI] {label} tests FAILED — 0 failures, timeout=True");
                EditorApplication.Exit(1);
            }
        }

        EditorApplication.update += Watchdog;
    }

    [InitializeOnLoadMethod]
    private static void ResumePlayModeResultWatchdog()
    {
        if (SessionState.GetBool(PlayModeWatchdogActiveKey, false))
        {
            RegisterPlayModeResultWatchdog();
        }
    }

    private static void RegisterPlayModeResultWatchdog()
    {
        EditorApplication.update -= PlayModeResultWatchdog;
        EditorApplication.update += PlayModeResultWatchdog;
    }

    private static void PlayModeResultWatchdog()
    {
        if (!SessionState.GetBool(PlayModeWatchdogActiveKey, false))
        {
            EditorApplication.update -= PlayModeResultWatchdog;
            return;
        }

        System.DateTime startUtc = PlayModeWatchdogStartUtc();
        double timeoutSeconds = SessionState.GetFloat(PlayModeWatchdogTimeoutKey, 300f);
        if ((System.DateTime.UtcNow - startUtc).TotalSeconds > timeoutSeconds)
        {
            SessionState.SetBool(PlayModeWatchdogActiveKey, false);
            EditorApplication.update -= PlayModeResultWatchdog;
            Debug.LogError("[CI] PlayMode tests FAILED — result watchdog timeout=True");
            EditorApplication.Exit(1);
            return;
        }

        string resultPath = System.IO.Path.Combine(Application.persistentDataPath, "TestResults.xml");
        if (!System.IO.File.Exists(resultPath) || System.IO.File.GetLastWriteTimeUtc(resultPath) < startUtc)
            return;

        try
        {
            var doc = new System.Xml.XmlDocument();
            doc.Load(resultPath);
            System.Xml.XmlElement root = doc.DocumentElement;
            int passed = ParseXmlInt(root, "passed");
            int failed = ParseXmlInt(root, "failed");
            int skipped = ParseXmlInt(root, "skipped");

            SessionState.SetBool(PlayModeWatchdogActiveKey, false);
            EditorApplication.update -= PlayModeResultWatchdog;

            if (failed > 0)
            {
                Debug.LogError($"[CI] PlayMode tests FAILED — {failed} failures, {passed} passed, {skipped} skipped");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[CI] PlayMode tests PASSED — {passed} passed, {skipped} skipped");
            EditorApplication.Exit(0);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[CI] PlayMode result watchdog could not parse results yet: " + ex.Message);
        }
    }

    private static System.DateTime PlayModeWatchdogStartUtc()
    {
        string ticksText = SessionState.GetString(PlayModeWatchdogStartTicksKey, string.Empty);
        if (long.TryParse(ticksText, out long ticks))
        {
            return new System.DateTime(ticks, System.DateTimeKind.Utc);
        }

        return System.DateTime.UtcNow;
    }

    private static int ParseXmlInt(System.Xml.XmlElement element, string attributeName)
    {
        if (element == null)
            return 0;

        string value = element.GetAttribute(attributeName);
        return int.TryParse(value, out int parsed) ? parsed : 0;
    }

    private class TestCallbacks : ICallbacks
    {
        private readonly System.Action<TestRunResult> onResult;

        public TestCallbacks(System.Action<TestRunResult> onResult)
        {
            this.onResult = onResult;
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
