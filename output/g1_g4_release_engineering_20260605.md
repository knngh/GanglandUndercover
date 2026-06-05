# Gangland Undercover 阶段 G：发布工程文档

> 日期：2026-06-05  
> 状态：权威设计文档  
> 依赖：阶段 0（基线冻结）、A4（Relay clean machine 验证）  
> 目标：产出可信封测构建，建立持续集成和性能基线

---

## G1 macOS 和 Windows 构建脚本

### 1.1 构建要求

| 平台 | 包格式 | 最低 OS 版本 | 最低硬件 |
|------|--------|-------------|----------|
| macOS | .app 包（或 .zip 压缩发布） | macOS 12 Monterey | Apple Silicon M1 / Intel i5 |
| Windows | 独立 exe + Data 文件夹（或 .zip） | Windows 10 22H2 | Intel i5 / AMD Ryzen 5，4GB RAM |

- IL2CPP 后端（Windows: x86_64，macOS: Apple Silicon + Intel 双架构）。
- 目标帧率：60fps，720p 至 1440p 窗口。
- 不依赖网络发现协议（纯 Relay/直连）。
- 构建产物不含 gitignored 第三方资产路径。

### 1.2 Unity Batchmode 构建脚本

#### macOS Build Script（`build_macos.sh`）

```bash
#!/bin/bash
# Gangland Undercover — macOS Build Script
# 用法: ./build_macos.sh [version] [output_dir]
# 示例: ./build_macos.sh 0.1.0-beta ./Builds

set -euo pipefail

VERSION="${1:-0.1.0-dev}"
OUTPUT_DIR="${2:-./Builds/macOS}"
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY="/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity"
LOG_FILE="${PROJECT_DIR}/unity-build.log"

echo "=== Gangland Undercover macOS Build ==="
echo "Version: ${VERSION}"
echo "Output:  ${OUTPUT_DIR}"
echo "Project: ${PROJECT_DIR}"

mkdir -p "${OUTPUT_DIR}"

"${UNITY}" \
  -quit \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_DIR}" \
  -buildTarget StandaloneOSX \
  -executeMethod BuildScript.BuildMacOS \
  -logFile "${LOG_FILE}" \
  -buildVersion "${VERSION}" \
  -buildOutputPath "${OUTPUT_DIR}"

BUILD_EXIT=$?

if [ $BUILD_EXIT -eq 0 ]; then
  echo ""
  echo "=== BUILD SUCCESS ==="
  echo "Output: ${OUTPUT_DIR}"
  ls -lh "${OUTPUT_DIR}/GanglandUndercover.app" 2>/dev/null || \
    ls -lh "${OUTPUT_DIR}"/*.app 2>/dev/null || \
    echo "Check ${OUTPUT_DIR} for build artifacts"
else
  echo ""
  echo "=== BUILD FAILED (exit code: ${BUILD_EXIT}) ==="
  echo "See ${LOG_FILE} for details"
  tail -n 50 "${LOG_FILE}"
fi

exit $BUILD_EXIT
```

#### Windows Build Script（`build_windows.bat`）

```batch
@echo off
REM Gangland Undercover — Windows Build Script
REM 用法: build_windows.bat [version] [output_dir]

setlocal enabledelayedexpansion

set VERSION=%1
if "%VERSION%"=="" set VERSION=0.1.0-dev

set OUTPUT_DIR=%2
if "%OUTPUT_DIR%"=="" set OUTPUT_DIR=.\Builds\Windows

set PROJECT_DIR=%~dp0
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe"
set LOG_FILE=%PROJECT_DIR%\unity-build.log

echo === Gangland Undercover Windows Build ===
echo Version: %VERSION%
echo Output:  %OUTPUT_DIR%
echo Project: %PROJECT_DIR%

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

%UNITY% ^
  -quit ^
  -batchmode ^
  -nographics ^
  -projectPath "%PROJECT_DIR%" ^
  -buildTarget StandaloneWindows64 ^
  -executeMethod BuildScript.BuildWindows ^
  -logFile "%LOG_FILE%" ^
  -buildVersion "%VERSION%" ^
  -buildOutputPath "%OUTPUT_DIR%"

if %ERRORLEVEL% EQU 0 (
  echo.
  echo === BUILD SUCCESS ===
  echo Output: %OUTPUT_DIR%
  dir "%OUTPUT_DIR%\GanglandUndercover.exe"
) else (
  echo.
  echo === BUILD FAILED (exit code: %ERRORLEVEL%) ===
  echo See %LOG_FILE% for details
)
exit /b %ERRORLEVEL%
```

