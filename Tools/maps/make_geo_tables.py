import json
import os
import sys

from targets import BUILD, GAME, GEODATA

SLOTS = ("Primary", "Secondary", "Tertiary", "Quaternary")
INDEXED_TABLES = (
    "AdventureCosts", "Adventures", "Backstories", "BiomePalettes", "Biomes", "CharacterAttachments", "CreatureSets", "Events",
    "GameModeProperties", "JobChallenges", "Jobs", "MapSizeCategories", "MaterialCustomisations", "Materials", "MapSizes", "PlayerSigils", "Regions", "TerrainGenerators",
    "TimeOfDayPresetLists", "TimeOfDayPresets", "Tribes", "Videos",
)


def write(out_dir, name, lines):
    path = os.path.join(out_dir, name)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{len(lines)} lines -> {path}")


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(GAME, "Data")
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    resources = sorted(geo["Resources"], key=lambda r: r["Index"])
    properties = {p["Name"]: p for p in geo["PhysicalProperties"]}
    shared = {s["Name"]: s for s in geo["SharedVoxelDatas"]}

    write(out_dir, "Resources.txt", [
        f"# GeoData.json ({BUILD}) Resources in Index order. Line n (1-based, comments excluded) is wire index n:",
        f"# the client's item table has a null entry 0, then these {len(resources)}.",
    ] + [r["Name"] for r in resources])

    category_defaults = {m["Category"]: m["Name"] for m in geo["Materials"] if m.get("IsDefaultForCategory")}
    materials = []
    for r in resources:
        slots = [r.get("MaterialCategories", {}).get(slot, {}) for slot in SLOTS]
        defaults = [s.get("DefaultMaterial") or category_defaults.get(s.get("Category"), "-") for s in slots]
        if any(d != "-" for d in defaults):
            materials.append(" ".join([r["Name"]] + defaults))
    write(out_dir, "Materials.txt", materials)

    voxels = []
    for v in sorted(geo["Voxels"], key=lambda v: v["VoxelIndex"]):
        s = shared.get(v.get("SharedVoxelData"), {})
        voxels.append(f"{v['VoxelIndex']} {s.get('Durability', 0)} {s.get('Resistance', 0)} "
                      f"{s.get('MiningToughnessTier', 0)} {1 if s.get('IsDiggable') else 0} {v['Name']}")
    write(out_dir, "VoxelStats.txt", voxels)

    durability = []
    for r in resources:
        health = properties.get(r.get("PhysicalProperties"), {}).get("Stat", {}).get("Health", {}).get("Value")
        if r.get("RequiresDurability") and health:
            durability.append(f"{r['Name']} {health:g}")
    write(out_dir, "Durability.txt", sorted(durability))

    tags = []
    for r in resources:
        values = [t["Value"] for t in r.get("TagList", {}).get("Values", [])]
        if values:
            tags.append(" ".join([r["Name"]] + values))
    write(out_dir, "ItemTags.txt", sorted(tags))

    index = [f"# GeoData.json ({BUILD}) table entries: table index name. Wire index = index + 1 (0 is none)."]
    for table in INDEXED_TABLES:
        index += [f"{table} {e['Index']} {e['Name']}" for e in sorted(geo[table], key=lambda e: e["Index"])]
    write(out_dir, "GeoIndex.txt", index)

    tod = [f"# GeoData.json ({BUILD}) time of day: preset name fixed hours | list name preset | "
           "adventure name preset list cycleMinutes | biome name cycleMinutes. '-' is none."]
    tod += [f"preset {p['Name']} {int(p['TimeOfDay']['IsFixed'])} {p['TimeOfDay']['Time']}" for p in geo["TimeOfDayPresets"]]
    for lst in geo["TimeOfDayPresetLists"]:
        entries = sorted(lst.get("Entries", []), key=lambda e: -e.get("Frequency", 0))
        if entries:
            tod.append(f"list {lst['Name']} {entries[0]['TimeOfDayPreset']}")
    for a in geo["Adventures"]:
        t = a.get("TimeOfDay", {})
        cycle = a["DayNightCycleDuration"] if a.get("OverrideDayNightCycleDuration") else "-"
        tod.append(f"adventure {a['Name']} {t.get('TimeOfDayPreset') or '-'} {t.get('TimeOfDayPresetList') or '-'} {cycle}")
    tod += [f"biome {b['Name']} {b['DayNightCycleDuration']}" for b in geo["Biomes"] if "DayNightCycleDuration" in b]
    write(out_dir, "TimeOfDay.txt", tod)


if __name__ == "__main__":
    main()
