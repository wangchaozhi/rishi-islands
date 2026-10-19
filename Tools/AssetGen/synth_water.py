"""程序合成的临时音效：游泳、潜水、氧气、敲击声、背包、砍刀、鱼枪、贝壳 / 砗磲、水下环境声。
用法：python Tools/AssetGen/synth_water.py Assets/Audio/Placeholder
正式音效到位后放进 Assets/Audio/Final，同名即可替换。
"""
import os, sys, wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(99)


def save(name, x, peak=0.85):
    x = np.asarray(x, float)
    x = x / (np.abs(x).max() + 1e-9) * peak
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


def t_(sec):
    return np.arange(int(SR * sec)) / SR


def band(n, lo, hi):
    spec = np.fft.rfft(rng.normal(size=n)); f = np.fft.rfftfreq(n, 1 / SR)
    spec *= 1 / (1 + (lo / np.maximum(f, 1)) ** 4) / (1 + (f / hi) ** 4)
    return np.fft.irfft(spec, n)


def env(t, attack, decay):
    return np.minimum(1, t / max(attack, 1e-4)) * np.exp(-np.maximum(t - attack, 0) * decay)


def bubble(t, f0, decay):
    # 气泡：频率上扬的衰减正弦
    return np.sin(2 * np.pi * (f0 * t + f0 * 1.6 * t * t)) * np.exp(-t * decay)


def mix_at(x, y, at):
    i = int(at * SR)
    n = min(len(y), len(x) - i)
    if n > 0: x[i:i + n] += y[:n]
    return x


def bubbles_burst(sec, count, lo=400, hi=1400, vol=0.5):
    x = np.zeros(int(SR * sec))
    for _ in range(count):
        tb = t_(rng.uniform(0.04, 0.12))
        mix_at(x, bubble(tb, rng.uniform(lo, hi), rng.uniform(25, 60)) * rng.uniform(0.3, 1) * vol, rng.uniform(0, sec - 0.12))
    return x


# ---------- 水 ----------
t = t_(0.9)
save("splash_enter", band(len(t), 200, 7000) * env(t, 0.01, 6) + bubbles_burst(0.9, 20) * 0.6)
t = t_(1.6)
save("splash_big", band(len(t), 80, 6000) * env(t, 0.01, 3.5) * 1.3 + bubbles_burst(1.6, 45, 250, 1100))
t = t_(0.35)
save("splash_small", band(len(t), 600, 9000) * env(t, 0.003, 18) + bubbles_burst(0.35, 6, 900, 2000) * 0.4, 0.6)
t = t_(1.2)
drips = np.zeros(len(t))
for k in range(9):
    mix_at(drips, bubble(t_(0.05), rng.uniform(1200, 2400), 80) * 0.4, rng.uniform(0.2, 1.1))
save("water_exit", band(len(t), 300, 5000) * env(t, 0.02, 5) * 0.8 + drips, 0.6)
for v in range(3):
    t = t_(0.8)
    stroke = band(len(t), 250, 4500) * np.sin(np.pi * np.clip(t / 0.55, 0, 1)) ** 2
    save(f"swim_stroke_{v + 1}", stroke + bubbles_burst(0.8, 4, 500, 1200) * 0.3, 0.5)
    t = t_(0.9)
    save(f"swim_under_{v + 1}", band(len(t), 60, 700) * np.sin(np.pi * np.clip(t / 0.7, 0, 1)) ** 2 + bubbles_burst(0.9, 3, 200, 600) * 0.3, 0.45)
t = t_(0.8)
save("dive_under", band(len(t), 80, 2500) * env(t, 0.01, 5) + bubbles_burst(0.8, 25, 200, 900), 0.6)
t = t_(0.7)
save("surface_breach", band(len(t), 400, 8000) * env(t, 0.005, 7) + bubbles_burst(0.7, 6, 800, 1600) * 0.3, 0.6)
for v in range(3):
    save(f"bubbles_{v + 1}", bubbles_burst(1.2, 14 + v * 4, 300, 1000), 0.5)

# 喘气：吸气的气流噪声 + 喉音
for v in range(2):
    t = t_(1.1)
    breath = band(len(t), 500, 5000) * np.sin(np.pi * np.clip(t / 0.75, 0, 1)) ** 1.5
    voice = np.sin(2 * np.pi * (180 + 40 * t) * t) * np.sin(np.pi * np.clip(t / 0.5, 0, 1)) * 0.15
    save(f"gasp_{v + 1}", breath + voice, 0.7)

# 心跳（循环）
t = t_(1.0)
hb = np.zeros(len(t))
for at, amp in ((0.0, 1.0), (0.28, 0.7)):
    tt = t_(0.2)
    mix_at(hb, np.sin(2 * np.pi * 48 * tt) * np.exp(-tt * 22) * amp, at)
save("heartbeat", hb, 0.9)

# 三下敲击：隔着水和铁皮传过来，低沉、带金属余音
t = t_(3.2)
k3 = np.zeros(len(t))
for i in range(3):
    tt = t_(1.4)
    knock = band(len(tt), 60, 900) * np.exp(-tt * 30) + (np.sin(2 * np.pi * 180 * tt) + 0.5 * np.sin(2 * np.pi * 431 * tt)) * np.exp(-tt * 3.5) * 0.35
    mix_at(k3, knock, 0.25 + i * 0.62)
