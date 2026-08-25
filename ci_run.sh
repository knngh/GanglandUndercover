#!/bin/bash
# Gangland Undercover — CI Pipeline (Single Script)
# 用法: ./ci_run.sh [--skip-build] [--skip-tests] [--version 0.1.0-dev]
# 退出码: 0=全部通过, 非0=某阶段失败
#
# 阶段:
#   1. Compile       — C# 编译检查
#   2. EditMode      — EditMode 单元测试
#   3. PlayMode      — PlayMode 集成测试
#   4. Build         — macOS + Windows 构建

set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY="/Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity"
CI_LOG_DIR="${PROJECT_DIR}/ci-logs"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
SKIP_BUILD=false
SKIP_TESTS=false
VERSION="0.1.0-dev"
FINAL_EXIT=0

# Parse flags
while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-build) SKIP_BUILD=true; shift ;;
    --skip-tests) SKIP_TESTS=true; shift ;;
    --version) VERSION="$2"; shift 2 ;;
    *) echo "Unknown flag: $1"; exit 1 ;;
  esac
done

mkdir -p "${CI_LOG_DIR}"

log_stage() {
  echo ""
  echo "========================================"
  echo "  STAGE: $1"
  echo "  Log:   ${CI_LOG_DIR}/${TIMESTAMP}_$2.log"
  echo "========================================"
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

# run_tests <platform> <log_name>
# 使用 Unity Test Framework 官方 -runTests 同步执行模式。
# 退出码: 0=全部通过, 2=有失败, 4=超时/取消。
# 修复记录 (2026-08-21): 原实现经 CIRunner.RunXxxTests + EditorApplication.update
# 等待回调，但 -quit 在异步测试启动后立即退出，导致测试从未执行、退出码恒 0
# （历史 59 份 ci-logs 均无结果行佐证）。本函数改用 -runTests 保证测试真实执行。
run_tests() {
  local platform="$1"
  local log_name="$2"
  local xml_path="${CI_LOG_DIR}/${TIMESTAMP}_${log_name}.xml"

  echo "  Running ${platform} tests (this can take several minutes)..."
  "${UNITY}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_DIR}" \
    -runTests \
    -testPlatform "${platform}" \
    -testResults "${xml_path}" \
    -logFile "${CI_LOG_DIR}/${TIMESTAMP}_${log_name}.log"
  local rc=$?
  if [ $rc -ne 0 ]; then
    return $rc
  fi
  # 汇总结果行（从 NUnit XML 提取）
  if [ -f "${xml_path}" ]; then
    local summary
    summary=$(grep -o 'result="[A-Za-z]*" total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' "${xml_path}" | head -1)
    echo "  Results: ${summary}"
    echo "  XML:     ${xml_path}"
  fi
  return 0
}

# ── Stage 1: Compile ──
log_stage "Compile" "compile"
if run_unity "CIRunner.Compile" "compile"; then
  echo "  ✅ Compile passed"
else
  echo "  ❌ Compile FAILED"
  FINAL_EXIT=1
fi

# ── Stage 2: EditMode Tests ──
if [ "$SKIP_TESTS" = false ] && [ $FINAL_EXIT -eq 0 ]; then
  log_stage "EditMode Tests" "editmode"
  if run_tests "EditMode" "editmode"; then
    echo "  ✅ EditMode tests passed"
  else
    echo "  ❌ EditMode tests FAILED (exit code above)"
    FINAL_EXIT=1
  fi
else
  echo "  ⏭  EditMode tests skipped"
fi

# ── Stage 3: PlayMode Tests ──
if [ "$SKIP_TESTS" = false ] && [ $FINAL_EXIT -eq 0 ]; then
  log_stage "PlayMode Tests" "playmode"
  if run_tests "PlayMode" "playmode"; then
    echo "  ✅ PlayMode tests passed"
  else
    echo "  ❌ PlayMode tests FAILED (exit code above)"
    FINAL_EXIT=1
  fi
else
  echo "  ⏭  PlayMode tests skipped"
fi

# ── Stage 4: Build ──
if [ "$SKIP_BUILD" = false ] && [ $FINAL_EXIT -eq 0 ]; then
  log_stage "Build" "build"
  if run_unity "CIRunner.BuildAll" "build" "-buildVersion ${VERSION} -buildOutputPath ${PROJECT_DIR}/Builds"; then
    echo "  ✅ Build passed"
  else
    echo "  ❌ Build FAILED"
    FINAL_EXIT=1
  fi
else
  echo "  ⏭  Build skipped"
fi

echo ""
if [ $FINAL_EXIT -eq 0 ]; then
  echo "========================================"
  echo "  CI PIPELINE PASSED"
  echo "  Logs: ${CI_LOG_DIR}/${TIMESTAMP}_*.log"
  echo "========================================"
else
  echo "========================================"
  echo "  CI PIPELINE FAILED"
  echo "  Check logs: ${CI_LOG_DIR}/${TIMESTAMP}_*.log"
  echo "========================================"
fi

exit $FINAL_EXIT
