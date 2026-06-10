#!/usr/bin/env bash
# Gangland Undercover - Steam PC Windows closure runner.
#
# Produces one report covering EditMode, Windows player build, Steam visual
# archive attachment/fallback, zip packaging, size check, and sha256.

set -u

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity}"
VERSION="${VERSION:-0.1.0-dev}"
DATE_STAMP="$(date +%Y%m%d)"
TIME_STAMP="$(date +%Y%m%d-%H%M%S)"
BUILD_ROOT="${BUILD_ROOT:-${PROJECT_DIR}/Builds/SteamPC-${DATE_STAMP}}"
LOG_DIR="${PROJECT_DIR}/Logs"
WINDOWS_DIR="${BUILD_ROOT}/StandaloneWindows64"
PLAYER_PATH="${WINDOWS_DIR}/GanglandUndercover.exe"
ARCHIVE_MANIFEST="${WINDOWS_DIR}/SteamVisualArchive/MANIFEST.md"
ZIP_PATH="${BUILD_ROOT}/GanglandUndercover-SteamPC-Windows-${TIME_STAMP}.zip"
REPORT_PATH="${BUILD_ROOT}/WINDOWS_CLOSURE_REPORT.md"
TARGET_ZIP_BYTES=$((600 * 1024 * 1024))

RUN_TESTS=true
RUN_ZIP=true

usage() {
  cat <<'USAGE'
Usage: bash build_steampc_windows_closure.sh [options]

Options:
  --version VALUE       Build version label, default: 0.1.0-dev
  --output-dir PATH     Build root, default: Builds/SteamPC-YYYYMMDD
  --skip-tests          Skip EditMode test run
  --no-zip              Skip zip packaging and sha256
  -h, --help            Show this help

Exit codes:
  0   Full Windows closure passed: tests, exe, archive, zip size, sha256.
  1   Tests or packaging failed.
  2   Windows build is blocked; report is written and prior archive evidence may exist.
USAGE
}

while [ $# -gt 0 ]; do
  case "$1" in
    --version)
      VERSION="$2"
      shift 2
      ;;
    --output-dir)
      BUILD_ROOT="$2"
      WINDOWS_DIR="${BUILD_ROOT}/StandaloneWindows64"
      PLAYER_PATH="${WINDOWS_DIR}/GanglandUndercover.exe"
      ARCHIVE_MANIFEST="${WINDOWS_DIR}/SteamVisualArchive/MANIFEST.md"
      ZIP_PATH="${BUILD_ROOT}/GanglandUndercover-SteamPC-Windows-${TIME_STAMP}.zip"
      REPORT_PATH="${BUILD_ROOT}/WINDOWS_CLOSURE_REPORT.md"
      shift 2
      ;;
    --skip-tests)
      RUN_TESTS=false
      shift
      ;;
    --no-zip)
      RUN_ZIP=false
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1"
      usage
      exit 1
      ;;
  esac
done

EDITMODE_STATUS="NOT_RUN"
EDITMODE_LOG="${LOG_DIR}/steampc-windows-editmode-${TIME_STAMP}.log"
EDITMODE_XML="${LOG_DIR}/steampc-windows-editmode-${TIME_STAMP}.xml"
BUILD_STATUS="NOT_RUN"
BUILD_LOG="${LOG_DIR}/build-windows-steampc-${TIME_STAMP}.log"
ARCHIVE_STATUS="NOT_RUN"
ARCHIVE_LOG="${LOG_DIR}/export-steam-visual-archive-${TIME_STAMP}.log"
ZIP_STATUS="NOT_RUN"
ZIP_BYTES="0"
ZIP_SHA256=""
CLOSURE_STATUS="NOT_RUN"
EXIT_CODE=0
PLAYER_FRESH=false
ARCHIVE_FRESH=false
WINDOWS_SUPPORT_STATUS="NOT_CHECKED"

mkdir -p "${BUILD_ROOT}" "${LOG_DIR}"

unity_editor_root() {
  local unity_bin_dir
  local unity_contents_dir

  unity_bin_dir="$(cd "$(dirname "${UNITY}")" && pwd)"
  unity_contents_dir="$(cd "${unity_bin_dir}/.." && pwd)"
  cd "${unity_contents_dir}/../.." && pwd
}

unity_contents_dir() {
  cd "$(dirname "${UNITY}")/.." && pwd
}

windows_support_installed() {
  local editor_root="$1"
  local contents_dir="$2"

  [ -d "${editor_root}/PlaybackEngines/WindowsStandaloneSupport" ] \
    || [ -d "${contents_dir}/PlaybackEngines/WindowsStandaloneSupport" ]
}

write_windows_support_log() {
  local editor_root="$1"
  local contents_dir="$2"

  {
    echo "[BuildScript] Build target unsupported: StandaloneWindows64."
    echo "[BuildScript] Install Windows Build Support (Mono) for Unity 6000.4.9f1 in Unity Hub."
    echo "[BuildScript] Checked:"
    echo "  ${editor_root}/PlaybackEngines/WindowsStandaloneSupport"
    echo "  ${contents_dir}/PlaybackEngines/WindowsStandaloneSupport"
  } | tee "${BUILD_LOG}"
}

