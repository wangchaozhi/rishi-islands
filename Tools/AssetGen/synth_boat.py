"""程序合成的临时音效：小艇（划桨、舷边的水声、浪拍船身、船底擦过沙滩、小马达启动和运转）。
用法：python Tools/AssetGen/synth_boat.py Assets/Audio/Placeholder
"""
import os, sys, wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(1505)


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


def env(n, attack, decay):
    t = np.arange(n) / SR
    return np.minimum(1, t / attack) * np.exp(-t * decay)


def loopable(x, fade=0.25):
    """首尾交叉淡化，循环播放时没有接缝。"""
    k = int(SR * fade)
    head, tail = x[:k], x[-k:]
    ramp = np.linspace(0, 1, k)
    out = x[k:].copy()
    out[-k:] = tail * (1 - ramp) + head * ramp
    return out


# 划桨：桨叶入水的「噗」+ 往后拉水的哗哗声 + 出水时的滴水（3 个变体）
for v in range(3):
    n = int(SR * 1.1)
    x = np.zeros(n)
    plop = band(int(SR * 0.12), 150, 900) * env(int(SR * 0.12), 0.004, 30)
    x[:len(plop)] += plop * 1.4
    pull = band(int(SR * 0.6), 400, 3500) * np.hanning(int(SR * 0.6)) * 0.6
    i = int(SR * 0.08); x[i:i + len(pull)] += pull
    for k in range(5 + v):
        j = int(SR * (0.7 + rng.random() * 0.35)); L = int(SR * 0.03)
        if j + L < n:
            x[j:j + L] += np.sin(2 * np.pi * (1200 + rng.random() * 1400) * np.arange(L) / SR) * env(L, 0.001, 120) * 0.25
    save(f"boat_oar_{v + 1}", x, 0.7)

# 浪拍船身：短促的「啪」，低频的空腔共鸣（4 个变体）
for v in range(4):
    n = int(SR * 0.5)
    slap = band(n, 120, 1800) * env(n, 0.003, 14 + v * 3)
    hull = np.sin(2 * np.pi * (85 + v * 12) * np.arange(n) / SR) * env(n, 0.005, 9) * 0.5
    save(f"boat_slap_{v + 1}", slap + hull, 0.7)

# 舷边的水声（循环）：船走动时水流过船舷的嘶嘶声，带一点起伏
n = int(SR * 6.25)
t = np.arange(n) / SR
water = band(n, 300, 2500) * (0.6 + 0.4 * np.sin(2 * np.pi * 0.35 * t) ** 2) + band(n, 60, 300) * 0.4
save("boat_water", loopable(water), 0.6)

# 船底擦过沙子 / 礁石：粗糙的摩擦 + 一声闷响
n = int(SR * 0.9)
scrape = band(n, 200, 4000) * env(n, 0.01, 4) * (1 + 0.5 * np.sin(2 * np.pi * 23 * np.arange(n) / SR))
thud = np.sin(2 * np.pi * 70 * np.arange(n) / SR) * env(n, 0.003, 12)
save("boat_scrape", scrape * 0.7 + thud, 0.8)

# 小马达运转（循环）：两冲程的突突声，基频约 32 Hz 的爆发脉冲 + 机械嗡鸣
n = int(SR * 4.25)
t = np.arange(n) / SR
f0 = 32
phase = (t * f0) % 1
pulse = np.exp(-phase * 18) * (1 + 0.15 * rng.normal(size=n))
pulse = np.convolve(pulse, np.hanning(64) / 32, "same")
whine = np.sin(2 * np.pi * f0 * 6 * t) * 0.12 + np.sin(2 * np.pi * f0 * 9.5 * t) * 0.05
rattle = band(n, 800, 5000) * 0.08 * (0.5 + pulse)
motor = pulse + whine + rattle
save("boat_motor", loopable(motor - motor.mean()), 0.7)

# 马达启动：拉启动绳的嗖嗖声，咳了两下，然后转起来
n = int(SR * 2.4)
x = np.zeros(n)
pull = band(int(SR * 0.35), 600, 4000) * np.hanning(int(SR * 0.35)) * 0.5
x[:len(pull)] += pull
for start, length in [(0.45, 0.18), (0.75, 0.22)]:
    i = int(SR * start); L = int(SR * length); tt = np.arange(L) / SR
    cough = np.exp(-((tt * 24) % 1) * 14) * env(L, 0.005, 6)
    x[i:i + L] += cough * 0.9
i = int(SR * 1.1); L = n - i; tt = np.arange(L) / SR
ramp = np.minimum(1, tt / 0.4)
run = np.exp(-((tt * (20 + 12 * ramp)) % 1) * 18) * ramp
x[i:] += run
save("boat_motor_start", x - x.mean(), 0.75)

print("boat_oar_1..3 boat_slap_1..4 boat_water boat_scrape boat_motor boat_motor_start")
