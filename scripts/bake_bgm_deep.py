#!/usr/bin/env python3
"""
bake_bgm_deep.py — BGM 深化：三首多层合成曲目替换 4-8 秒占位循环

现状（2026-10-08 盘点）：BGM_InGame 4.2s / BGM_MainMenu 8.4s / BGM_Meeting 4.8s，
芯片风格单层循环。本脚本用 numpy 向量化合成升级为：

  MainMenu — 72 BPM, 16 小节 x2 = 106.7s，D 小调慢氛围（pad 和声 + 哑键琶音，
             第二遍加五声旋律 lead），港雨夜 noir 基调
  InGame   — 92 BPM, 16 小节 x2 = 83.5s，i-VI-III-VII 进行（Dm Dm Bb C Dm Dm Gm A），
             bass 根音八分 + kick/hat/snare 低调律动，第一遍减配第二遍全奏
  Meeting  — 60 BPM, 8 小节 x2 = 64s，心跳双 kick + 时钟 tick + 小二度紧张 pad，
             低音每两小节半音下行制造不安

设计约束：整曲整数小节、音色尾音在小节内自然衰减 -> 无缝循环；
软限幅 tanh；stereo pan 分轨；44100Hz 16bit -> ffmpeg libvorbis q4 ogg。
输出文件名与现有完全一致（.meta 不动，引用零破坏）。
用 /opt/homebrew/bin/python3（系统 numpy，只读使用）。
"""

import subprocess
import wave
from pathlib import Path

import numpy as np

SR = 44100
PROJECT = Path("/Users/zhugehao/projects/GanglandUndercover")
BGM_DIR = PROJECT / "Assets/_Project/Resources/Audio/BGM"
TMP = PROJECT / "output/art_batch4_audio"
TMP.mkdir(parents=True, exist_ok=True)

NOTE_SEMITONES = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4,
                  "F#": -3, "G": -2, "G#": -1, "A": 0, "A#": 1, "B": 2}


def note(name: str) -> float:
    """'D3' -> 频率（A4=440）。"""
    pitch, octave = name[:-1], int(name[-1])
    semis = NOTE_SEMITONES[pitch] + (octave - 4) * 12
    return 440.0 * (2 ** (semis / 12))


def _t(dur: float) -> np.ndarray:
    return np.arange(int(SR * dur)) / SR


def pluck(freq: float, dur: float, amp: float = 0.22) -> np.ndarray:
    """哑键竖琴感：4 泛音叠加 + 指数衰减 + 2ms attack。"""
    t = _t(dur)
    w = (1.00 * np.sin(2 * np.pi * freq * t)
         + 0.45 * np.sin(2 * np.pi * freq * 2 * t + 0.3)
         + 0.22 * np.sin(2 * np.pi * freq * 3 * t + 1.1)
         + 0.10 * np.sin(2 * np.pi * freq * 4 * t))
    env = np.exp(-t * 2.8)
    a = int(SR * 0.002)
    if a > 0:
        env[:a] *= np.linspace(0, 1, a)
    return amp * w * env


def pad(freq: float, dur: float, amp: float = 0.10) -> np.ndarray:
    """双层失谐正弦长音，慢攻击慢释放，正负循环点交叉淡化。"""
    t = _t(dur)
    w = (np.sin(2 * np.pi * freq * t) + np.sin(2 * np.pi * freq * 1.004 * t)
         + 0.5 * np.sin(2 * np.pi * freq * 2 * t)) / 2.5
    env = np.ones_like(t)
    a = int(SR * 0.6)
    r = int(SR * 0.9)
    if len(t) > a + r:
        env[:a] = np.linspace(0, 1, a)
        env[-r:] = np.linspace(1, 0, r)
    return amp * w * env


def bass(freq: float, dur: float, amp: float = 0.30) -> np.ndarray:
    """方波感低音：奇次泛音，高次衰减快（软低通）。"""
    t = _t(dur)
    w = (np.sin(2 * np.pi * freq * t)
         + 0.33 * np.sin(2 * np.pi * freq * 3 * t)
         + 0.14 * np.sin(2 * np.pi * freq * 5 * t)) / 1.47
    env = np.exp(-t * 3.2)
    a = int(SR * 0.004)
    if a > 0:
        env[:a] *= np.linspace(0, 1, a)
    return amp * w * env


