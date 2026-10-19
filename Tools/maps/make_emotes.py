import json
import os
import sys

from targets import BUILD, GAME, GEODATA


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(GAME, "Data")
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    lines = [
        f"# GeoData.json ({BUILD}) Emotes in Index order: name, MakeAvailableToNewPlayers.",
        "# Line n (1-based, comments excluded) is wire index n; 0 is no emote.",
    ]
    for e in sorted(geo["Emotes"], key=lambda e: e["Index"]):
        lines.append(f"{e['Name']} {int(bool(e['MakeAvailableToNewPlayers']))}")
    path = os.path.join(out_dir, "Emotes.txt")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{len(lines)} lines -> {path}")


if __name__ == "__main__":
    main()
