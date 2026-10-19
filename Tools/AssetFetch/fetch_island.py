"""Download island assets from Poly Haven (CC0) and convert maps for Unity URP.

Terrain layers  -> albedo (RGB), normal, mask (R metal, G AO, B height, A smoothness)   [URP TerrainLit mask map]
Model materials -> albedo (RGBA when the map set has an alpha map), normal, ms (R metal, A smoothness), ao
"""
import json
import os
import sys
import urllib.request

import numpy as np
from PIL import Image

PROJECT = sys.argv[1]
ART = os.path.join(PROJECT, "Assets", "Art")
API = "https://api.polyhaven.com/files/"
UA = {"User-Agent": "FPSGame-asset-fetch/1.0"}

TERRAIN = ["aerial_beach_01", "damp_beach_sand", "leafy_grass", "forest_leaves_02", "rock_face_03"]
MODELS = ["island_tree_01", "island_tree_02", "fern_02", "shrub_03", "grass_medium_01", "rock_moss_set_01"]


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


def url_of(files, key, exts=("png", "jpg")):
    entry = files[key]["2k"]
    for ext in exts:
        if ext in entry:
            return entry[ext]["url"], ext
    raise KeyError(key)


def tmp_get(files, key, out_dir, name, exts=("png", "jpg")):
    url, ext = url_of(files, key, exts)
    return download(url, os.path.join(out_dir, f"_{name}.{ext}"))


def load(path, mode):
    return np.asarray(Image.open(path).convert(mode))


def fetch_terrain(tid):
    out = os.path.join(ART, "Terrain", tid)
    if os.path.exists(os.path.join(out, f"{tid}_mask.png")):
        print("  skip", tid)
        return
    files = get_json(API + tid)
    alb = tmp_get(files, "Diffuse", out, "diff", ("jpg", "png"))
    Image.open(alb).convert("RGB").save(os.path.join(out, f"{tid}_albedo.png"))
    nor = tmp_get(files, "nor_gl", out, "nor")
    os.replace(nor, os.path.join(out, f"{tid}_normal.png"))
    arm = load(tmp_get(files, "arm", out, "arm"), "RGB")
    disp = load(tmp_get(files, "Displacement", out, "disp"), "L")
    if disp.shape != arm.shape[:2]:
        disp = np.asarray(Image.fromarray(disp).resize(arm.shape[1::-1]))
    mask = np.dstack([arm[..., 2], arm[..., 0], disp, 255 - arm[..., 1]]).astype(np.uint8)
    Image.fromarray(mask, "RGBA").save(os.path.join(out, f"{tid}_mask.png"))
    for f in os.listdir(out):
        if f.startswith("_"):
            os.remove(os.path.join(out, f))
    print("  conv", tid)


def fetch_model(mid):
    out = os.path.join(ART, "Models", mid)
    files = get_json(API + mid)
    download(files["fbx"]["2k"]["fbx"]["url"], os.path.join(out, f"{mid}.fbx"))
    groups = [p for p in ("", "branches_", "leaves_", "bark_", "hull_", "rigging_", "sails_") if (p + "arm") in files or (p == "" and "arm" in files)]
    for p in groups:
        name = f"{mid}_{p}"
        if os.path.exists(os.path.join(out, name + "ao.png")):
            continue
        diff_key = "Diffuse" if p == "" else p + "diff"
        alpha_key = "Alpha" if p == "" else p + "alpha"
        diff = load(tmp_get(files, diff_key, out, p + "diff", ("png", "jpg")), "RGBA")
        if alpha_key in files:
            alpha = load(tmp_get(files, alpha_key, out, p + "alpha"), "L")
            if alpha.shape != diff.shape[:2]:
                alpha = np.asarray(Image.fromarray(alpha).resize(diff.shape[1::-1]))
            diff = np.dstack([diff[..., :3], alpha])
            Image.fromarray(diff.astype(np.uint8), "RGBA").save(os.path.join(out, name + "albedo.png"))
        else:
            Image.fromarray(diff[..., :3].astype(np.uint8), "RGB").save(os.path.join(out, name + "albedo.png"))
        os.replace(tmp_get(files, p + "nor_gl", out, p + "nor"), os.path.join(out, name + "normal.png"))
        arm = load(tmp_get(files, p + "arm", out, p + "arm"), "RGB")
        ms = np.dstack([arm[..., 2]] * 3 + [255 - arm[..., 1]]).astype(np.uint8)
        Image.fromarray(ms, "RGBA").save(os.path.join(out, name + "ms.png"))
        Image.fromarray(np.dstack([arm[..., 0]] * 3).astype(np.uint8), "RGB").save(os.path.join(out, name + "ao.png"))
        print("  conv", name)
    for f in os.listdir(out):
        if f.startswith("_"):
            os.remove(os.path.join(out, f))


def main():
    for tid in TERRAIN:
        print(tid, flush=True)
        fetch_terrain(tid)
    for mid in MODELS:
        print(mid, flush=True)
        fetch_model(mid)
    print("DONE")


if __name__ == "__main__":
    main()
