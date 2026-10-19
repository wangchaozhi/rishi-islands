"""Download shipwreck / ruin assets from Poly Haven (CC0) and convert maps for URP Lit.

Per material group (prefix) writes: <id>_<prefix>albedo.png (RGBA if alpha), _normal.png, _ms.png (R metal, A smooth), _ao.png
Textures (for ruin stone) are written the same way with an empty prefix.
"""
import json
import os
import sys
import urllib.request

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
PROJECT = sys.argv[1]
ART = os.path.join(PROJECT, "Assets", "Art")
API = "https://api.polyhaven.com/files/"
UA = {"User-Agent": "FPSGame-asset-fetch/1.0"}

MODELS = {  # id -> (resolution, prefixes to skip)
    "ship_pinnace": ("1k", {"sails_", "rigging_"}),
    "treasure_chest": ("2k", set()),
    "cannon_01": ("2k", set()),
}
TEXTURES = ["mossy_sandstone", "mossy_cobblestone", "old_sandstone_02"]


def get_json(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA)) as r:
        return json.load(r)


def download(url, dest):
    if os.path.exists(dest):
        return dest
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA)) as r, open(dest + ".part", "wb") as f:
        while chunk := r.read(1 << 20):
            f.write(chunk)
    os.replace(dest + ".part", dest)
    print("  got ", os.path.basename(dest), os.path.getsize(dest) // 1024, "KB", flush=True)
    return dest


def first_key(files, *names):
    return next((n for n in names if n in files), None)


def fetch_map(files, key, res, out_dir, tag, mode):
    entry = files[key][res]
    for ext in ("png", "jpg"):
        if ext in entry:
            path = download(entry[ext]["url"], os.path.join(out_dir, f"_{tag}.{ext}"))
            return np.asarray(Image.open(path).convert(mode))
    raise KeyError(key)


def resize_to(a, shape):
    if a.shape[:2] == shape[:2]:
        return a
    return np.asarray(Image.fromarray(a).resize((shape[1], shape[0])))


def prefixes(files):
    found = set()
    for k in files:
        for suffix in ("diff", "Diffuse"):
            if k.endswith(suffix):
                found.add(k[: -len(suffix)] if k != "Diffuse" else "")
    return sorted(found)


def convert_group(files, prefix, res, out_dir, name):
    if os.path.exists(os.path.join(out_dir, name + "ao.png")):
        return
    p = prefix
    diff_key = first_key(files, p + "diff", "Diffuse" if p == "" else "")
    nor_key = first_key(files, p + "nor_gl", "nor_gl" if p == "" else "")
    if not diff_key or not nor_key:
        print("  skip group", repr(prefix))
        return
    diff = fetch_map(files, diff_key, res, out_dir, p + "diff", "RGBA")
    alpha_key = first_key(files, p + "alpha", "Alpha" if p == "" else "")
    if alpha_key:
        alpha = resize_to(fetch_map(files, alpha_key, res, out_dir, p + "alpha", "L"), diff.shape)
        Image.fromarray(np.dstack([diff[..., :3], alpha]).astype(np.uint8), "RGBA").save(os.path.join(out_dir, name + "albedo.png"))
    else:
        Image.fromarray(diff[..., :3].astype(np.uint8), "RGB").save(os.path.join(out_dir, name + "albedo.png"))
    nor = fetch_map(files, nor_key, res, out_dir, p + "nor", "RGB")
    Image.fromarray(nor.astype(np.uint8), "RGB").save(os.path.join(out_dir, name + "normal.png"))

    h, w = diff.shape[:2]
    arm_key = first_key(files, p + "arm", "arm" if p == "" else "")
    if arm_key:
        arm = resize_to(fetch_map(files, arm_key, res, out_dir, p + "arm", "RGB"), diff.shape)
        ao, rough, metal = arm[..., 0], arm[..., 1], arm[..., 2]
    else:
        def single(*names, default):
            k = first_key(files, *names)
            return resize_to(fetch_map(files, k, res, out_dir, k, "L"), diff.shape) if k else np.full((h, w), default, np.uint8)
        ao = single(p + "ao", "AO", default=255)
        rough = single(p + "rough", p + "roughness", "Rough", default=180)
        metal = single(p + "metal", p + "metallic", "Metal", default=0)
    Image.fromarray(np.dstack([metal, metal, metal, 255 - rough]).astype(np.uint8), "RGBA").save(os.path.join(out_dir, name + "ms.png"))
    Image.fromarray(np.dstack([ao, ao, ao]).astype(np.uint8), "RGB").save(os.path.join(out_dir, name + "ao.png"))
    print("  conv", name, flush=True)


def cleanup(out_dir):
    for f in os.listdir(out_dir):
        if f.startswith("_"):
            os.remove(os.path.join(out_dir, f))


def main():
    for mid, (res, skip) in MODELS.items():
        print(mid, flush=True)
        files = get_json(API + mid)
        out = os.path.join(ART, "Models", mid)
        download(files["fbx"][res]["fbx"]["url"], os.path.join(out, f"{mid}.fbx"))
        for p in prefixes(files):
            if p in skip:
                continue
            convert_group(files, p, res, out, f"{mid}_{p}")
        cleanup(out)
    for tid in TEXTURES:
        print(tid, flush=True)
        files = get_json(API + tid)
        out = os.path.join(ART, "Textures", tid)
        convert_group(files, "", "2k", out, f"{tid}_")
        cleanup(out)
    print("DONE")


if __name__ == "__main__":
    main()
