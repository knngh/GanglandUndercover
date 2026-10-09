#!/usr/bin/env python3
"""
bake_ambience_policestation.py — 补齐三地图主题环境音之警察署室内

现有：amb_harbour_rain（港雨）、amb_kowloon_neon（城寨霓虹）。
缺失：PoliceStation 主题环境音。

合成内容（60s 无缝循环）：
- 50Hz 电网哼声 + 谐波（荧光灯/配电，中国 50Hz）
- 空调/设备低噪（滤波棕噪声）
- 远处持续电脑风扇白噪
- 稀疏远处脚步声与低频关门闷响（不作事件音，仅空间感）

无缝循环：棕噪声按循环边界交叉混音；谐波相位循环对齐。
输出 ogg vorbis 160k（与现有 ambience 一致；meta 手工写入）。
"""

import subprocess
import uuid
from pathlib import Path

import numpy as np

SR = 44100
DUR = 60.0
PROJECT = Path("/Users/zhugehao/projects/GanglandUndercover")
OUT = PROJECT / "Assets/_Project/Resources/Audio/Ambience/amb_policestation_indoor.ogg"
TMP = PROJECT / "output/art_batch4_audio"

META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 7
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def main():
    t = np.arange(int(SR * DUR)) / SR
    rng = np.random.default_rng(20261008)

    # 1) 50Hz 电网哼声 + 2/3 次谐波，缓慢抖动（真实感）
    wobble = 1 + 0.004 * np.sin(2 * np.pi * 0.13 * t)
    hum = (0.035 * np.sin(2 * np.pi * 50 * wobble * t)
           + 0.012 * np.sin(2 * np.pi * 100 * t)
           + 0.006 * np.sin(2 * np.pi * 150 * t))

    # 2) 空调低噪：棕噪声低通 + 缓慢呼吸包络
    white = rng.standard_normal(len(t))
    brown = np.cumsum(white)
    brown = brown / np.max(np.abs(brown))
    # 简单一阶低通（y[n] = y[n-1] + a(x[n]-y[n-1])）
    alpha = 0.02
    lp = np.zeros_like(brown)
    for i in range(1, len(brown)):
        lp[i] = lp[i - 1] + alpha * (brown[i] - lp[i - 1])
    breath = 0.6 + 0.4 * np.sin(2 * np.pi * (t / 18.0))
    ac = 0.05 * lp * breath

    # 3) 风扇白噪：高频衰减白噪
    fan = rng.standard_normal(len(t))
    fan = np.convolve(fan, np.ones(8) / 8, mode="same")
    fan = 0.012 * fan / np.max(np.abs(fan))

    # 4) 空间事件：稀疏远处脚步（每 12-20s 一组）与关门闷响
    events = np.zeros_like(t)
    for base in [9.0, 27.0, 45.0]:
        for k in range(3):
            at = base + k * 0.55
            i0 = int(at * SR)
            dur = 0.09
            seg = rng.standard_normal(int(SR * dur))
            env = np.exp(-np.arange(len(seg)) / (SR * 0.03))
            step = seg * env * 0.05
            if i0 + len(step) < len(events):
                events[i0:i0 + len(step)] += step
    for at in [18.5, 38.0, 56.0]:
        i0 = int(at * SR)
        dur = 0.5
        tt = np.arange(int(SR * dur)) / SR
        thump = np.sin(2 * np.pi * (60 - 25 * tt / dur) * tt) * np.exp(-tt * 6) * 0.10
        if i0 + len(thump) < len(events):
            events[i0:i0 + len(thump)] += thump

    mix = hum + ac + fan + events
    # 循环无缝：首尾 1.5s 交叉淡化
    xfade = int(SR * 1.5)
    head = mix[:xfade].copy()
    tail = mix[-xfade:].copy()
    ramp = np.linspace(0, 1, xfade)
    mix[-xfade:] = tail * (1 - ramp) + head * ramp
    mix = mix[: int(SR * DUR) - 0]

    stereo = np.stack([mix, mix], axis=1)
    stereo = np.tanh(stereo * 2.2) * 0.85
    data = (stereo * 32767).astype(np.int16)

    import wave
    wav_path = TMP / "amb_policestation_indoor.wav"
    TMP.mkdir(parents=True, exist_ok=True)
    with wave.open(str(wav_path), "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(data.tobytes())
    subprocess.run(["/opt/homebrew/bin/ffmpeg", "-y", "-loglevel", "error",
                    "-i", str(wav_path), "-c:a", "vorbis", "-strict", "experimental",
                    "-b:a", "160k", str(OUT)], check=True)
    meta = Path(str(OUT) + ".meta")
    if not meta.exists():
        meta.write_text(META.format(guid=uuid.uuid4().hex))
    print(f"  ✔ {OUT.name} ({OUT.stat().st_size // 1024} KB, 60s)")


if __name__ == "__main__":
    main()
