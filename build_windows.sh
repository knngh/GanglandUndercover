#!/bin/bash
# Gangland Undercover — Windows/Steam PC Build Script
# 用法: ./build_windows.sh [version] [output_dir]
# 需要在 macOS 上安装 Unity 6000.4.9f1 的 Windows Build Support (Mono) 模块。

set -euo pipefail

VERSION="${1:-0.1.0-dev}"
OUTPUT_DIR="${2:-./Builds/SteamPC-$(date +%Y%m%d)}"
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity}"
LOG_DIR="${PROJECT_DIR}/Logs"
LOG_FILE="${LOG_FILE:-${LOG_DIR}/build-windows-steampc-$(date +%Y%m%d-%H%M%S).log}"
WINDOWS_DIR="${OUTPUT_DIR}/StandaloneWindows64"
PLAYER_PATH="${WINDOWS_DIR}/GanglandUndercover.exe"

unity_editor_root() {
  local unity_bin_dir
  local unity_contents_dir

  unity_bin_dir="$(cd "$(dirname "${UNITY}")" && pwd)"
  unity_contents_dir="$(cd "${unity_bin_dir}/.." && pwd)"
  cd "${unity_contents_dir}/../.." && pwd
}

windows_support_installed() {
  local editor_root="$1"
  local unity_contents_dir
  unity_contents_dir="$(cd "$(dirname "${UNITY}")/.." && pwd)"

  [ -d "${editor_root}/PlaybackEngines/WindowsStandaloneSupport" ] \
    || [ -d "${unity_contents_dir}/PlaybackEngines/WindowsStandaloneSupport" ]
}

echo "=== Gangland Undercover Windows/Steam PC Build ==="
echo "Version: ${VERSION}"
echo "Output:  ${OUTPUT_DIR}"
echo "Project: ${PROJECT_DIR}"

mkdir -p "${OUTPUT_DIR}" "${LOG_DIR}"

EDITOR_ROOT="$(unity_editor_root)"
if ! windows_support_installed "${EDITOR_ROOT}"; then
  {
    echo "[BuildScript] Build target unsupported: StandaloneWindows64."
    echo "[BuildScript] Install Windows Build Support (Mono) for Unity 6000.4.9f1 in Unity Hub."
    echo "[BuildScript] Checked:"
    echo "  ${EDITOR_ROOT}/PlaybackEngines/WindowsStandaloneSupport"
    echo "  $(cd "$(dirname "${UNITY}")/.." && pwd)/PlaybackEngines/WindowsStandaloneSupport"
  } | tee "${LOG_FILE}"
  exit 2
fi

set +e
"${UNITY}" \
  -quit \
  -batchmode \
  -projectPath "${PROJECT_DIR}" \
  -executeMethod GanglandUndercover.Editor.BuildScript.Build \
  -buildTarget "StandaloneWindows64" \
  -outputDir "${OUTPUT_DIR}" \
  -logFile "${LOG_FILE}" \
  -buildVersion "${VERSION}" \
  -accept-apiupdate

BUILD_EXIT=$?
set -e

if [ $BUILD_EXIT -eq 0 ]; then
  echo ""
  echo "=== BUILD SUCCESS ==="
  echo "Output: ${WINDOWS_DIR}"
  echo "Log:    ${LOG_FILE}"
  ls -lh "${PLAYER_PATH}" 2>/dev/null || echo "Check ${WINDOWS_DIR} for build artifacts"
  ls -lh "${WINDOWS_DIR}/SteamVisualArchive/MANIFEST.md" 2>/dev/null || true
else
  echo ""
  echo "=== BUILD FAILED (exit code: ${BUILD_EXIT}) ==="
  echo "See ${LOG_FILE} for details"
  tail -n 50 "${LOG_FILE}" || true
fi

exit $BUILD_EXIT