def kick(amp: float = 0.55, dur: float = 0.35) -> np.ndarray:
    """110->45Hz 扫频正弦心跳鼓。"""
    t = _t(dur)
    f = 45 + 65 * np.exp(-t * 18)
    phase = 2 * np.pi * np.cumsum(f) / SR
    env = np.exp(-t * 9)
    return amp * np.sin(phase) * env


def hat(amp: float = 0.10, dur: float = 0.05) -> np.ndarray:
    """差分白噪声高通近似 + 快衰减。"""
    rng = np.random.default_rng(42)
    n = rng.standard_normal(int(SR * dur))
    n = np.diff(n, prepend=0)
    t = _t(dur)
    return amp * n * np.exp(-t * 90)


def snare(amp: float = 0.16, dur: float = 0.16) -> np.ndarray:
    """中频噪声短爆（会议紧张用 snare 感）。"""
    rng = np.random.default_rng(7)
    n = rng.standard_normal(int(SR * dur))
    t = _t(dur)
    body = 0.3 * np.sin(2 * np.pi * 185 * t)
    return amp * (n + body) * np.exp(-t * 26)


def tick(amp: float = 0.12) -> np.ndarray:
    """2kHz 时钟滴答。"""
    t = _t(0.03)
    return amp * np.sin(2 * np.pi * 2100 * t) * np.exp(-t * 120)


class Mix:
    """轨道布局：track(n_samples, stereo float)。"""

    def __init__(self, total_dur: float):
        self.buf = np.zeros((int(SR * total_dur), 2), dtype=np.float64)

    def add(self, sig: np.ndarray, at: float, pan: float = 0.0, gain: float = 1.0):
        """pan: -1 左 ~ +1 右。"""
        i = int(at * SR)
        j = min(i + len(sig), len(self.buf))
        if j <= i:
            return
        s = sig[i - i: j - i] * gain
        gl = np.sqrt(0.5 * (1 - pan))
        gr = np.sqrt(0.5 * (1 + pan))
        self.buf[i:j, 0] += s * gl * 1.4142
        self.buf[i:j, 1] += s * gr * 1.4142

    def render(self) -> np.ndarray:
        x = np.tanh(self.buf * 1.2) * 0.9  # 软限幅
        return (x * 32767).astype(np.int16)


def chord_notes(root: str, quality: str = "min") -> list:
    third = "min" in quality and "maj" not in quality
    iv = 3 if third else 4
    r = NOTE_SEMITONES[root[:-1]] + (int(root[-1]) - 4) * 12
    keys = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
    o = int(root[-1])
    return [root, keys[(r + iv) % 12] + str(o + (1 if r + iv >= 12 else 0)),
            keys[(r + 7) % 12] + str(o + (1 if r + 7 >= 12 else 0))]


