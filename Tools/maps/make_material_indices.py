import json
import os

from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "MaterialIndices.txt")


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    rows = [f"{m['Index'] + 1} {m['Name']}" for m in sorted(geo["Materials"], key=lambda m: m["Index"])]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} materials -> {OUT}")


if __name__ == "__main__":
    main()
