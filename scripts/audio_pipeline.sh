#!/bin/bash
# Gangland Undercover 音频处理管线 v1.0
# 依赖: ffmpeg
# 用法: source audio_pipeline.sh 然后调用各函数

set -e

# === 配置 ===
FFMPEG="ffmpeg"
AUDIO_ROOT="${AUDIO_ROOT:-Assets/_Project/Audio}"

# === 核心函数 ===

# 标准化 SFX：mono 44.1kHz .ogg + 淡入淡出 + 响度标准化
normalize_sfx() {
    local input="$1"
    local output="$2"
    mkdir -p "$(dirname "$output")"
    $FFMPEG -i "$input" \
        -c:a libvorbis -ar 44100 -ac 1 \
        -af "afade=t=in:d=0.01,afade=t=out:d=0.03,loudnorm=I=-16:LRA=11:TP=-1.5" \
        -q:a 4 \
        "$output" -y 2>/dev/null
    echo "  ✓ SFX: $(basename "$output")"
}

# 标准化环境音/BGM：stereo 44.1kHz .ogg + 淡入淡出 + 响度标准化
normalize_ambience() {
    local input="$1"
    local output="$2"
    mkdir -p "$(dirname "$output")"
    $FFMPEG -i "$input" \
        -c:a libvorbis -ar 44100 -ac 2 \
        -af "afade=t=in:d=2,afade=t=out:d=2,loudnorm=I=-16:LRA=11:TP=-1.5" \
        -q:a 3 \
        "$output" -y 2>/dev/null
    echo "  ✓ Ambience: $(basename "$output")"
}

# 制作无缝循环段
make_loop() {
    local input="$1"
    local output="$2"
    local duration="${3:-120}"
    mkdir -p "$(dirname "$output")"
    $FFMPEG -i "$input" \
        -t "$duration" \
        -c:a libvorbis -ar 44100 -ac 2 \
        -af "afade=t=in:d=2,afade=t=out:d=2,loudnorm=I=-16:LRA=11:TP=-1.5" \
        -q:a 3 \
        "$output" -y 2>/dev/null
    echo "  ✓ Loop (${duration}s): $(basename "$output")"
}

# 裁剪音频段
trim_audio() {
    local input="$1"
    local output="$2"
    local start="${3:-0}"
    local duration="${4:-5}"
    mkdir -p "$(dirname "$output")"
    $FFMPEG -i "$input" \
        -ss "$start" -t "$duration" \
        -c:a libvorbis -ar 44100 -ac 1 \
        -af "afade=t=in:d=0.01,afade=t=out:d=0.03" \
        -q:a 4 \
        "$output" -y 2>/dev/null
    echo "  ✓ Trimmed: $(basename "$output")"
}

# 批量处理目录（所有音频文件 → 标准化 .ogg）
process_dir() {
    local src_dir="$1"
    local dst_dir="$2"
    mkdir -p "$dst_dir"
    local count=0
    for f in "$src_dir"/*.{wav,mp3,flac,aiff,WAV,MP3,FLAC,AIFF}; do
        [ -f "$f" ] || continue
        local name
        name=$(basename "${f%.*}")
        normalize_sfx "$f" "$dst_dir/${name}.ogg"
        count=$((count + 1))
    done
    echo "  ✓ Processed $count files: $dst_dir"
}

# 响度信息
loudness_info() {
    local input="$1"
    $FFMPEG -i "$input" -af "loudnorm=I=-16:LRA=11:TP=-1.5:print=summary" -f null - 2>&1 | grep -A 10 "Input Integrated"
}

# 格式信息
audio_info() {
    local input="$1"
    $FFMPEG -i "$input" 2>&1 | grep -E "(Duration|Audio|Stream)"
}

echo "Gangland Audio Pipeline v1.0 loaded"
echo "Available: normalize_sfx | normalize_ambience | make_loop | trim_audio | process_dir | loudness_info | audio_info"
