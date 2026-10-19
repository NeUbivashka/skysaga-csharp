import json
import os
import re
import sys

from targets import BUILD, BUNDLE, GAME, GEODATA

SIZE_CHUNKS = (6, 4, 6)
FLOOR_Y = 31
GROUND_Y = FLOOR_Y + 1
FLOOR_BLOCK = 1
FLOOR_DEPTH = 4
EDGE = 8

SMALL_SETS = {
    "36731": [
        "HUB_NPC_Trader", "HUB_NPC_Gladiator",
        "Forest_Chicken", "Forest_Skitterling_Fighter_0", "Forest_Human_Fighter_0",
    ],
    "38328": [
        "HUB_NPC_ExplorerShop", "HUB_NPC_FoodShop",
        "Forest_Chicken", "Forest_Sheep",
        "Forest_Skitterling_Fighter_0", "Forest_Skitterling_Spitter_0",
        "Forest_Wolf_Fighter_0", "Forest_Bear_Fighter_0", "Forest_Boar", "Forest_Human_Fighter_0",
    ],
}
SMALL_SET = SMALL_SETS[BUILD]

BROKEN_GROUPS = [
    ("NPCs", r"^HUB_NPC_"),
    ("Underking", r"^Arctic_HumanA_"),
    ("Wasteland", r"^Arctic_Human_(?!Beast_|WarBeast_)"),
    ("Dervish", r"^Desert_Cat_(?!Beast_)"),
    ("Outlaw", r"^Forest_HumanA_"),
    ("Brigand", r"^Forest_Human_"),
    ("Ronin", r"^Jungle_Lizard_"),
    ("Other", r"^(FTUE_CityGuard_|Test_Biped|Jungle_Monkey_Mastery|Desert_Chicken$|Midwinter_Chicken$)"),
]
REFERENCE_CHEST = "Chest_Generic_Major"
REFERENCE_CHEST_SLOTS = 55 - 9
REFERENCE_CHEST_SPACING = 2
REFERENCE_CHEST_BUILDS = {"36731"}
ROW_SPACING = 6
COLUMN_SPACING = 4


def broken_rows():
    data = json.load(open(os.path.join(GAME, "Data", "Entities.json"), encoding="utf-8"))
    entities = data["Entities"] if isinstance(data, dict) else data
    seen, names = set(), []
    for e in entities:
        key = re.sub(r"_\d+$", "", e["Name"])
        if key not in seen:
            seen.add(key)
            names.append(e["Name"])
    return [(title, [n for n in names if re.search(pattern, n)]) for title, pattern in BROKEN_GROUPS]


def voxel_types():
    rows = []
    with open(os.path.join(GAME, "Data", "VoxelStats.txt"), encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if len(parts) >= 6 and parts[4] == "1":
                rows.append((int(parts[0]), parts[5]))
    return [row for row in rows if row[0] != 0] + [row for row in rows if row[0] == 0]


def creature_names(all_types):
    if not all_types:
        return SMALL_SET
    data = json.load(open(os.path.join(GAME, "Data", "Entities.json"), encoding="utf-8"))
    entities = data["Entities"] if isinstance(data, dict) else data
    seen, names = set(), []
    for e in entities:
        name = e["Name"]
        components = e.get("client", {}).get("components", {})
        if "clientenemyaicomponent" in components:
            key = re.sub(r"_\d+$", "", name)
            if key not in seen:
                seen.add(key)
                names.append(name)
        elif name.startswith("HUB_NPC_"):
            names.append(name)
    return names


def reference_chests(z):
    with open(GEODATA, encoding="utf-8") as f:
        inventory_items = {r["Name"] for r in json.load(f)["Resources"] if r.get("IsInventoryItem")}
    with open(os.path.join(GAME, "Data", "Resources.txt"), encoding="utf-8") as f:
        items = [line.strip() for line in f if line.strip() and not line.startswith("#")]
    items = [item for item in items if item in inventory_items]
    batches = [items[i:i + REFERENCE_CHEST_SLOTS] for i in range(0, len(items), REFERENCE_CHEST_SLOTS)]
    return [{"name": REFERENCE_CHEST, "x": EDGE + i * REFERENCE_CHEST_SPACING, "y": GROUND_Y, "z": z, "items": batch}
            for i, batch in enumerate(batches)]


def main():
    all_types = "--all" in sys.argv
    sx, sz = SIZE_CHUNKS[0] * 32, SIZE_CHUNKS[2] * 32
    voxels = [{"x": x, "y": y, "z": z, "block": FLOOR_BLOCK, "meta": 0}
              for y in range(FLOOR_Y - FLOOR_DEPTH + 1, FLOOR_Y + 1) for x in range(sx) for z in range(sz)]

    per_row = (sx - 2 * EDGE) // 3
    types = voxel_types()
    for i, (index, _) in enumerate(types):
        x = EDGE + (i % per_row) * 3
        z = EDGE + (i // per_row) * 4
        for y in (GROUND_Y, GROUND_Y + 1):
            voxels.append({"x": x, "y": y, "z": z, "block": index, "meta": 0})
    gallery_end_z = EDGE + ((len(types) - 1) // per_row) * 4 + 6

    entities = [{"name": "Player", "x": sx // 2, "y": GROUND_Y, "z": gallery_end_z}]
    if "--broken" in sys.argv:
        rows = broken_rows()
        names = [n for _, row in rows for n in row]
        for r, (title, row) in enumerate(rows):
            z = gallery_end_z + 8 + r * ROW_SPACING
            left = sx // 2 + (len(row) - 1) * COLUMN_SPACING // 2
            print(f"row {r + 1} {title}:")
            for i, name in enumerate(row):
                entities.append({"name": name, "x": left - i * COLUMN_SPACING, "y": GROUND_Y, "z": z})
                print(f"  {i + 1}: {name}")
    else:
        per_row = (sx - 2 * EDGE) // 6
        names = creature_names(all_types)
        for i, name in enumerate(names):
            entities.append({"name": name,
                             "x": EDGE + (i % per_row) * 6,
                             "y": GROUND_Y,
                             "z": gallery_end_z + 8 + (i // per_row) * 6})
    creatures = entities[1:]
    if BUILD in REFERENCE_CHEST_BUILDS:
        entities += reference_chests(gallery_end_z + 4)
    entities.append({"name": "Airship",
                     "x": (min(e["x"] for e in creatures) + max(e["x"] for e in creatures)) // 2,
                     "y": GROUND_Y,
                     "z": max(e["z"] for e in creatures) + 14})

    world = {"mapSizeChunks": {"x": SIZE_CHUNKS[0], "y": SIZE_CHUNKS[1], "z": SIZE_CHUNKS[2]},
             "biomeName": "Forest", "gameMode": 1, "voxels": voxels, "entities": entities}
    out = os.path.join(GAME, "Data", "TestZoo.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(world, f, separators=(",", ":"))
    print(f"{out}: {len(voxels)} voxels, {len(types)} voxel types, {len(names)} creatures and NPCs")


if __name__ == "__main__":
    main()
