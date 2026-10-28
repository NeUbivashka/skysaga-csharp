import json
import os

from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "WallPlacements.txt")
RULES = ["PlaceAgainst", "PlaceBelow", "PlaceAbove"]


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    rows = []
    for data in geo["DynamicPlaceAgainstWallDatas"]:
        for rule in RULES:
            r = data["PlacementRules"][rule]
            rows.append(f"{data['Name']} {rule} {r['PitchOffset']} {r['YawOffset']}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} placement rules -> {OUT}")


if __name__ == "__main__":
    main()
