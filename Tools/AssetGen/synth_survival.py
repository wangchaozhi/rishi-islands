"""程序合成的临时音效：篝火噼啪（可循环）、点火、受伤痛哼、重伤喘息（可循环）、倒下。
用法：python Tools/AssetGen/synth_survival.py Assets/Audio/Placeholder
正式音效到位后放进 Assets/Audio/Final，同名即可替换。
"""
import os, sys, wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(404)


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


def formant_voice(t, f0, formants, breath=0.4):
    """粗略的人声：锯齿波基频经过几个共振峰带通，再混一点气声。"""
    n = len(t)
    phase = np.cumsum(f0 / SR)
    saw = 2 * (phase % 1) - 1
    spec = np.fft.rfft(saw); f = np.fft.rfftfreq(n, 1 / SR)
    shaped = np.zeros_like(spec)
    for fc, bw, g in formants:
        shaped += spec * g / (1 + ((f - fc) / bw) ** 2)
    v = np.fft.irfft(shaped, n)
    v /= np.abs(v).max() + 1e-9
    return v + band(n, 400, 5000) / 4 * breath


# 篝火：低沉的呼呼声 + 随机的噼啪（绕回开头保证循环无缝）
N = int(SR * 8)
fire = band(N, 60, 700) * 0.5 + band(N, 1500, 6000) * 0.08
pops = np.zeros(N)
for _ in range(260):
    at = rng.integers(0, N)
    L = int(SR * rng.uniform(0.002, 0.012))
    p = rng.normal(size=L) * np.exp(-np.arange(L) / SR * rng.uniform(300, 900)) * rng.uniform(0.3, 1.0)
    pops[(at + np.arange(L)) % N] += p
fire = fire / np.abs(fire).max() + pops / np.abs(pops).max() * 0.9
save("fire_crackle", fire, 0.7)

# 点火：一下呼的燃起
t = np.arange(int(SR * 1.2)) / SR
whoosh = band(len(t), 150, 3500) * np.minimum(1, t / 0.08) * np.exp(-t * 2.5)
save("fire_ignite", whoosh, 0.75)

# 受伤痛哼：三个变体，基频下滑
for v in range(3):
    t = np.arange(int(SR * rng.uniform(0.35, 0.5))) / SR
    f0 = rng.uniform(120, 150) * (1 - 0.25 * t / t[-1])
    voice = formant_voice(t, f0, [(600, 90, 1.0), (1000, 120, 0.6), (2500, 200, 0.25)], 0.5)
    envl = np.minimum(1, t / 0.03) * np.exp(-t * 5)
    save(f"player_hurt_{v + 1}", voice * envl, 0.75)

# 重伤喘息：粗重的一呼一吸，约 1.4 秒一个循环
N = int(SR * 5.6)
t = np.arange(N) / SR
cycle = (t % 1.4) / 1.4
inhale = np.clip(np.sin(np.pi * np.clip(cycle / 0.45, 0, 1)), 0, 1) ** 1.5
exhale = np.clip(np.sin(np.pi * np.clip((cycle - 0.5) / 0.45, 0, 1)), 0, 1) ** 1.5
breath = band(N, 500, 3500) * inhale * 0.7 + band(N, 250, 2200) * exhale
save("breath_hurt", breath, 0.6)

# 倒下：一声长长的呻吟 + 身体倒地的闷响
t = np.arange(int(SR * 1.8)) / SR
f0 = 130 * (1 - 0.35 * t / t[-1])
groan = formant_voice(t, f0, [(550, 80, 1.0), (900, 110, 0.5), (2400, 220, 0.2)], 0.6) * np.minimum(1, t / 0.05) * np.exp(-t * 1.6)
thud_t = np.arange(int(SR * 0.4)) / SR
thud = band(len(thud_t), 40, 300) * np.exp(-thud_t * 12)
death = np.zeros(int(SR * 2.4))
death[:len(groan)] += groan
i = int(SR * 1.1)
death[i:i + len(thud)] += thud * 1.5
save("player_death", death, 0.8)
print("fire_crackle fire_ignite player_hurt_1..3 breath_hurt player_death")