run_editmode_tests() {
  if [ "${RUN_TESTS}" != true ]; then
    EDITMODE_STATUS="SKIPPED"
    return 0
  fi

  echo "=== EditMode tests ==="
  "${UNITY}" \
    -runTests \
    -batchmode \
    -projectPath "${PROJECT_DIR}" \
    -testPlatform EditMode \
    -testResults "${EDITMODE_XML}" \
    -logFile "${EDITMODE_LOG}" \
    -accept-apiupdate
  local test_exit=$?

  if [ "${test_exit}" -eq 0 ]; then
    EDITMODE_STATUS="PASSED"
    return 0
  fi

  EDITMODE_STATUS="FAILED exit=${test_exit}"
  return "${test_exit}"
}

run_windows_build() {
  echo "=== Windows player build ==="
  LOG_FILE="${BUILD_LOG}" bash "${PROJECT_DIR}/build_windows.sh" "${VERSION}" "${BUILD_ROOT}"
  local build_exit=$?

  if [ "${build_exit}" -eq 0 ] && [ -f "${PLAYER_PATH}" ]; then
    BUILD_STATUS="PASSED"
    PLAYER_FRESH=true
    if [ -f "${ARCHIVE_MANIFEST}" ]; then
      ARCHIVE_STATUS="ATTACHED_BY_BUILD"
      ARCHIVE_FRESH=true
    else
      ARCHIVE_STATUS="MISSING_AFTER_BUILD"
    fi
    return 0
  fi

  if [ "${build_exit}" -eq 0 ]; then
    BUILD_STATUS="FAILED missing_player"
  elif [ "${build_exit}" -eq 2 ]; then
    BUILD_STATUS="BLOCKED missing_windows_support"
    return 2
  else
    BUILD_STATUS="FAILED exit=${build_exit}"
  fi
  return 1
}

export_visual_archive_fallback() {
  echo "=== Steam visual archive fallback ==="
  "${UNITY}" \
    -quit \
    -batchmode \
    -projectPath "${PROJECT_DIR}" \
    -executeMethod GanglandUndercover.Editor.BuildScript.ExportSteamVisualArchive \
    -outputDir "${BUILD_ROOT}" \
    -logFile "${ARCHIVE_LOG}" \
    -accept-apiupdate
  local archive_exit=$?

  if [ "${archive_exit}" -eq 0 ] && [ -f "${ARCHIVE_MANIFEST}" ]; then
    ARCHIVE_STATUS="FALLBACK_EXPORTED"
    ARCHIVE_FRESH=true
    return 0
  fi

  if [ "${archive_exit}" -eq 0 ]; then
    ARCHIVE_STATUS="FAILED missing_manifest"
  else
    ARCHIVE_STATUS="FAILED exit=${archive_exit}"
  fi
  return 1
}

package_zip() {
  if [ "${RUN_ZIP}" != true ]; then
    ZIP_STATUS="SKIPPED"
    return 0
  fi

  if [ ! -d "${WINDOWS_DIR}" ]; then
    ZIP_STATUS="FAILED missing_windows_dir"
    return 1
  fi

  if [ "${PLAYER_FRESH}" != true ] && [ "${ARCHIVE_FRESH}" != true ]; then
    ZIP_STATUS="SKIPPED no_fresh_windows_artifact"
    return 1
  fi

  echo "=== Zip package ==="
  rm -f "${ZIP_PATH}"
  if command -v ditto >/dev/null 2>&1; then
    ditto -c -k --sequesterRsrc --keepParent "${WINDOWS_DIR}" "${ZIP_PATH}"
  else
    (cd "${BUILD_ROOT}" && zip -qr "${ZIP_PATH}" "$(basename "${WINDOWS_DIR}")")
  fi

  if [ ! -f "${ZIP_PATH}" ]; then
    ZIP_STATUS="FAILED missing_zip"
    return 1
  fi

  ZIP_BYTES="$(stat -f%z "${ZIP_PATH}" 2>/dev/null || stat -c%s "${ZIP_PATH}" 2>/dev/null || echo 0)"
  ZIP_SHA256="$(shasum -a 256 "${ZIP_PATH}" | awk '{print $1}')"

  if [ "${ZIP_BYTES}" -ge "${TARGET_ZIP_BYTES}" ]; then
    ZIP_STATUS="PASSED"
    return 0
  fi

  ZIP_STATUS="FAILED below_600MiB"
  return 1
}

