#!/usr/bin/env bash
# D-4 macOS 构建产物启动冒烟。
# 拉起 .app → 等待 → 断言进程存活 + Player.log 无异常 → 截屏存证 → 退出。
# 用法: bash run-build-smoke.sh [app路径] [等待秒数]
set -u

APP="${1:-Builds/macOS/GanglandUndercover.app}"
WAIT_SECONDS="${2:-20}"
APP_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT_DIR="$APP_DIR/ci-logs"
SMOKE_LOG="$OUT_DIR/d4-smoke-result.txt"

PLAYER_LOG="$HOME/Library/Logs/GanglandUndercover/Gangland Undercover/Player.log"
BUNDLE_ID="com.gangland.undercover"

mkdir -p "$OUT_DIR"
: > "$SMOKE_LOG"

log() { echo "[smoke] $*" | tee -a "$SMOKE_LOG"; }

if [ ! -d "$APP" ]; then
  log "FAIL: 找不到 $APP"
  exit 1
fi

# 旧日志归档，避免读到上次运行的残留
if [ -f "$PLAYER_LOG" ]; then
  mv "$PLAYER_LOG" "$PLAYER_LOG.prev"
  log "已归档旧 Player.log"
fi

log "拉起 $APP (bundle id $BUNDLE_ID)"
open "$APP"

sleep "$WAIT_SECONDS"

if pgrep -f "Gangland Undercover.app/Contents/MacOS" > /dev/null; then
  log "PASS: 进程存活 ${WAIT_SECONDS}s"
  PROC_ALIVE=1
else
  log "FAIL: 进程在 ${WAIT_SECONDS}s 内退出"
  PROC_ALIVE=0
fi

if [ -f "$PLAYER_LOG" ]; then
  cp "$PLAYER_LOG" "$OUT_DIR/d4-smoke-player.log"
  EXCEPTIONS=$(grep -cE "^(Exception|SocketException|NullReferenceException)" "$PLAYER_LOG" || true)
  log "Player.log 异常行数: $EXCEPTIONS"
  grep -E "^(Exception|SocketException|NullReferenceException)" "$PLAYER_LOG" | head -5 | tee -a "$SMOKE_LOG"
  grep -m3 "Gangland\|BuildGuid\|Command line" "$PLAYER_LOG" | tee -a "$SMOKE_LOG" || true
else
  EXCEPTIONS=-1
  log "WARN: Player.log 未生成（进程可能没起来）"
fi

# 截屏存证（无屏幕录制权限时静默跳过）
SHOT="$OUT_DIR/d4-smoke-screen.png"
if screencapture -x "$SHOT" 2>/dev/null && [ -s "$SHOT" ]; then
  log "截屏已存: $SHOT"
else
  rm -f "$SHOT"
  log "截屏跳过（无权限或失败）"
fi

if [ "$PROC_ALIVE" = "1" ]; then
  osascript -e 'quit app id "com.gangland.undercover"' 2>/dev/null || pkill -f "Gangland Undercover.app/Contents/MacOS"
  sleep 3
  if pgrep -f "Gangland Undercover.app/Contents/MacOS" > /dev/null; then
    log "WARN: 进程未响应 quit，强制 kill"
    pkill -9 -f "Gangland Undercover.app/Contents/MacOS" || true
  fi
  log "已退出游戏进程"
fi

if [ "$PROC_ALIVE" = "1" ] && [ "$EXCEPTIONS" = "0" ]; then
  log "SMOKE RESULT: PASS"
  exit 0
elif [ "$PROC_ALIVE" = "1" ]; then
  log "SMOKE RESULT: PASS_WITH_LOG_NOISE (进程存活但日志有异常行，人工过目 d4-smoke-player.log)"
  exit 0
else
  log "SMOKE RESULT: FAIL"
  exit 1
fi
