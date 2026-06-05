#!/bin/bash
# Gangland Undercover — All Platforms Build
# 用法: ./build_all.sh [version]
# 按顺序构建 macOS → Windows，不中断

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

# 任一平台失败则整体退出码非零
if [ $MAC_EXIT -ne 0 ] || [ $WIN_EXIT -ne 0 ]; then
  exit 1
fi
exit 0
