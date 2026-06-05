#!/bin/bash
# Gangland Undercover — Windows Build Script (cross-compile from macOS)
# 用法: ./build_windows.sh [version] [output_dir]
# 需要在 macOS 上安装 Windows IL2CPP 构建支持模块

set -euo pipefail

VERSION="${1:-0.1.0-dev}"
OUTPUT_DIR="${2:-./Builds/Windows}"
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY="/Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity"
LOG_FILE="${PROJECT_DIR}/unity-build-windows.log"

echo "=== Gangland Undercover Windows Build ==="
echo "Version: ${VERSION}"
echo "Output:  ${OUTPUT_DIR}"
echo "Project: ${PROJECT_DIR}"

mkdir -p "${OUTPUT_DIR}"

"${UNITY}" \
  -quit \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_DIR}" \
  -buildTarget StandaloneWindows64 \
  -executeMethod BuildScript.BuildWindows \
  -logFile "${LOG_FILE}" \
  -buildVersion "${VERSION}" \
  -buildOutputPath "${OUTPUT_DIR}"

BUILD_EXIT=$?

if [ $BUILD_EXIT -eq 0 ]; then
  echo ""
  echo "=== BUILD SUCCESS ==="
  echo "Output: ${OUTPUT_DIR}"
  ls -lh "${OUTPUT_DIR}/GanglandUndercover.exe" 2>/dev/null || \
    ls -lh "${OUTPUT_DIR}"/*.exe 2>/dev/null || \
    echo "Check ${OUTPUT_DIR} for build artifacts"
else
  echo ""
  echo "=== BUILD FAILED (exit code: ${BUILD_EXIT}) ==="
  echo "See ${LOG_FILE} for details"
  tail -n 50 "${LOG_FILE}"
fi

exit $BUILD_EXIT