#### 全平台构建脚本（`build_all.sh`）

```bash
#!/bin/bash
# Gangland Undercover — All Platforms Build
# 用法: ./build_all.sh [version]

set -euo pipefail

VERSION="${1:-0.1.0-dev}"
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "=== Building All Platforms (version: ${VERSION}) ==="

# macOS
echo ""
echo "--- macOS ---"
bash "${PROJECT_DIR}/build_macos.sh" "${VERSION}" "${PROJECT_DIR}/Builds/macOS"
MAC_EXIT=$?

# Windows
echo ""
echo "--- Windows ---"
bash "${PROJECT_DIR}/build_windows.sh" "${VERSION}" "${PROJECT_DIR}/Builds/Windows"
WIN_EXIT=$?

echo ""
echo "=== Build Summary ==="
echo "macOS:   $([ $MAC_EXIT -eq 0 ] && echo 'SUCCESS' || echo 'FAILED')"
echo "Windows: $([ $WIN_EXIT -eq 0 ] && echo 'SUCCESS' || echo 'FAILED')"
```

### 1.3 C# Build Script（`BuildScript.cs`）

```csharp
// Assets/_Project/Scripts/Build/BuildScript.cs
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
        "Assets/_Project/Scenes/MainMenu.unity",
        "Assets/_Project/Scenes/Lobby.unity",
        "Assets/_Project/Scenes/HarbourDistrict.unity",
        "Assets/_Project/Scenes/PoliceStation.unity",
        "Assets/_Project/Scenes/KowloonWalledCity.unity",
        "Assets/_Project/Scenes/GameOver.unity",
        "Assets/_Project/Scenes/Tutorial.unity",
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
```

### 1.4 场景列表

构建包含以下场景（`File > Build Settings > Scenes In Build`）：

| 场景文件 | 用途 | 索引 |
|----------|------|------|
| `Assets/_Project/Scenes/MainMenu.unity` | 主菜单 | 0 |
| `Assets/_Project/Scenes/Lobby.unity` | 联机大厅 | 1 |
| `Assets/_Project/Scenes/HarbourDistrict.unity` | 港区地图 | 2 |
| `Assets/_Project/Scenes/PoliceStation.unity` | 警署地图 | 3 |
| `Assets/_Project/Scenes/KowloonWalledCity.unity` | 九龙城寨地图 | 4 |
| `Assets/_Project/Scenes/GameOver.unity` | 结算场景 | 5 |
| `Assets/_Project/Scenes/Tutorial.unity` | 新手教程场景 | 6 |

### 1.5 Clean Machine 验证步骤

**macOS**：
1. 找一台未安装 Unity 的 Mac。
2. 复制 `GanglandUndercover.app`。
3. 启动 → 验证主菜单显示、无崩溃。
4. 创建房间 → 放行网络权限 → 房间码生成。
5. 加入房间（从另一台设备输入房间码）。
6. 验证不弹出"需要 Unity"或"需要 Xcode"等错误。

**Windows**：
1. 找一台 Windows 10/11 干净机器。
2. 解压 `GanglandUndercover.zip`。
3. 双击 `GanglandUndercover.exe` → 验证不出现动态库缺失错误。
4. 同 macOS 步骤 4-6。

---

## G2 性能预算（Performance Budget）

### 2.1 帧率目标

| 配置 | 目标帧率 | 最低帧率 | 场景 |
|------|----------|----------|------|
| High（1440p） | 60fps | 50fps | 所有地图 |
| Medium（1080p） | 60fps | 55fps | 所有地图 |
| Low（720p） | 60fps | 60fps | 所有地图 |

- **帧率衡量**：Unity Profiler 平均帧率（建议 60 秒采样）。
- **卡顿阈值**：单帧耗时 > 33ms（低于 30fps）记为卡顿帧，占比 < 1%。

### 2.2 Draw Call 预算

| 类别 | 目标 | 上限 |
|------|------|------|
| 总 Draw Calls（全屏） | < 200 | < 500 |
| 角色（每 10 人） | < 40 | < 60 |
| 地图静态（tile） | < 80 | < 120 |
| UI Canvas | < 30 | < 50 |
| VFX / 破坏遮罩 | < 20 | < 40 |
| 小游戏 UI | < 30 | < 50 |

