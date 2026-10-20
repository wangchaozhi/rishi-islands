"""程序合成的临时音效：影嗣的声音组。
- shade_whisper：若有若无的低语（可循环），几个听不清内容的气声在耳边交错
- shade_scream：被光照到时撕裂般的尖啸
- shade_alert / shade_bite / shade_hurt / shade_death
用法：python Tools/AssetGen/synth_shade.py Assets/Audio/Placeholder
"""
import os, sys, wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(707)


def save(name, x, peak=0.8):
    x = np.asarray(x, float)
    x = x / (np.abs(x).max() + 1e-9) * peak
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


def formant_noise(n, formants):
    """气声：白噪声经过共振峰，像没有声带振动的耳语。"""
    spec = np.fft.rfft(rng.normal(size=n)); f = np.fft.rfftfreq(n, 1 / SR)
    shaped = sum(spec * g / (1 + ((f - fc) / bw) ** 2) for fc, bw, g in formants)
    x = np.fft.irfft(shaped, n)
    return x / (np.abs(x).max() + 1e-9)


VOWELS = [[(700, 120, 1), (1200, 150, .6), (2600, 250, .3)], [(300, 80, 1), (2300, 200, .6), (3000, 250, .3)],
          [(450, 100, 1), (800, 120, .7), (2500, 250, .3)], [(350, 90, 1), (900, 120, .6), (2400, 250, .3)]]

# 低语：8 秒循环，几个声音各自说着一串听不清的音节（绕回开头，保证无缝）
N = int(SR * 8)
whisper = np.zeros(N)
for voice in range(4):
    t0 = rng.uniform(0, 8)
    gain = rng.uniform(0.4, 1.0)
    for _ in range(int(rng.integers(10, 18))):
        L = int(SR * rng.uniform(0.08, 0.25))
        syl = formant_noise(L, VOWELS[rng.integers(0, 4)]) * np.hanning(L) ** 0.7
        if rng.random() < 0.4:  # 偶尔的「嘶」
            s = formant_noise(L // 2, [(5000, 1500, 1)]) * np.hanning(L // 2)
            syl[:len(s)] += s * 0.6
        at = int((t0 % 8) * SR)
        whisper[(at + np.arange(L)) % N] += syl * gain
        t0 += rng.uniform(0.12, 0.55)
bed = formant_noise(N, [(500, 300, 1), (2500, 800, .4)]) * 0.12
save("shade_whisper", whisper + bed, 0.55)

# 尖啸：几层不协和的高频锯齿 + 撕裂的噪声，音高往上挣
n = int(SR * 1.6)
t = np.arange(n) / SR
scream = np.zeros(n)
for f0 in (620, 890, 1310):
    f = f0 * (1 + 0.35 * t / t[-1]) * (1 + 0.02 * np.sin(2 * np.pi * 23 * t))
    scream += (2 * ((np.cumsum(f / SR)) % 1) - 1) * 0.4
scream += formant_noise(n, [(3500, 1200, 1)]) * 0.7
scream *= np.minimum(1, t / 0.02) * np.exp(-t * 1.6)
save("shade_scream", scream, 0.85)

# 警觉：一口急促的倒吸气
n = int(SR * 0.6); t = np.arange(n) / SR
save("shade_alert", formant_noise(n, [(1800, 600, 1), (4000, 900, .6)]) * np.sin(np.pi * t / t[-1]) ** 0.5 * np.linspace(0.3, 1, n), 0.7)

# 撕咬：两个变体，短促的嘶吼 + 咬合的咔嚓
for v in range(2):
    n = int(SR * 0.45); t = np.arange(n) / SR
    snarl = formant_noise(n, [(400 + v * 80, 150, 1), (1100, 250, .7), (3000, 500, .4)]) * np.minimum(1, t / 0.02) * np.exp(-t * 6)
    click = np.zeros(n); i = int(SR * 0.12); L = int(SR * 0.015)
    click[i:i + L] = rng.normal(size=L) * np.exp(-np.arange(L) / SR * 400) * 2
    save(f"shade_bite_{v + 1}", snarl + click, 0.85)

# 受伤：短促尖叫
n = int(SR * 0.5); t = np.arange(n) / SR
f = 900 * (1 - 0.3 * t / t[-1])
hurt = (2 * ((np.cumsum(f / SR)) % 1) - 1) * 0.5 + formant_noise(n, [(2800, 900, 1)]) * 0.6
save("shade_hurt", hurt * np.minimum(1, t / 0.01) * np.exp(-t * 5), 0.8)

# 死亡：尖叫往下沉，散成一阵风
n = int(SR * 2.5); t = np.arange(n) / SR
f = 800 * np.exp(-t * 1.2) + 60
death = (2 * ((np.cumsum(f / SR)) % 1) - 1) * np.exp(-t * 2) * 0.5 + formant_noise(n, [(700, 500, 1)]) * np.exp(-((t - 1.2) ** 2) / 0.4) * 0.8
save("shade_death", death, 0.8)
print("shade_whisper shade_scream shade_alert shade_bite_1..2 shade_hurt shade_death")
