#!/bin/bash
# Gangland Undercover — macOS Build Script
# 用法: ./build_macos.sh [version] [output_dir]
# 示例: ./build_macos.sh 0.1.0-beta.1 ./Builds

set -euo pipefail

VERSION="${1:-0.1.0-dev}"
OUTPUT_DIR="${2:-./Builds/macOS}"
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY="/Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity"
LOG_FILE="${PROJECT_DIR}/unity-build-macos.log"

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