- 使用 `FrameDebugger` 检查每帧 Draw Call 分布。
- 地图 tile 合并到 Sprite Atlas 以减少 batch。

### 2.3 Sprite Atlas 预算

| Atlas | 内容 | 最大尺寸 | 最大文件大小 |
|-------|------|----------|-------------|
| `atlas_harbour_tiles` | 港区地板/墙壁 tile | 2048×2048 | 1 MB |
| `atlas_harbour_props` | 港区道具 sprite | 2048×2048 | 1 MB |
| `atlas_police_tiles` | 警署地板/墙壁 tile | 2048×2048 | 1 MB |
| `atlas_police_props` | 警署道具 sprite | 2048×2048 | 1 MB |
| `atlas_kowloon_tiles` | 九龙城寨地板/墙壁 tile | 2048×2048 | 1 MB |
| `atlas_kowloon_props` | 九龙城寨道具 sprite | 2048×2048 | 1 MB |
| `atlas_characters` | 所有角色 sprite sheet | 2048×2048 | 1 MB |
| `atlas_ui_common` | 通用 UI 元素 | 1024×1024 | 0.5 MB |
| `atlas_vfx` | VFX sprite | 1024×1024 | 0.5 MB |
| `atlas_minigames` | 小游戏 UI 资源 | 1024×1024 | 0.5 MB |

- 每个 Sprite Atlas ≤ 1 MB（压缩后），格式 RGBA32。
- 如需更大，使用 `maxTextureSize=4096`，上限 2 MB。

### 2.4 网络带宽预算

| 指标 | 目标 | 上限 |
|------|------|------|
| 每客户端下行 | < 50 KB/s | < 100 KB/s |
| 每客户端上行 | < 20 KB/s | < 50 KB/s |
| Server（Host）总带宽 | < 200 KB/s | < 500 KB/s（10 客户端） |
| 单个 RPC 消息 | < 512 bytes | < 1 KB |
| NetworkVariable 更新频率 | 2-5 Hz（可配置） | 10 Hz 上限 |

**带宽优化策略**：
- 移动同步：基于 `NetworkTransform` 插值，同步率 10 Hz → 可降至 5 Hz。
- 任务状态：仅在完成/失败时同步（事件驱动），非持续 stream。
- 聊天消息：单条 < 256 bytes（120 字符 UTF-8 ≈ 360 bytes + header）。
- 破坏计时器：仅同步状态变更（激活/修复），非每帧计时器值。
- 使用 Unity Transport `MaxSendQueueSize` 限制发送队列。

**验证工具**：
- Unity Profiler 网络模块。
- `NetworkManager.NetworkConfig` 中 `NetworkTransport.OnTransportEvent` 日志。
- 自定义 `BandwidthMonitor` 组件统计每秒 byte 收发量。

### 2.5 内存预算

| 类别 | 目标 | 上限 |
|------|------|------|
| 总内存（运行时） | < 500 MB | < 800 MB |
| 纹理内存 | < 100 MB | < 150 MB |
| Mesh 内存 | < 20 MB | < 40 MB |
| Audio 内存 | < 20 MB | < 40 MB |
| GC 分配 / 帧 | < 1 KB | < 5 KB |

- 使用 `MemoryProfiler` 包检查。
- 纹理格式：RGBA32 → DXT5/ETC2 压缩（真机）。
- 音频：ogg vorbis 流式加载（Ambience/BGM），Decompress On Load（短 SFX）。

### 2.6 长局性能测试

- 时长：20 分钟连续游玩（标准局约 10-12 分钟，双倍膨胀）。
- 采样点：每 2 分钟记录帧率、内存、Draw Call、带宽。
- 验收标准：
  - 帧率退化 < 10%（第 20 分钟 vs 第 2 分钟）。
  - 内存增长 < 50 MB（排除正常的分配波动）。
  - 无 NetworkObject 泄漏（远端复制对象不会无限增长）。

---

## G3 CI 自动化（Continuous Integration）

### 3.1 CI 管道概览

