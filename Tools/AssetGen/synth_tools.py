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
