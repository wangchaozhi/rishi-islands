"""Download Poly Haven CC0 models for the seabed and props: conch shell, sunken Dutch ship, dive-site buoy, flashlight."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fetch_island as fi  # noqa: E402

fi.TERRAIN = []
fi.MODELS = ["lambis_shell", "dutch_ship_medium", "ocean_buoy", "vintage_flashlight"]

if __name__ == "__main__":
    fi.PROJECT = sys.argv[1]
    fi.ART = os.path.join(fi.PROJECT, "Assets", "Art")
    fi.main()