```
┌─────────┐    ┌──────────┐    ┌───────────┐    ┌──────────┐    ┌────────┐
│ Checkout │ → │ Compile   │ → │ EditMode  │ → │ PlayMode │ → │ Build  │
│          │    │ (C#)      │    │ Tests     │    │ Tests     │    │        │
└─────────┘    └──────────┘    └───────────┘    └───────────┘    └────────┘
     0s            30-60s          10-30s           2-5min         2-5min
```

总管道时长目标：< 10 分钟。

### 3.2 单脚本 CI 入口（`ci_run.sh`）

```bash
#!/bin/bash
# Gangland Undercover — CI Pipeline (Single Script)
# 用法: ./ci_run.sh [--skip-build] [--skip-tests]
# 退出码: 0=全部通过, 非0=某阶段失败

set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY="/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity"
CI_LOG_DIR="${PROJECT_DIR}/ci-logs"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
FINAL_EXIT=0

mkdir -p "${CI_LOG_DIR}"

log_stage() {
  echo ""
  echo "========== STAGE: $1 =========="
  echo "Log: ${CI_LOG_DIR}/${TIMESTAMP}_$2.log"
}

run_unity() {
  local method="$1"
  local log_name="$2"
  local extra_args="${3:-}"

  "${UNITY}" \
    -quit \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_DIR}" \
    -executeMethod "${method}" \
    -logFile "${CI_LOG_DIR}/${TIMESTAMP}_${log_name}.log" \
    ${extra_args} || return $?
}

# ── Stage 1: Compile ──
log_stage "Compile" "compile"
run_unity "CIRunner.Compile" "compile"
echo "  ✅ Compile passed"

# ── Stage 2: EditMode Tests ──
log_stage "EditMode Tests" "editmode"
run_unity "CIRunner.RunEditModeTests" "editmode"
echo "  ✅ EditMode tests passed"

# ── Stage 3: PlayMode Tests ──
log_stage "PlayMode Tests" "playmode"
run_unity "CIRunner.RunPlayModeTests" "playmode"
echo "  ✅ PlayMode tests passed"

# ── Stage 4: Build ──
log_stage "Build" "build"
run_unity "CIRunner.BuildAll" "build"
echo "  ✅ Build passed"

echo ""
echo "========== CI PIPELINE PASSED =========="
echo "Logs: ${CI_LOG_DIR}/${TIMESTAMP}_*.log"
exit 0
```

### 3.3 C# CI Runner（`CIRunner.cs`）

```csharp
// Assets/_Project/Scripts/CI/CIRunner.cs
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

        // Force recompile
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        // Check for compile errors
        if (EditorUtility.scriptCompilationFailed)
        {
            Debug.LogError("[CI] Compile FAILED");
            EditorApplication.Exit(1);
        }

        Debug.Log("[CI] Compile PASSED");
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
        bool passed = false;

        runner.RegisterCallbacks(new TestCallbacks(
            (result) => {
                passed = result.resultState == TestRunResult.Pass;
                Debug.Log($"[CI] EditMode: {result.passCount} passed, {result.failCount} failed, {result.skipCount} skipped");
            },
            () => { completed = true; }
        ));

        runner.Execute(new ExecutionSettings(filter));

        // Wait for completion (batchmode loop)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!completed && sw.ElapsedMilliseconds < 120000) // 2 min timeout
        {
            System.Threading.Thread.Sleep(100);
        }

        if (!passed)
        {
            Debug.LogError("[CI] EditMode tests FAILED");
            EditorApplication.Exit(1);
        }

        Debug.Log("[CI] EditMode tests PASSED");
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
        bool passed = false;

        runner.RegisterCallbacks(new TestCallbacks(
            (result) => {
                passed = result.resultState == TestRunResult.Pass;
                Debug.Log($"[CI] PlayMode: {result.passCount} passed, {result.failCount} failed, {result.skipCount} skipped");
            },
            () => { completed = true; }
        ));

        runner.Execute(new ExecutionSettings(filter));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!completed && sw.ElapsedMilliseconds < 300000) // 5 min timeout
        {
            System.Threading.Thread.Sleep(100);
        }

        if (!passed)
        {
            Debug.LogError("[CI] PlayMode tests FAILED");
            EditorApplication.Exit(1);
        }

        Debug.Log("[CI] PlayMode tests PASSED");
    }

    // ── Stage 4: Build ──
    public static void BuildAll()
    {
        Debug.Log("[CI] Build starting...");
        BuildScript.BuildMacOS();
        BuildScript.BuildWindows();
        Debug.Log("[CI] Build PASSED");
    }

    // ── Helpers ──
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
                resultState = result.FailCount > 0 ? TestRunResult.Fail : TestRunResult.Pass,
            });
        }
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void OnComplete()
        {
            onFinish();
        }
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
```

