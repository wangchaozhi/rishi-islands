"""程序合成的临时音效：手电筒开关的咔嗒声、装电池的声音。
用法：python Tools/AssetGen/synth_tools.py Assets/Audio/Placeholder
正式音效到位后放进 Assets/Audio/Final，同名即可替换。
"""
import os, sys, wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(303)


def save(name, x, peak=0.7):
    x = np.asarray(x, float)
    x = x / (np.abs(x).max() + 1e-9) * peak
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


def click(sec, f0, decay, noise=0.6):
    t = np.arange(int(SR * sec)) / SR
    tone = np.sin(2 * np.pi * f0 * t) * np.exp(-t * decay)
    n = rng.normal(size=len(t)) * np.exp(-t * decay * 2.5) * noise
    return tone + n


def place(x, y, at):
    i = int(at * SR)
    x[i:i + len(y)] += y[:len(x) - i]
    return x


# 开关：两下金属咔嗒（按下、弹回），间隔 40 毫秒
sw = np.zeros(int(SR * 0.16))
sw = place(sw, click(0.05, 3200, 180), 0.0)
sw = place(sw, click(0.05, 2500, 220, 0.4) * 0.6, 0.04)
save("flashlight_click", sw, 0.6)

# 装电池：拧开盖子的摩擦 + 电池滑进去 + 拧紧的咔哒
t = np.arange(int(SR * 0.7)) / SR
bat = np.zeros(len(t))
rub = rng.normal(size=int(SR * 0.22)) * np.hanning(int(SR * 0.22)) * 0.25
bat = place(bat, rub, 0.0)
slide = np.sin(2 * np.pi * 900 * np.arange(int(SR * 0.12)) / SR) * np.exp(-np.arange(int(SR * 0.12)) / SR * 30) * 0.4
bat = place(bat, slide + rng.normal(size=len(slide)) * 0.15 * np.hanning(len(slide)), 0.28)
bat = place(bat, click(0.06, 2800, 160), 0.55)
save("battery_insert", bat, 0.6)
print("flashlight_click.wav battery_insert.wav")

# 砍灌木：刀砍进枝叶的「唰」+ 树枝折断的脆响（3 个变体）；砍倒：一阵哗啦的枝叶倒地声
for v in range(3):
    n = int(SR * 0.45); t = np.arange(n) / SR
    rustle = np.fft.irfft(np.fft.rfft(rng.normal(size=n)) * (1 / (1 + (1500 / np.maximum(np.fft.rfftfreq(n, 1 / SR), 1)) ** 2)), n)
    rustle *= np.minimum(1, t / 0.01) * np.exp(-t * (7 + v))
    snap = np.zeros(n); i = int(SR * (0.03 + v * 0.015)); L = int(SR * 0.02)
    snap[i:i + L] = rng.normal(size=L) * np.exp(-np.arange(L) / SR * 300) * 1.5
    save(f"bush_chop_{v + 1}", rustle / np.abs(rustle).max() + snap, 0.75)
n = int(SR * 1.6); t = np.arange(n) / SR
fall = np.fft.irfft(np.fft.rfft(rng.normal(size=n)) * (1 / (1 + (900 / np.maximum(np.fft.rfftfreq(n, 1 / SR), 1)) ** 2)), n)
fall *= np.minimum(1, t / 0.08) * np.exp(-t * 2.2)
save("bush_fall", fall, 0.8)
print("bush_chop_1..3 bush_fall")
