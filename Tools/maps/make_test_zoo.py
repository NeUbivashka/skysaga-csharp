import json
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
GAME = os.path.join(ROOT, "Servers", "SkySaga.Game")

SIZE_CHUNKS = (6, 4, 6)
FLOOR_Y = 31
GROUND_Y = FLOOR_Y + 1
FLOOR_BLOCK = 1
EDGE = 8

SMALL_SET = [
    "Desert_Chicken", "Arctic_Rabbit", "Arctic_Penguin", "Desert_Boar", "Arctic_Goat",
    "Arctic_Wolf_Fighter_0", "Arctic_Bear_Fighter_0", "Arctic_Yeti_Fighter_0",
    "Arctic_Human_Fighter_0", "Desert_Cat_Fighter_0",
    "HUB_NPC_Explorer", "HUB_NPC_Gladiator_OldCity",
]


def voxel_types():
    rows = []
    with open(os.path.join(GAME, "Data", "VoxelStats.txt"), encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if len(parts) >= 6 and int(parts[0]) != 0 and parts[4] == "1":
                rows.append((int(parts[0]), parts[5]))
    return rows


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


def main():
    all_types = "--all" in sys.argv
    sx, sz = SIZE_CHUNKS[0] * 32, SIZE_CHUNKS[2] * 32
    voxels = [{"x": x, "y": FLOOR_Y, "z": z, "block": FLOOR_BLOCK, "meta": 0}
              for x in range(sx) for z in range(sz)]

    per_row = (sx - 2 * EDGE) // 3
    types = voxel_types()
    for i, (index, _) in enumerate(types):
        x = EDGE + (i % per_row) * 3
        z = EDGE + (i // per_row) * 4
        for y in (GROUND_Y, GROUND_Y + 1):
            voxels.append({"x": x, "y": y, "z": z, "block": index, "meta": 0})
    gallery_end_z = EDGE + ((len(types) - 1) // per_row) * 4 + 6

    entities = [{"name": "Player", "x": sx // 2, "y": GROUND_Y, "z": gallery_end_z}]
    per_row = (sx - 2 * EDGE) // 6
    names = creature_names(all_types)
    for i, name in enumerate(names):
        entities.append({"name": name,
                         "x": EDGE + (i % per_row) * 6,
                         "y": GROUND_Y,
                         "z": gallery_end_z + 8 + (i // per_row) * 6})
    entities.append({"name": "Airship", "x": sx - 24, "y": GROUND_Y + 8, "z": sz - 24})

    world = {"mapSizeChunks": {"x": SIZE_CHUNKS[0], "y": SIZE_CHUNKS[1], "z": SIZE_CHUNKS[2]},
             "biomeName": "Forest", "gameMode": 1, "voxels": voxels, "entities": entities}
    out = os.path.join(GAME, "Data", "TestZoo.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(world, f, separators=(",", ":"))
    print(f"{out}: {len(voxels)} voxels, {len(types)} voxel types, {len(names)} creatures and NPCs")


if __name__ == "__main__":
    main()
