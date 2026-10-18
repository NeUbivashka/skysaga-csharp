import json
import os

from targets import BUNDLE, GAME, GEODATA
OUT = os.path.join(GAME, "Data", "LootTables.txt")


def biomes(entry):
    values = entry["Filter"]["ValidBiomes"]
    names = [v["Biome"] for v in values["Values"]]
    if not names:
        return "-"
    return ("!" if values["Invert"] else "") + ",".join(names)


SLOTS = ("Primary", "Secondary", "Tertiary", "Quaternary")


def materials(entry):
    specs = entry["ItemSpec"].get("Materials", {})
    fields = []
    for slot in SLOTS:
        spec = specs.get(slot, {}).get("MaterialOrDefault", {})
        if spec.get("Type") == "Specific" and spec.get("Material"):
            fields.append(spec["Material"])
        elif spec.get("Type") == "Entity":
            fields.append("@entity")
        else:
            fields.append("-")
    return " ".join(fields)


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    lines = []
    for table in geo["LootTables"]:
        lines.append(f"table {table['Name']}")
        for entry in table["Entries"]:
            ref = entry["LootTableOrList"]
            kind, name = ("list", ref["LootList"]) if ref["LootList"] else ("table", ref["LootTable"])
            if not name:
                continue
            lines.append(f"  {kind} {name} {entry['Quantity']} {entry['SpawnPercentage']:g} {biomes(entry)}")
    for loot_list in geo["LootLists"]:
        lines.append(f"list {loot_list['Name']} {'all' if loot_list['AddAllValidItems'] else 'pick'}")
        for entry in loot_list["Entries"]:
            resource = entry["ItemSpec"].get("Resource")
            if not resource:
                continue
            lines.append(f"  item {resource} {entry['Quantity']} {entry.get('Frequency', 1):g} {biomes(entry)} {materials(entry)}")
    for material in geo["Materials"]:
        if material.get("Category") == "Meat":
            lines.append(f"meat {material['Name']}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{len(geo['LootTables'])} tables, {len(geo['LootLists'])} lists -> {OUT}")


if __name__ == "__main__":
    main()
