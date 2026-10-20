"""程序合成的临时音效：瀑布轰鸣（可无缝循环）。
用法：python Tools/AssetGen/synth_waterfall.py Assets/Audio/Placeholder
正式音效到位后放进 Assets/Audio/Final，同名即可替换。
"""
import os, sys, wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(2026)
SEC = 8.0
N = int(SR * SEC)


def save(name, x, peak=0.8):
    x = np.asarray(x, float)
    x = x / (np.abs(x).max() + 1e-9) * peak
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


def band(lo, hi, tilt=0.0):
    """整段长度的带通噪声；在频域里生成，天然首尾相接，可以无缝循环。"""
    spec = np.fft.rfft(rng.normal(size=N)); f = np.fft.rfftfreq(N, 1 / SR)
    spec *= 1 / (1 + (lo / np.maximum(f, 1)) ** 4) / (1 + (f / hi) ** 4)
    spec *= np.maximum(f, 20) ** (-tilt)
    return np.fft.irfft(spec, N)


def slow(cycles):
    """缓慢起伏的包络，周期是整段长度的整数分之一，保证循环无缝。"""
    t = np.arange(N) / N
    return sum(np.sin(2 * np.pi * (c * t + rng.random())) / (i + 1) for i, c in enumerate(cycles))


roar = band(40, 900, 0.5)                       # 低沉的轰鸣
body = band(300, 4000, 0.2)                      # 水流的主体
hiss = band(3000, 12000)                         # 水花的嘶嘶声
roar *= 1 + 0.15 * slow([3, 7])
body *= 1 + 0.25 * slow([5, 11, 17])
hiss *= 1 + 0.35 * slow([9, 23, 31])
mix = roar / np.abs(roar).max() * 1.0 + body / np.abs(body).max() * 0.55 + hiss / np.abs(hiss).max() * 0.18
save("amb_waterfall", mix, 0.75)
print("amb_waterfall.wav")
