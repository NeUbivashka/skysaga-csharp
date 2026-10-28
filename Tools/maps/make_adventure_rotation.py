import json
import os

from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "AdventureRotation.txt")

DIFFICULTIES = ["Easy", "Medium", "Hard", "Noof"]


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)

    def wire(table, name):
        if not name:
            return 0
        return next(e["Index"] for e in geo[table] if e["Name"] == name) + 1

    adventures = {a["Name"]: a for a in geo["Adventures"]}
    rows = []

    for biome in geo["Biomes"]:
        for card in biome.get("Adventures", []):
            spec = card["MapData"]["MapSpec"]
            adventure = adventures[spec["Adventure"]]
            time_of_day = spec["TimeOfDay"] if spec["TimeOfDay"]["TimeOfDayPreset"] or spec["TimeOfDay"]["TimeOfDayPresetList"] else adventure["TimeOfDay"]
            searchable = [
                wire("Biomes", spec["Biome"]),
                wire("Regions", spec["Region"]),
                wire("Adventures", spec["Adventure"]),
                DIFFICULTIES.index(spec["Difficulty"]),
                wire("BiomePalettes", adventure["BiomePalette"]),
                wire("CreatureSets", spec["FeatureCreatureSet"]),
                wire("CreatureSets", spec["TerrainCreatureSet"]),
                wire("CreatureSets", spec["CaveCreatureSet"]),
                wire("TimeOfDayPresets", time_of_day["TimeOfDayPreset"]),
                wire("TimeOfDayPresetLists", time_of_day["TimeOfDayPresetList"]),
                wire("MapSizeCategories", spec["MapSizeCategory"]),
                wire("MapSizes", spec["MapSize"]),
                wire("TerrainGenerators", spec["TerrainGenerator"]),
                wire("Events", spec["ActiveEvent"]),
            ]
            cost = wire("AdventureCosts", card["MapData"]["Cost"])
            rows.append(" ".join([spec["Adventure"], *map(str, searchable), str(cost),
                                  adventure.get("AdventureType", "-"), adventure["RootFeature"]]))

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} adventure cards -> {OUT}")


if __name__ == "__main__":
    main()