### 3.4 CI 日志输出结构

```
ci-logs/
├── 20260605_143000_compile.log
├── 20260605_143000_editmode.log
├── 20260605_143000_playmode.log
├── 20260605_143000_build.log
├── 20260605_150000_compile.log
└── ...
```

每条日志中搜索关键标记：
- `[CI] Compile PASSED` / `[CI] Compile FAILED`
- `[CI] EditMode tests PASSED` / `[CI] EditMode tests FAILED`
- `[CI] PlayMode tests PASSED` / `[CI] PlayMode tests FAILED`
- `[CI] Build PASSED` / `[CI] Build FAILED`

### 3.5 CI 失败处理

| 阶段 | 失败行为 | 恢复方式 |
|------|----------|----------|
| Compile | 立即退出，不跑后续 | 检查 `unity-compile.log` 找到错误文件和行号 |
| EditMode | 立即退出 | 检查 EditMode 失败测试的 `test-results.xml` |
| PlayMode | 立即退出 | 检查 PlayMode 失败测试的 `playmode-results.xml` |
| Build | 立即退出 | 检查 `unity-build.log`（编译错误或场景缺失） |

---

## G4 封测门禁清单（Beta Gate Checklist）

### 4.1 72 小时无 P0/P1 问题

#### 缺陷等级定义

| 等级 | 定义 | 示例 |
|------|------|------|
| P0 | 阻断性：核心玩法不可用、崩溃、数据丢失 | 加入房间即崩溃、投票不计数、Host 掉线无提示 |
| P1 | 严重：重要功能缺失或严重影响体验 | 任务完成不计数、聊天频道错误、破坏不修复 |
| P2 | 一般：功能可用但体验差 | UI 遮挡、文字截断、动画不流畅 |
| P3 | 轻微：视觉瑕疵、建议优化 | 像素偏移、拼写错误、颜色微调 |

#### 验收规则

- **封测前提**：最近 72 小时无新增 P0，无可复现 P1。
- **P0 处理**：发现后立即修复，修复后重置 72 小时计数器。
- **P1 处理**：修复后需回归验证，确认不引入新 P0。
- **已知 P2/P3**：可记录为 known issues，不阻塞封测。

### 4.2 版本号规范

```
格式：MAJOR.MINOR.PATCH-PRELEASE

示例：
  0.1.0-dev     — 开发版本
  0.1.0-beta.1  — 封闭测试第一版
  0.1.0-beta.2  — 封闭测试第二版
  1.0.0         — 正式发布

当前封测目标版本号：0.1.0-beta.1
```

**版本号写入位置**：
- `ProjectSettings/ProjectSettings.asset` → `bundleVersion: 0.1.0-beta.1`
- `Application.version` 运行时显示（主菜单左下角）。
- 构建文件名：`GanglandUndercover_0.1.0-beta.1_macOS.zip`。

### 4.3 Bug Tracker 清单

#### 推荐结构（GitHub Issues / 内部看板）

每个 issue 必须包含：

```
标题：[P0] 加入房间时客户端崩溃

标签：bug, P0, networking, crash
版本：0.1.0-beta.1
平台：Windows, macOS
复现步骤：
  1. Host 创建房间 AB3F7K
  2. Client 输入房间码加入
  3. 加入瞬间 Client 崩溃
  4. Host 日志："Client disconnected"

预期行为：Client 成功加入，显示大厅 UI

实际行为：Client 黑屏后退出

日志/截图：[附件]

Assignee: @dev-name
```

#### 必设标签

- `bug` / `feature` / `polish`
- `P0` / `P1` / `P2` / `P3`
- `networking` / `ui` / `gameplay` / `audio` / `art` / `build`
- `macOS` / `windows`
- `verified`（修复已验证）/ `wontfix` / `duplicate`

### 4.4 回滚计划

#### 回滚触发条件
- 封测构建出现新增 P0 崩溃（5 分钟内 ≥ 3 个玩家报告）。
- 服务器端出现无法在线热修的严重 bug。
- 关键数据（PlayerPrefs / save）损坏。

