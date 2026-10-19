"""Download CC0 assets from Poly Haven into the Unity project and convert ARM maps for URP Lit.

URP Lit wants:  _MetallicGlossMap (R = metallic, A = smoothness)  and  _OcclusionMap (G = AO).
Poly Haven ARM: R = AO, G = roughness, B = metallic.
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

TEXTURES = ["concrete_floor_02", "brick_wall_006", "concrete_wall_004"]
MODELS = {  # id -> map-name prefix used in the files listing ("" for plain names)
    "service_pistol": "",
    "wooden_crate_02": "",
    "concrete_road_barrier": "",
    "Barrel_01": "explosive_",
}
HDRI = "kloofendal_48d_partly_cloudy_puresky"


def get_json(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA)) as r:
        return json.load(r)


def download(url, dest):
    if os.path.exists(dest):
        print("  skip", os.path.basename(dest))
        return
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA)) as r, open(dest + ".part", "wb") as f:
        while chunk := r.read(1 << 20):
            f.write(chunk)
    os.replace(dest + ".part", dest)
    print("  got ", os.path.basename(dest), os.path.getsize(dest) // 1024, "KB")


def pick(files, key, res="2k", exts=("png", "jpg")):
    entry = files.get(key, {}).get(res, {})
    for ext in exts:
        if ext in entry:
            return entry[ext]["url"], ext
    raise KeyError(f"{key} {res} not found in {list(files)}")


def fetch_pbr(asset_id, files, prefix, out_dir):
    diff_key = next(k for k in (prefix + "diff", prefix + "Diffuse", "Diffuse") if k in files)
    nor_key = next(k for k in (prefix + "nor_gl", "nor_gl") if k in files)
    arm_key = next(k for k in (prefix + "arm", "arm") if k in files)
    for key, suffix, exts in ((diff_key, "albedo", ("jpg", "png")), (nor_key, "normal", ("png", "jpg")), (arm_key, "arm", ("png", "jpg"))):
        url, ext = pick(files, key, exts=exts)
        download(url, os.path.join(out_dir, f"{asset_id}_{suffix}.{ext}"))
    convert_arm(asset_id, out_dir)


def convert_arm(asset_id, out_dir):
    ms_path = os.path.join(out_dir, f"{asset_id}_ms.png")
    ao_path = os.path.join(out_dir, f"{asset_id}_ao.png")
    if os.path.exists(ms_path) and os.path.exists(ao_path):
        return
    arm_file = next(f for f in os.listdir(out_dir) if f.startswith(f"{asset_id}_arm."))
    arm = np.asarray(Image.open(os.path.join(out_dir, arm_file)).convert("RGB"))
    ao, rough, metal = arm[..., 0], arm[..., 1], arm[..., 2]
    ms = np.dstack([metal, metal, metal, 255 - rough]).astype(np.uint8)
    Image.fromarray(ms, "RGBA").save(ms_path)
    Image.fromarray(np.dstack([ao, ao, ao]).astype(np.uint8), "RGB").save(ao_path)
    os.remove(os.path.join(out_dir, arm_file))
    print("  conv", asset_id, "-> _ms/_ao")


def main():
    for tid in TEXTURES:
        print(tid)
        fetch_pbr(tid, get_json(API + tid), "", os.path.join(ART, "Textures", tid))

    for mid, prefix in MODELS.items():
        print(mid)
        files = get_json(API + mid)
        out = os.path.join(ART, "Models", mid)
        download(files["fbx"]["2k"]["fbx"]["url"], os.path.join(out, f"{mid}.fbx"))
        fetch_pbr(mid, files, prefix, out)

    print(HDRI)
    files = get_json(API + HDRI)
    download(files["hdri"]["4k"]["hdr"]["url"], os.path.join(ART, "HDRI", f"{HDRI}_4k.hdr"))


if __name__ == "__main__":
    main()