def write_ogg(name: str, data: np.ndarray):
    wav_path = TMP / (name + ".wav")
    with wave.open(str(wav_path), "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(data.tobytes())
    out = BGM_DIR / (name + ".ogg")
    subprocess.run(["/opt/homebrew/bin/ffmpeg", "-y", "-loglevel", "error",
                    "-i", str(wav_path), "-c:a", "vorbis", "-strict", "experimental",
                    "-b:a", "160k", str(out)], check=True)
    print(f"  ✔ {name}.ogg ({out.stat().st_size // 1024} KB)")


# ──────────────────────────── 三首曲目 ────────────────────────────

def main_menu():
    """72 BPM, 32 小节, 106.7s。Dm Gm Bb A 循环，第二遍加旋律。"""
    bpm = 72
    beat = 60 / bpm
    bar = beat * 4
    total = bar * 32
    m = Mix(total)
    prog = [("D3", "min"), ("G2", "maj"), ("A#2", "maj"), ("A2", "maj")] * 8
    for bi, (root, q) in enumerate(prog):
        t0 = bi * bar
        # pad 和弦（根+3+5，中音区上八度）
        for n in chord_notes(root, q):
            m.add(pad(note(n) * 2, bar * 0.98, 0.085), t0, pan=0.0)
        # 哑键琶音：每小节 6 个八分音符，根-3-5-根(高)-5-3 分解
        seq = chord_notes(root, q)
        arp = [seq[0], seq[1], seq[2], seq[0], seq[2], seq[1]]
        octs = [2, 2, 2, 3, 2, 2]
        for k, (n, oc) in enumerate(zip(arp, octs)):
            m.add(pluck(note(n) * 2 ** (oc - 1), beat * 1.8, 0.16),
                  t0 + k * beat, pan=-0.35 + 0.15 * (k % 2))
    # 第二遍（小节 16 起）加五声 lead：D F G A C，稀疏长音
    lead = ["D5", "F5", "A5", "G5", "F5", "D5", "C5", "D5"]
    for bi in range(16, 32):
        if bi % 2 == 0:
            m.add(pluck(note(lead[(bi // 2) % 8]), beat * 5.5, 0.13),
                  bi * bar + beat * 0.5, pan=0.25)
    write_ogg("BGM_MainMenu", m.render())


def in_game():
    """92 BPM, 32 小节, 83.5s。Dm Dm Bb C Dm Dm Gm A，第二遍全奏。"""
    bpm = 92
    beat = 60 / bpm
    bar = beat * 4
    total = bar * 32
    m = Mix(total)
    prog = [("D2", "min"), ("D2", "min"), ("A#1", "maj"), ("C2", "maj"),
            ("D2", "min"), ("D2", "min"), ("G1", "maj"), ("A1", "maj")] * 4
    for bi, (root, q) in enumerate(prog):
        t0 = bi * bar
        full = bi >= 16  # 第二遍全奏
        # pad
        for n in chord_notes(root, q):
            m.add(pad(note(n) * 4, bar * 0.98, 0.07), t0, pan=0.0)
        # bass 八分根音
        for k in range(8):
            m.add(bass(note(root), beat * 0.9), t0 + k * beat / 2, pan=0.0)
        # 鼓组（全奏时加 hat/snare）
        m.add(kick(), t0)
        m.add(kick(0.42), t0 + beat * 2.5)
        if full:
            m.add(snare(), t0 + beat)
            m.add(snare(), t0 + beat * 3)
            for k in range(8):
                m.add(hat(), t0 + k * beat / 2 + beat / 4, pan=0.45)
        # 琶音点缀（全奏）
        if full and bi % 2 == 1:
            seq = chord_notes(root, q)
            for k, n in enumerate(seq):
                m.add(pluck(note(n) * 8, beat * 1.6, 0.10),
                      t0 + k * beat, pan=-0.5 + 0.25 * k)
    write_ogg("BGM_InGame", m.render())


def meeting():
    """60 BPM, 16 小节, 64s。心跳 kick + tick + 小二度紧张 pad + 半音下行低音。"""
    bpm = 60
    beat = 60 / bpm
    bar = beat * 4
    total = bar * 16
    m = Mix(total)
    roots = ["D1", "D1", "C#1", "C#1", "C1", "C1", "B0", "B0"] * 2
    for bi, root in enumerate(roots):
        t0 = bi * bar
        f = note(root)
        # 紧张 pad：小二度叠加（+半音）
        m.add(pad(f * 2, bar * 0.99, 0.075), t0, pan=-0.2)
        m.add(pad(f * 2 * 2 ** (1 / 12), bar * 0.99, 0.055), t0, pan=0.25)
        m.add(bass(f, bar * 0.95, 0.22), t0)
        # 心跳双 kick：小节头 boom-boom
        m.add(kick(0.5), t0 + beat * 0.0, pan=0.0)
        m.add(kick(0.38), t0 + beat * 0.5, pan=0.0)
        m.add(kick(0.44), t0 + beat * 2.0, pan=0.0)
        m.add(kick(0.33), t0 + beat * 2.5, pan=0.0)
        # 时钟 tick：每拍
        for k in range(4):
            m.add(tick(0.09 if k % 2 == 0 else 0.06), t0 + k * beat, pan=0.55)
        # 第二遍加 snare 转
        if bi >= 8:
            m.add(snare(0.10), t0 + beat * 3.5, pan=-0.3)
    write_ogg("BGM_Meeting", m.render())


def main():
    print("=" * 60)
    print("Gangland Undercover — BGM 深化烘焙（numpy 向量化合成）")
    print("=" * 60)
    main_menu()
    in_game()
    meeting()
    print("OK 3/3")


if __name__ == "__main__":
    main()