#### 回滚方案
1. **本地回滚**：切换到上一个标签版本。
   ```bash
   git checkout tags/v0.1.0-beta.0
   git checkout -b hotfix/rollback
   ```
2. **构建回滚**：重新分发上一个稳定构建包（`Builds/stable/` 保留最近 3 个版本）。
3. **房间回滚**：Host 端可降级到上一版本并重新创建房间。

#### 稳定构建保留策略
```
Builds/
├── stable/
│   ├── GanglandUndercover_0.1.0-beta.0_macOS.zip   # N-2
│   ├── GanglandUndercover_0.1.0-beta.1_macOS.zip   # N-1
│   └── GanglandUndercover_0.1.0-beta.2_macOS.zip   # 当前
└── ...
```

### 4.5 测试说明文档

封测需向测试人员提供以下文档（写入 `output/beta_test_instructions_20260605.md`）：

1. **游戏简介**：1 段说明（警匪社交推理）。
2. **系统要求**：macOS 12+ / Windows 10+，4GB RAM。
3. **安装步骤**：
   - macOS：解压 `.zip`，将 `.app` 拖入 Applications。
   - Windows：解压 `.zip`，双击 `.exe`。
4. **启动流程**：
   - 主菜单 → 创建/加入房间 → 输入房间码 → 准备 → 开始。
5. **基本操作**：WASD 移动、E 交互、Q 技能、Tab 小地图、Enter 聊天。
6. **游戏规则**：警方完成任务找黑帮，黑帮破坏和击杀。会议投票淘汰。
7. **已知问题**：列出所有 P2/P3 known issues。
8. **测试重点**：
   - 多端连接稳定性（4-6 人）。
   - 会议投票一致性。
   - 11 种小游戏联机同步。
   - 破坏系统修复反馈。
   - 聊天频道规则。
   - 断线重连体验。

### 4.6 反馈入口

- **封测反馈表单**（Google Form / 腾讯问卷 / GitHub Issues）：
  - 必填：游戏版本、平台、玩家数量。
  - 必填：问题描述 + 复现步骤。
  - 选填：截图/录屏/日志。
- **Discord / QQ 群**：实时反馈通道（注明玩家反馈专用）。
- **自动化崩溃报告**：使用 Unity `CrashReportHandler` 捕获崩溃堆栈并上报到服务端。

### 4.7 封测就绪最终清单

```
[ ] 构建：macOS 和 Windows 构建均可在干净机器启动
[ ] 版本号：0.1.0-beta.1 显示在主菜单左下角
[ ] 72hr 稳定：最近 72 小时无新增 P0 或可复现 P1
[ ] Bug Tracker：所有已知 bug 有编号、标签、assignee
[ ] 回滚包：至少 1 个上一稳定版本保存
[ ] 测试说明：beta_test_instructions 文档完整
[ ] 反馈入口：表单/频道可用
[ ] 隐私：无 Vivox/麦克风权限声明
[ ] 第三方授权：发布包内所有第三方资产有 license 记录
[ ] 崩溃报告：CrashReportHandler 已启用
[ ] 网络：Relay 区域设置正确，防火墙不影响连接
```

---

## 附录 A：目录结构建议

```
Builds/
├── macOS/
│   └── GanglandUndercover.app
├── Windows/
│   ├── GanglandUndercover.exe
│   └── GanglandUndercover_Data/
└── stable/                              # 保留最近 3 个稳定版本
    ├── GanglandUndercover_0.1.0-beta.0_macOS.zip
    └── GanglandUndercover_0.1.0-beta.0_Windows.zip

ci-logs/
└── YYYYMMDD_HHMMSS_{stage}.log

Assets/_Project/Scripts/
├── Build/
│   └── BuildScript.cs                   # 构建脚本
└── CI/
    └── CIRunner.cs                      # CI 自动化入口
```

## 附录 B：G 阶段依赖与前置条件

| 子阶段 | 前置依赖 | 前置完成状态 |
|--------|----------|-------------|
| G1 构建 | 阶段 0（基线冻结）、A4（clean machine） | 待完成 |
| G2 性能 | E3-E6（美术资产）、A2（多端完整局） | 待完成 |
| G3 CI | 阶段 0（编译/测试基线） | 待完成 |
| G4 封测 gate | A-G 全部前置 | 待完成 |
