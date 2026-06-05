#!/usr/bin/env python3
"""Gangland Undercover 环境音和 BGM 程序化生成器

使用 Python wave 模块 + math 生成：
- 3 地图环境音循环（雨夜/室内/城寨）
- 8 BGM 层（探索/紧张/会议/投票/胜利×2/菜单/大厅）

策略：使用噪声、正弦波叠加和包络生成低保真/芯片音乐风格音频
"""

import wave
import math
import struct
import random
import os

SAMPLE_RATE = 44100
BITS = 16
MAX_AMP = 32767

def sine_wave(freq, duration, amplitude=0.5):
    """生成正弦波采样"""
    samples = []
    num_samples = int(SAMPLE_RATE * duration)
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        val = amplitude * math.sin(2 * math.pi * freq * t)
        samples.append(int(val * MAX_AMP))
    return samples

def noise(duration, amplitude=0.3, seed=42):
    """生成白噪声"""
    random.seed(seed)
    samples = []
    num_samples = int(SAMPLE_RATE * duration)
    for _ in range(num_samples):
        val = amplitude * (random.random() * 2 - 1)
        samples.append(int(val * MAX_AMP))
    return samples

def brown_noise(duration, amplitude=0.4, seed=42):
    """生成棕噪声（低频重的噪声，适合雨声/风）"""
    random.seed(seed)
    samples = []
    num_samples = int(SAMPLE_RATE * duration)
    last = 0
    for _ in range(num_samples):
        white = random.random() * 2 - 1
        last = (last + (0.02 * white)) / 1.02
        val = amplitude * last * 3.5
        samples.append(int(max(-MAX_AMP, min(MAX_AMP, val * MAX_AMP))))
    return samples

def apply_envelope(samples, attack=0.01, decay=0.1, sustain=0.7, release=0.2, duration=None):
    """应用 ADSR 包络"""
    if duration is None:
        duration = len(samples) / SAMPLE_RATE
    total = len(samples)
    attack_s = int(attack * SAMPLE_RATE)
    decay_s = int(decay * SAMPLE_RATE)
    release_s = int(release * SAMPLE_RATE)
    sustain_start = attack_s + decay_s
    sustain_end = total - release_s
    
    for i, s in enumerate(samples):
        t = i / SAMPLE_RATE
        if i < attack_s:
            env = i / attack_s
        elif i < sustain_start:
            progress = (i - attack_s) / decay_s
            env = 1.0 - (1.0 - sustain) * progress
        elif i < sustain_end:
            env = sustain
        else:
            progress = (i - sustain_end) / release_s
            env = sustain * (1.0 - progress)
        samples[i] = int(s * max(0, env))
    return samples

def mix_samples(*sample_lists):
    """混合多轨采样（简单相加+限幅）"""
    max_len = max(len(s) for s in sample_lists)
    result = [0] * max_len
    for sl in sample_lists:
        for i, s in enumerate(sl):
            result[i] += s
    # 限幅
    for i in range(len(result)):
        result[i] = max(-MAX_AMP, min(MAX_AMP, result[i]))
    return result