save("knock_three", k3, 0.9)

t = t_(3.0)
save("drown", band(len(t), 50, 1500) * env(t, 0.3, 0.8) + bubbles_burst(3.0, 70, 150, 700), 0.8)

# 水下环境声（循环）：低频嗡鸣 + 远处的气泡 + 礁石上虾群的噼啪声
t = t_(12.0)
amb = band(len(t), 20, 300) * 0.8
amb += np.sin(2 * np.pi * 55 * t) * 0.05 * (1 + 0.3 * np.sin(2 * np.pi * 0.07 * t))
snap = np.zeros(len(t))
for _ in range(700):
    mix_at(snap, band(80, 3000, 12000) * np.exp(-np.arange(80) / SR * 900) * rng.uniform(0.05, 0.25), rng.uniform(0, 11.9))
amb += snap
amb += bubbles_burst(12.0, 25, 150, 500) * 0.3
fade = int(SR * 0.5)
amb[:fade] *= np.linspace(0, 1, fade); amb[-fade:] *= np.linspace(1, 0, fade)
amb = amb + np.roll(amb, len(amb) // 2) * 0  # 保持简单
save("amb_underwater", amb, 0.6)

# 鱼群受惊
t = t_(0.6)
save("fish_scatter", band(len(t), 150, 1500) * env(t, 0.02, 7) + bubbles_burst(0.6, 8, 400, 900) * 0.4, 0.5)

# ---------- 背包与界面 ----------
t = t_(0.35)
save("item_pickup", (np.sin(2 * np.pi * 880 * t) * 0.5 + np.sin(2 * np.pi * 1320 * t) * 0.3) * env(t, 0.005, 12) + band(len(t), 2000, 8000) * env(t, 0.002, 40) * 0.3, 0.5)
t = t_(0.3)
save("item_drop", band(len(t), 100, 2500) * env(t, 0.002, 25), 0.6)
t = t_(0.6)
save("item_use", band(len(t), 200, 3000) * (np.sin(2 * np.pi * 7 * t) > 0) * env(t, 0.02, 4), 0.5)
t = t_(0.45)
cloth = band(len(t), 800, 7000) * env(t, 0.03, 9)
save("bag_open", cloth + band(len(t), 100, 600) * env(t, 0.01, 20) * 0.5, 0.5)
save("bag_close", cloth[::-1] * 0.8, 0.45)
t = t_(0.06)
save("ui_click", band(len(t), 2000, 9000) * env(t, 0.001, 120), 0.35)
t = t_(0.5)
save("weapon_equip", band(len(t), 300, 6000) * env(t, 0.002, 20) + np.sin(2 * np.pi * 2400 * t) * env(t, 0.002, 30) * 0.2, 0.5)

# ---------- 砍刀 ----------
t = t_(0.7)
save("machete_draw", (np.sin(2 * np.pi * (3200 + 1800 * t) * t) * 0.3 + band(len(t), 3000, 12000)) * env(t, 0.15, 6), 0.45)
for v in range(3):
    t = t_(0.4)
    swoosh = band(len(t), 400 + v * 100, 3500) * np.sin(np.pi * np.clip(t / 0.3, 0, 1)) ** 2
    save(f"machete_swing_{v + 1}", swoosh, 0.6)
t = t_(0.5)
save("machete_hit_wood", band(len(t), 150, 3000) * env(t, 0.001, 25) + np.sin(2 * np.pi * 240 * t) * env(t, 0.001, 30) * 0.4, 0.8)
save("machete_hit_stone", band(len(t), 1500, 10000) * env(t, 0.001, 30) + np.sin(2 * np.pi * 3100 * t) * env(t, 0.001, 9) * 0.3, 0.8)
save("machete_hit_flesh", band(len(t), 100, 1500) * env(t, 0.003, 18), 0.8)

# ---------- 鱼枪 ----------
t = t_(0.6)
save("speargun_fire", band(len(t), 150, 4000) * env(t, 0.001, 14) + np.sin(2 * np.pi * 95 * t) * env(t, 0.001, 9) * 0.6, 0.85)
t = t_(1.2)
stretch = band(len(t), 100, 1200) * np.clip(t / 0.9, 0, 1) * (t < 0.95)
click = np.zeros(len(t)); mix_at(click, band(int(SR * 0.05), 2000, 9000) * np.exp(-np.arange(int(SR * 0.05)) / SR * 80), 1.0)
save("speargun_reload", stretch * 0.6 + click, 0.6)
t = t_(0.4)
save("spear_hit_flesh", band(len(t), 100, 1800) * env(t, 0.002, 16), 0.8)
save("spear_hit_sand", band(len(t), 200, 3000) * env(t, 0.002, 22), 0.6)
save("spear_hit_hard", band(len(t), 1200, 9000) * env(t, 0.001, 28) + np.sin(2 * np.pi * 2600 * t) * env(t, 0.001, 12) * 0.4, 0.75)

# ---------- 砗磲 ----------
t = t_(1.0)
save("clam_open", band(len(t), 80, 900) * env(t, 0.1, 3) + bubbles_burst(1.0, 12, 200, 700) * 0.5, 0.6)
print("ok")
