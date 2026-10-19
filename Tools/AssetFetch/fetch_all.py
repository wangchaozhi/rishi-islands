"""一键下载 Poly Haven 的 CC0 资源到 Assets/Art（仓库里不包含这些大文件）。

用法（在项目根目录）：  python Tools/AssetFetch/fetch_all.py
需要 Python 3.10+、numpy、Pillow。已存在的文件会跳过。
"""
import os
import runpy
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

for script in ("fetch_assets.py", "fetch_island.py", "fetch_ruin.py", "fetch_biomes.py"):
    print("==", script)
    sys.argv = [script, PROJECT]
    runpy.run_path(os.path.join(HERE, script), run_name="__main__")
print("完成。打开 Unity 后运行菜单 Tools/Island 里的重建，或用 README 里的命令行重建场景。")
