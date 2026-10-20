"""程序合成的临时音效：雨声（可无缝循环），给动态天气用。
用法：python Tools/AssetGen/synth_weather.py Assets/Audio/Placeholder
正式音效到位后放进 Assets/Audio/Final，同名即可替换。
"""
import os, sys, wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(1203)
SEC = 10.0
N = int(SR * SEC)


def save(name, x, peak=0.8):
    x = np.asarray(x, float)
    x = x / (np.abs(x).max() + 1e-9) * peak
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


def band(lo, hi, tilt=0.0):
    """整段长度的带通噪声；在频域里生成，首尾相接，可以无缝循环。"""
    spec = np.fft.rfft(rng.normal(size=N)); f = np.fft.rfftfreq(N, 1 / SR)
    spec *= 1 / (1 + (lo / np.maximum(f, 1)) ** 4) / (1 + (f / hi) ** 4)
    spec *= np.maximum(f, 20) ** (-tilt)
    return np.fft.irfft(spec, N)


# 雨幕：中高频的沙沙声 + 低频的闷响
hiss = band(1200, 9000)
body = band(150, 1500, 0.3)
rain = hiss / np.abs(hiss).max() + body / np.abs(body).max() * 0.45

# 近处的雨滴：随机散布的短促滴答（绕回开头，保证循环无缝）
drops = np.zeros(N)
for _ in range(1400):
    at = rng.integers(0, N)
    f0 = rng.uniform(1800, 5200)
    L = int(SR * rng.uniform(0.004, 0.02))
    t = np.arange(L) / SR
    d = np.sin(2 * np.pi * f0 * t) * np.exp(-t * rng.uniform(250, 600)) * rng.uniform(0.2, 1.0)
    idx = (at + np.arange(L)) % N
    drops[idx] += d
rain += drops / np.abs(drops).max() * 0.35
save("amb_rain", rain, 0.7)
print("amb_rain.wav")
