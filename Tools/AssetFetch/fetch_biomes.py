"""Download Poly Haven assets for terrain variety (cliffs, rocks, swamp mud, river stones, bare rock)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fetch_island as fi  # noqa: E402

fi.TERRAIN = ["brown_mud_leaves_01", "river_small_rocks", "mossy_rock", "aerial_rocks_02", "coast_sand_rocks_02"]
fi.MODELS = ["coastal_cliff_01", "coastal_cliff_02", "namaqualand_cliff_01", "boulder_01", "rock_face_01"]

if __name__ == "__main__":
    fi.PROJECT = sys.argv[1]
    fi.ART = os.path.join(fi.PROJECT, "Assets", "Art")
    fi.main()
