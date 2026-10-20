"""程序合成的临时音效：野猪（哼哼、尖叫、冲锋的蹄声、撞击、受伤、死亡）和椰子蟹（咔哒、嘶声、钳子夹合、受伤、死亡）。
用法：python Tools/AssetGen/synth_beasts.py Assets/Audio/Placeholder
"""
import os, sys, wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(808)


def save(name, x, peak=0.8):
    x = np.asarray(x, float)
    x = x / (np.abs(x).max() + 1e-9) * peak
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


def band(n, lo, hi):
    spec = np.fft.rfft(rng.normal(size=n)); f = np.fft.rfftfreq(n, 1 / SR)
    spec *= 1 / (1 + (lo / np.maximum(f, 1)) ** 4) / (1 + (f / hi) ** 4)
    return np.fft.irfft(spec, n)


def voice(sec, f0a, f0b, formants, rough=0.3, breath=0.3):
    n = int(SR * sec); t = np.arange(n) / SR
    f0 = np.linspace(f0a, f0b, n) * (1 + rough * band(n, 20, 80) / 3)
    saw = 2 * ((np.cumsum(f0 / SR)) % 1) - 1
    spec = np.fft.rfft(saw); f = np.fft.rfftfreq(n, 1 / SR)
    v = np.fft.irfft(sum(spec * g / (1 + ((f - fc) / bw) ** 2) for fc, bw, g in formants), n)
    v /= np.abs(v).max() + 1e-9
    return v + band(n, 300, 3000) / 5 * breath, t


def at(total, parts):
    x = np.zeros(int(SR * total))
    for start, y in parts:
        i = int(start * SR); x[i:i + len(y)] += y[:len(x) - i]
    return x


# ---- 野猪 ----
for v in range(2):  # 哼哼：几声短促的鼻音
    grunts = []
    for k in range(3 + v):
        g, t = voice(0.14, 110 + v * 15, 90, [(400, 120, 1), (900, 200, .4)], 0.6, 0.5)
        grunts.append((k * 0.22 + rng.uniform(0, 0.05), g * np.sin(np.pi * t / t[-1])))
    save(f"boar_idle_{v + 1}", at(1.0, grunts), 0.6)
x, t = voice(0.7, 700, 1100, [(1300, 250, 1), (2600, 400, .5)], 0.4)
save("boar_alert", x * np.minimum(1, t / 0.03) * np.exp(-t * 2.5), 0.8)
hooves = []
for k in range(10):  # 冲锋：越来越密的蹄声 + 一声尖叫
    L = int(SR * 0.05); h = band(L, 60, 600) * np.exp(-np.arange(L) / SR * 60)
    hooves.append((k * 0.11, h))
sq, t = voice(0.6, 800, 1300, [(1400, 300, 1), (2800, 500, .4)], 0.5)
hooves.append((0.2, sq * np.exp(-t * 2) * 0.8))
save("boar_charge", at(1.3, hooves), 0.85)
n = int(SR * 0.4); t = np.arange(n) / SR
save("boar_hit", band(n, 50, 900) * np.exp(-t * 14) + voice(0.4, 160, 110, [(450, 120, 1)], 0.8)[0] * np.exp(-t * 8) * 0.5, 0.9)
x, t = voice(0.45, 1100, 700, [(1500, 300, 1), (3000, 500, .4)], 0.5)
save("boar_hurt", x * np.exp(-t * 4), 0.8)
x, t = voice(1.6, 600, 150, [(1000, 250, 1), (2200, 400, .4)], 0.6)
save("boar_death", x * np.exp(-t * 1.6), 0.8)

# ---- 椰子蟹 ----
for v in range(2):  # 咔哒咔哒
    clicks = []
    for k in range(5):
        L = int(SR * 0.012); c = band(L, 2000, 8000) * np.exp(-np.arange(L) / SR * 500)
        clicks.append((k * rng.uniform(0.07, 0.16) + v * 0.03, c))
    save(f"ccrab_idle_{v + 1}", at(0.9, clicks), 0.5)
n = int(SR * 0.7); t = np.arange(n) / SR
save("ccrab_alert", band(n, 2500, 9000) * np.minimum(1, t / 0.05) * np.exp(-t * 3), 0.7)  # 嘶
snap = band(int(SR * 0.08), 800, 7000) * np.exp(-np.arange(int(SR * 0.08)) / SR * 90)
save("ccrab_attack", at(0.5, [(0.0, snap * 0.5), (0.18, snap)]), 0.9)  # 钳子张开再夹合
n = int(SR * 0.35); t = np.arange(n) / SR
save("ccrab_hurt", band(n, 1500, 8000) * np.exp(-t * 9) + band(n, 100, 800) * np.exp(-t * 20), 0.75)
n = int(SR * 1.2); t = np.arange(n) / SR
crack = band(n, 300, 5000) * np.exp(-t * 6)
save("ccrab_death", crack + at(1.2, [(0.3, snap * 0.4), (0.55, snap * 0.3)]), 0.8)
print("boar_* ccrab_*")
