import json
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
GEODATA = os.path.join(ROOT, "Bundled", "36731", "GeoData.json")
OUT = os.path.join(ROOT, "Servers", "SkySaga.Game", "Data", "VoxelLoot.txt")


def first_resource(name, tables, lists):
    table = tables.get(name)
    if not table:
        return None
    for entry in table["Entries"]:
        ref = entry["LootTableOrList"]
        if ref.get("LootTable"):
            found = first_resource(ref["LootTable"], tables, lists)
            if found:
                return found
        loot = lists.get(ref.get("LootList"))
        if loot:
            for item in loot["Entries"]:
                resource = item.get("ItemSpec", {}).get("Resource")
                if resource:
                    return resource
    return None


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    tables = {t["Name"]: t for t in geo["LootTables"]}
    lists = {t["Name"]: t for t in geo["LootLists"]}
    rows = []
    for voxel in sorted(geo["Voxels"], key=lambda v: v["VoxelIndex"]):
        resource = first_resource(voxel["LootTable"], tables, lists)
        if resource:
            rows.append(f"{voxel['VoxelIndex']} {resource} {voxel['Name']}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} voxels -> {OUT}")


if __name__ == "__main__":
    main()