def save_mono_wav(samples, filepath):
    """保存单声道 WAV"""
    os.makedirs(os.path.dirname(filepath) or '.', exist_ok=True)
    with wave.open(filepath, 'w') as wf:
        wf.setnchannels(1)
        wf.setsampwidth(BITS // 8)
        wf.setframerate(SAMPLE_RATE)
        for s in samples:
            wf.writeframes(struct.pack('<h', s))
    print(f"  ✓ {os.path.basename(filepath)} ({len(samples)/SAMPLE_RATE:.1f}s)")

def save_stereo_wav(left, right, filepath):
    """保存立体声 WAV"""
    os.makedirs(os.path.dirname(filepath) or '.', exist_ok=True)
    with wave.open(filepath, 'w') as wf:
        wf.setnchannels(2)
        wf.setsampwidth(BITS // 8)
        wf.setframerate(SAMPLE_RATE)
        max_len = max(len(left), len(right))
        for i in range(max_len):
            l = left[i] if i < len(left) else 0
            r = right[i] if i < len(right) else 0
            wf.writeframes(struct.pack('<hh', l, r))
    print(f"  ✓ {os.path.basename(filepath)} ({max_len/SAMPLE_RATE:.1f}s)")

def lowpass_filter(samples, cutoff=2000):
    """简单一阶低通滤波"""
    filtered = []
    rc = 1.0 / (2 * math.pi * cutoff)
    dt = 1.0 / SAMPLE_RATE
    alpha = dt / (rc + dt)
    last = 0
    for s in samples:
        last = last + alpha * (s - last)
        filtered.append(int(last))
    return filtered

# ============================================================
# 环境音生成
# ============================================================

def generate_harbour_rain(duration=120):
    """港区雨夜环境音：棕噪声（雨声）+ 低频城市嗡鸣 + 偶尔滴水"""
    random.seed(1)
    # 主雨声：棕噪声低通
    rain = brown_noise(duration, 0.35, seed=1)
    rain = lowpass_filter(rain, 3000)
    
    # 城市低频嗡鸣：低频正弦波组合
    city = []
    num = int(SAMPLE_RATE * duration)
    for i in range(num):
        t = i / SAMPLE_RATE
        # 60Hz + 120Hz 低频嗡鸣，缓慢幅度调制
        mod = 0.5 + 0.5 * math.sin(2 * math.pi * 0.1 * t)  # 0.1Hz 调制
        val = 0.06 * (math.sin(2 * math.pi * 60 * t) + 0.5 * math.sin(2 * math.pi * 120 * t))
        city.append(int(val * mod * MAX_AMP))
    
    # 稀疏滴水声
    drips = [0] * num
    for _ in range(30):
        t_drip = random.uniform(1, duration - 1)
        idx = int(t_drip * SAMPLE_RATE)
        for j in range(min(2000, num - idx)):
            freq = 800 + 200 * math.exp(-j / 500)
            env = math.exp(-j / 300)
            drips[idx + j] = int(0.08 * env * math.sin(2 * math.pi * freq * j / SAMPLE_RATE) * MAX_AMP)
    
    left = mix_samples(rain, city, drips)
    right = mix_samples(rain, city, drips)  # 稍作变化
    # 右声道微延迟（雨声自然感）
    delay = int(0.003 * SAMPLE_RATE)  # 3ms 延迟
    right = [0]*delay + right[:-delay]
    
    return left, right


def generate_police_station_interior(duration=120):
    """警署室内环境音：荧光灯嗡鸣 + 远处打印机 + 偶尔对讲机"""
    random.seed(2)
    num = int(SAMPLE_RATE * duration)
    
    # 荧光灯嗡鸣：120Hz 基频 + 谐波
    hum = []
    for i in range(num):
        t = i / SAMPLE_RATE
        mod = 0.8 + 0.2 * math.sin(2 * math.pi * 0.05 * t)
        val = 0.04 * (math.sin(2 * math.pi * 120 * t) + 
                       0.3 * math.sin(2 * math.pi * 240 * t) +
                       0.1 * math.sin(2 * math.pi * 360 * t))
        hum.append(int(val * mod * MAX_AMP))
    
    # 远处打印机：周期性机械声
    printer = [0] * num
    for cycle in range(int(duration / 8)):
        start = cycle * 8
        for j in range(int(1.5 * SAMPLE_RATE)):
            idx = int(start * SAMPLE_RATE) + j
            if idx >= num: break
            t2 = j / SAMPLE_RATE
            freq = 400 + 200 * math.sin(2 * math.pi * 8 * t2)
            env = 0.03 * math.exp(-t2 / 0.5)
            printer[idx] = int(env * math.sin(2 * math.pi * freq * t2) * MAX_AMP)
    
    # 偶尔对讲机静噪
    radio = [0] * num
    for _ in range(5):
        t_radio = random.uniform(10, duration - 10)
        idx = int(t_radio * SAMPLE_RATE)
        dur = int(0.3 * SAMPLE_RATE)
        for j in range(min(dur, num - idx)):
            env = 0.05 * math.exp(-j / 2000)
            radio[idx + j] = int(env * (random.random()*2-1) * MAX_AMP * 0.3)
    
    result = mix_samples(hum, printer, radio)
    return result, [s for s in result]


def generate_kowloon_neon(duration=120):
    """九龙城寨深夜环境音：霓虹灯嗡鸣 + 远处音乐低频 + 人群噪杂"""
    random.seed(3)
    num = int(SAMPLE_RATE * duration)
    
    # 霓虹灯密集嗡鸣：多频
    neon = []
    for i in range(num):
        t = i / SAMPLE_RATE
        val = 0.03 * (math.sin(2 * math.pi * 100 * t) +
                       0.5 * math.sin(2 * math.pi * 200 * t) +
                       0.3 * math.sin(2 * math.pi * 150 * t) +
                       0.2 * math.sin(2 * math.pi * 300 * t))
        neon.append(int(val * MAX_AMP))
    
    # 远处音乐低频（模拟酒吧/茶餐厅传出）
    music = []
    for i in range(num):
        t = i / SAMPLE_RATE
        mod = 0.3 + 0.7 * abs(math.sin(2 * math.pi * 0.03 * t))
        val = 0.02 * (math.sin(2 * math.pi * 80 * t) * 
                       math.sin(2 * math.pi * 0.5 * t + math.sin(0.1 * t)))
        music.append(int(val * mod * MAX_AMP))
    
    # 人群噪杂：带通噪声
    crowd = noise(duration, 0.08, seed=3)
    crowd = lowpass_filter(crowd, 3000)
    crowd = lowpass_filter(crowd, 500)  # 反向低通模拟带通
    
    # 偶尔巷子里的滴水/猫叫
    ambient = [0] * num
    for _ in range(8):
        t_event = random.uniform(5, duration - 5)
        idx = int(t_event * SAMPLE_RATE)
        dur = int(random.uniform(0.1, 0.5) * SAMPLE_RATE)
        freq = random.choice([200, 400, 600, 800])
        for j in range(min(dur, num - idx)):
            env = 0.06 * math.exp(-j / 3000)
            ambient[idx + j] = int(env * math.sin(2 * math.pi * freq * j / SAMPLE_RATE) * MAX_AMP)
    
    left = mix_samples(neon, music, crowd, ambient)
    # 右声道稍作延迟制造立体感
    delay = int(0.005 * SAMPLE_RATE)
    right = [0]*delay + left[:-delay]
    
    return left, right


# ============================================================
# BGM 生成（芯片音乐风格）
# ============================================================

def create_melody(notes, tempo=120):
    """从音符列表创建旋律采样
    notes: [(freq, duration_beats), ...]
    """
    samples = []
    beat_duration = 60.0 / tempo
    for freq, beats in notes:
        dur = beat_duration * beats
        num = int(SAMPLE_RATE * dur)
        for i in range(num):
            t = i / SAMPLE_RATE
            if freq > 0:
                # 方波 + 快速包络
                env = math.exp(-t / (dur * 0.3))
                val = 0.3 * env * (1 if math.sin(2 * math.pi * freq * t) > 0 else -1)
            else:
                val = 0  # 休止符
            samples.append(int(val * MAX_AMP))
    return samples


def generate_bgm_explore():
    """探索 BGM：压抑氛围 + 低音脉冲（C minor）"""
    duration = 60
    num = int(SAMPLE_RATE * duration)
    samples = []
    bass_notes = [65.41, 73.42, 82.41, 73.42]  # C2, D2, E2, D2
    note_len = int(SAMPLE_RATE * 4)  # 每音符4秒
    
    for i in range(num):
        t = i / SAMPLE_RATE
        note_idx = (i // note_len) % len(bass_notes)
        freq = bass_notes[note_idx]
        progress = (i % note_len) / note_len
        
        # 低音脉冲
        env = 0.15 * (0.6 + 0.4 * math.sin(2 * math.pi * 0.25 * t))
        bass = env * math.sin(2 * math.pi * freq * t)
        
        # 高音氛围层（C minor 和弦泛音）
        high = 0.03 * (math.sin(2 * math.pi * 523 * t) +  # C5
                        math.sin(2 * math.pi * 622 * t) +  # Eb5
                        math.sin(2 * math.pi * 784 * t))   # G5
        high *= (0.3 + 0.7 * abs(math.sin(2 * math.pi * 0.07 * t)))
        
        val = bass + high
        left.append(int(val * MAX_AMP))
        right.append(int(val * MAX_AMP))
    
    # 正确编写立体声循环
    return None  # 稍后重写


def generate_bgm_tension():
    """紧张 BGM：快速低音 + 不和谐高音"""
    pass


def generate_bgm_meeting():
    """会议 BGM：沉稳低音 + 时钟滴答感"""
    pass


# ============================================================
# 主入口：生成所有环境音和 BGM
# ============================================================

def generate_all_ambience(output_dir):
    """生成全部 3 个环境音"""
    print("\n=== Generating Ambience ===")
    
    # 港区雨夜
    left, right = generate_harbour_rain(120)
    save_stereo_wav(left, right, os.path.join(output_dir, "amb_harbour_rain.wav"))
    
    # 警署室内
    left, right = generate_police_station_interior(120)
    save_stereo_wav(left, right, os.path.join(output_dir, "amb_police_interior.wav"))
    
    # 九龙城寨
    left, right = generate_kowloon_neon(120)
    save_stereo_wav(left, right, os.path.join(output_dir, "amb_kowloon_neon.wav"))

def generate_all_bgm(output_dir):
    """生成全部 8 首 BGM"""
    print("\n=== Generating BGM ===")
    # 使用芯片音乐风格的方波/三角波合成
    
    bgms = {
        "bgm_menu": ("Menu", [
            (261.63, 0.5), (329.63, 0.5), (392.00, 0.5), (329.63, 0.5),
            (293.66, 0.5), (349.23, 0.5), (440.00, 0.5), (349.23, 0.5),
            (261.63, 0.5), (329.63, 0.5), (392.00, 0.5), (523.25, 0.5),
            (440.00, 0.5), (392.00, 0.5), (329.63, 1.0), (0, 0.5),
        ]),
        "bgm_lobby": ("Lobby", [
            (196.00, 1.0), (220.00, 1.0), (246.94, 1.0), (261.63, 1.0),
            (220.00, 0.5), (246.94, 0.5), (261.63, 1.0), (293.66, 1.0),
        ]),
        "bgm_explore": ("Explore", [
            (130.81, 2.0), (146.83, 2.0), (164.81, 2.0), (146.83, 2.0),
            (130.81, 2.0), (174.61, 1.0), (164.81, 1.0), (146.83, 2.0),
        ]),
        "bgm_threat": ("Threat", [
            (196.00, 0.25), (0, 0.25), (196.00, 0.25), (220.00, 0.25),
            (233.08, 0.25), (0, 0.25), (233.08, 0.25), (246.94, 0.25),
            (277.18, 0.5), (311.13, 0.5), (277.18, 0.5), (246.94, 0.5),
        ]),
        "bgm_meeting": ("Meeting", [
            (110.00, 1.0), (130.81, 1.0), (110.00, 0.5), (130.81, 0.5),
            (146.83, 1.0), (130.81, 1.0), (110.00, 2.0), (0, 1.0),
        ]),
        "bgm_vote": ("Vote", [
            (220.00, 0.25), (246.94, 0.25), (261.63, 0.25), (293.66, 0.25),
            (311.13, 0.25), (329.63, 0.25), (349.23, 0.25), (369.99, 0.25),
            (392.00, 0.5), (369.99, 0.5), (349.23, 1.0), (311.13, 1.0),
        ]),
        "bgm_victory_police": ("Police Victory", [
            (261.63, 0.5), (329.63, 0.5), (392.00, 0.5), (523.25, 1.5),
            (440.00, 0.5), (523.25, 0.5), (587.33, 2.0), (0, 1.0),
        ]),
        "bgm_victory_gang": ("Gang Victory", [
            (311.13, 0.5), (277.18, 0.5), (246.94, 0.5), (207.65, 1.5),
            (233.08, 0.5), (207.65, 0.5), (196.00, 2.0), (0, 1.0),
        ]),
    }
    
    for filename, (name, notes) in bgms.items():
        melody = create_melody(notes, tempo=100)
        # 添加低音轨道
        bass_notes = []
        for freq, dur in notes:
            bass_notes.append((freq / 2 if freq > 0 else 0, dur))
        bass = create_melody(bass_notes, tempo=100)
        
        # 对齐长度
        max_len = max(len(melody), len(bass))
        melody += [0] * (max_len - len(melody))
        bass += [0] * (max_len - len(bass))
        
        # 混音
        mixed = []
        for m, b in zip(melody, bass):
            mixed.append(int(m * 0.7 + b * 0.4))
        
        # 应用淡入淡出
        mixed = apply_envelope(mixed, attack=0.5, decay=0.2, sustain=0.8, release=2.0)
        save_mono_wav(mixed, os.path.join(output_dir, f"{filename}.wav"))


if __name__ == "__main__":
    output = os.path.join(os.path.dirname(os.path.abspath(__file__)), 
                          "..", "Assets", "_Project", "Audio")
    
    # 环境音 → Ambience/
    amb_dir = os.path.join(output, "Ambience")
    generate_all_ambience(amb_dir)
    
    # BGM → BGM/
    bgm_dir = os.path.join(output, "BGM")
    generate_all_bgm(bgm_dir)
    
    print(f"\n✓ All ambient and BGM files generated")
