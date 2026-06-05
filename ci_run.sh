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
  if run_unity "CIRunner.RunEditModeTests" "editmode"; then
    echo "  ✅ EditMode tests passed"
  else
    echo "  ❌ EditMode tests FAILED"
    FINAL_EXIT=1
  fi
else
  echo "  ⏭  EditMode tests skipped"
fi

# ── Stage 3: PlayMode Tests ──
if [ "$SKIP_TESTS" = false ] && [ $FINAL_EXIT -eq 0 ]; then
  log_stage "PlayMode Tests" "playmode"
  if run_unity "CIRunner.RunPlayModeTests" "playmode"; then
    echo "  ✅ PlayMode tests passed"
  else
    echo "  ❌ PlayMode tests FAILED"
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
