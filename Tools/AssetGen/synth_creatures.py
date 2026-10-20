"""程序合成的临时音效：生物声音组（待机低吼、警觉尖啸、攻击咆哮、受伤惨叫、死亡），给生物框架的测试生物用。
用法：python Tools/AssetGen/synth_creatures.py Assets/Audio/Placeholder
正式音效到位后放进 Assets/Audio/Final，同名即可替换。
"""
import os, sys, wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(606)


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


def growl(sec, f0a, f0b, rough, formants):
    """喉咙里的吼声：抖动的锯齿波基频（粗糙感）+ 共振峰 + 气声。"""
    n = int(SR * sec)
    t = np.arange(n) / SR
    f0 = np.linspace(f0a, f0b, n) * (1 + rough * band(n, 15, 60) / 3)
    saw = 2 * ((np.cumsum(f0 / SR)) % 1) - 1
    spec = np.fft.rfft(saw); f = np.fft.rfftfreq(n, 1 / SR)
    shaped = sum(spec * g / (1 + ((f - fc) / bw) ** 2) for fc, bw, g in formants)
    v = np.fft.irfft(shaped, n)
    v /= np.abs(v).max() + 1e-9
    return v + band(n, 300, 4000) / 6 * 0.6, t


for v in range(2):
    x, t = growl(1.6, 55 + v * 8, 48 + v * 6, 0.5, [(300, 80, 1.0), (700, 120, 0.5)])
    save(f"creature_idle_{v + 1}", x * np.sin(np.pi * t / t[-1]) ** 1.5, 0.6)
x, t = growl(0.9, 300, 900, 0.3, [(1200, 200, 1.0), (2600, 300, 0.6)])
save("creature_alert", x * np.minimum(1, t / 0.05) * np.exp(-t * 1.5), 0.8)
for v in range(2):
    x, t = growl(0.55, 140 + v * 20, 90, 0.8, [(500, 120, 1.0), (1100, 200, 0.6), (2300, 300, 0.3)])
    save(f"creature_attack_{v + 1}", x * np.minimum(1, t / 0.02) * np.exp(-t * 3), 0.85)
x, t = growl(0.4, 700, 400, 0.4, [(1500, 250, 1.0), (3000, 400, 0.5)])
save("creature_hurt", x * np.minimum(1, t / 0.01) * np.exp(-t * 6), 0.8)
x, t = growl(2.2, 180, 40, 0.6, [(450, 100, 1.0), (900, 150, 0.4)])
save("creature_death", x * np.minimum(1, t / 0.03) * np.exp(-t * 1.2), 0.8)
print("creature_idle_1..2 creature_alert creature_attack_1..2 creature_hurt creature_death")