write_report() {
  local player_exists="no"
  local archive_exists="no"
  local zip_exists="no"
  local archive_total="not available"

  [ -f "${PLAYER_PATH}" ] && player_exists="yes"
  [ -f "${ARCHIVE_MANIFEST}" ] && archive_exists="yes"
  [ -f "${ZIP_PATH}" ] && zip_exists="yes"

  if [ -f "${ARCHIVE_MANIFEST}" ]; then
    archive_total="$(grep -E '^Total copied bytes:' "${ARCHIVE_MANIFEST}" 2>/dev/null || echo "not available")"
  fi

  cat > "${REPORT_PATH}" <<REPORT
# Gangland Undercover - Steam PC Windows Closure Report

Date: ${TIME_STAMP}
Version: ${VERSION}
Unity: ${UNITY}
Project: ${PROJECT_DIR}
Build root: ${BUILD_ROOT}

## Status

- Closure: ${CLOSURE_STATUS}
- Windows support module: ${WINDOWS_SUPPORT_STATUS}
- EditMode: ${EDITMODE_STATUS}
- Windows build: ${BUILD_STATUS}
- Steam visual archive: ${ARCHIVE_STATUS}
- Zip package: ${ZIP_STATUS}

## Artifacts

- Player exe exists: ${player_exists}
- Player produced this run: ${PLAYER_FRESH}
- Player exe: ${PLAYER_PATH}
- Archive manifest exists: ${archive_exists}
- Archive produced this run: ${ARCHIVE_FRESH}
- Archive manifest: ${ARCHIVE_MANIFEST}
- Archive total: ${archive_total}
- Zip exists: ${zip_exists}
- Zip: ${ZIP_PATH}
- Zip bytes: ${ZIP_BYTES}
- Zip sha256: ${ZIP_SHA256}

## Logs

- EditMode log: ${EDITMODE_LOG}
- EditMode XML: ${EDITMODE_XML}
- Windows build log: ${BUILD_LOG}
- Archive fallback log: ${ARCHIVE_LOG}

## Interpretation

- PASS means the Windows executable, Steam visual archive, zip package, and 600 MiB size gate are all present.
- PARTIAL_BLOCKED means the runnable Windows executable was not produced; check the artifact freshness fields before using archive or zip evidence.
- On this Mac, PARTIAL_BLOCKED usually means Unity 6000.4.9f1 is missing Windows Build Support (Mono).
REPORT

  echo "Report: ${REPORT_PATH}"
}

echo "=== Gangland Undercover Steam PC Windows closure ==="
echo "Version: ${VERSION}"
echo "Build root: ${BUILD_ROOT}"
echo "Unity: ${UNITY}"

if [ ! -x "${UNITY}" ]; then
  echo "Unity editor not found or not executable: ${UNITY}"
  EDITMODE_STATUS="BLOCKED missing_unity"
  BUILD_STATUS="BLOCKED missing_unity"
  ARCHIVE_STATUS="BLOCKED missing_unity"
  ZIP_STATUS="NOT_RUN"
  CLOSURE_STATUS="BLOCKED"
  EXIT_CODE=1
  write_report
  exit "${EXIT_CODE}"
fi

EDITOR_ROOT="$(unity_editor_root)"
UNITY_CONTENTS_DIR="$(unity_contents_dir)"
if ! windows_support_installed "${EDITOR_ROOT}" "${UNITY_CONTENTS_DIR}"; then
  WINDOWS_SUPPORT_STATUS="MISSING"
  EDITMODE_STATUS="SKIPPED missing_windows_support"
  BUILD_STATUS="BLOCKED missing_windows_support"
  ARCHIVE_STATUS="SKIPPED missing_windows_support"
  ZIP_STATUS="SKIPPED missing_windows_support"
  CLOSURE_STATUS="PARTIAL_BLOCKED"
  EXIT_CODE=2
  write_windows_support_log "${EDITOR_ROOT}" "${UNITY_CONTENTS_DIR}"
  write_report
  exit "${EXIT_CODE}"
fi

WINDOWS_SUPPORT_STATUS="INSTALLED"

if ! run_editmode_tests; then
  CLOSURE_STATUS="FAILED_TESTS"
  EXIT_CODE=1
  write_report
  exit "${EXIT_CODE}"
fi

if run_windows_build; then
  if [ "${ARCHIVE_STATUS}" = "MISSING_AFTER_BUILD" ]; then
    export_visual_archive_fallback || true
  fi
else
  build_result=$?
  if [ "${build_result}" -eq 2 ]; then
    ARCHIVE_STATUS="SKIPPED missing_windows_support"
  else
    export_visual_archive_fallback || true
  fi
  EXIT_CODE=2
fi

if ! package_zip; then
  if [ "${EXIT_CODE}" -eq 0 ]; then
    EXIT_CODE=1
  fi
fi

if [ "${EXIT_CODE}" -eq 0 ] \
  && [ -f "${PLAYER_PATH}" ] \
  && [ -f "${ARCHIVE_MANIFEST}" ] \
  && { [ "${RUN_ZIP}" != true ] || [ "${ZIP_STATUS}" = "PASSED" ]; }; then
  CLOSURE_STATUS="PASS"
else
  if [ "${EXIT_CODE}" -eq 2 ]; then
    CLOSURE_STATUS="PARTIAL_BLOCKED"
  else
    CLOSURE_STATUS="FAILED"
  fi
fi

write_report
exit "${EXIT_CODE}"
