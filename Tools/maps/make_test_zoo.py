import json
import os
import re
import sys

from targets import BUILD, BUNDLE, GAME, GEODATA

SIZE_CHUNKS = (6, 4, 6)
FLOOR_Y = 6
GROUND_Y = FLOOR_Y + 1
FLOOR_BLOCK = 1
SUBFLOOR_BLOCK = 0
STONE_BLOCK = 2
WATER_BLOCK = 50
EDGE = 8
SEA_LEVEL = 5
SEA_FLOOR_LEVEL = 0
SEA_START_Z = 104
SEA_BED_Y = 2
SEA_STEPS_X = range(28, 33)
SEA_STEPS = {104: 5, 105: 4, 106: 3}
POOL_XS = (range(14, 19), range(22, 27))
POOL_ZS = range(82, 87)
POOL_BED_Y = 3

SMALL_SETS = {
    "36731": [
        "HUB_NPC_Trader", "HUB_NPC_Gladiator", "HUB_NPC_Explorer", "HUB_NPC_Crafter",
        "Forest_Chicken", "Forest_Skitterling_Fighter_0",
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
DAMAGED_BLOCK = 2
DAMAGED_BLOCK_HEALTH = (219, 188, 156, 125, 94, 63, 31)
DAMAGED_BLOCK_X = 14
DAMAGED_BLOCK_ZS = (54, 50)
DAMAGED_BLOCK_SPACING = 2
CHEST_SPACING = 3
CHESTS_PER_ROW = 18
CHEST_ROWS_Z = (0, 8, 14)
CHEST_BUILDS = {"36731"}
EXCLUDED_CHESTS = {"Chest_Christmas_Minor"}
ROW_SPACING = 6
MENTAL_STATE_CREATURE = "Forest_Chicken"
MENTAL_STATE_NEIGHBOUR = "Forest_Skitterling_Fighter_0"
MENTAL_STATES = ["Stun", "Dazed", "Sleep", "Resting", "Immobilised", "Vulnerable"]
MENTAL_STATE_SPACING = 1
FACING_START_YAW = 180
HOSTILE_CREATURE = "Forest_Skitterling_Fighter_0"
HOSTILE_POSITION = (80, 20)
AIRSHIP_POSITION = (23, 64)
COLUMN_SPACING = 4


ADVENTURE_ROW = [
    ("Mushroom_Cave_A", 16, GROUND_Y, 71),
    ("Mushroom_Cave_B", 18, GROUND_Y, 71),
    ("Cave_Crystal_Clump_A", 20, GROUND_Y, 71),
    ("Cave_Crystal_Clump_A", 22, GROUND_Y, 71),
    ("Cave_Crystal_Clump_A_Lit", 24, GROUND_Y, 71),
    ("Cave_Crystal_Clump_A_Lit", 26, GROUND_Y, 71),
    ("Cave_Crystal_Hanging_A", 19, GROUND_Y + 2, 74),
    ("Stalactite_Medium", 22, GROUND_Y + 2, 74),
    ("Cave_Crystal_Clump_Large", 20, GROUND_Y, 77),
    ("Barrel_A", 24, GROUND_Y, 77),
    ("Anvil_2x1x1", 27, GROUND_Y, 77),
]


TREES = [
    ("ForestTree", 36, 54, 11),
    ("ForestTree", 40, 74, 23),
    ("MountainTree", 44, 60, 37),
    ("PalmTree", 38, 64, 41),
    ("BambooTree", 46, 70, 53),
    ("DesertCactus", 34, 78, 67),
]
DECORATION_YAWS = (0, 90, 180, 270)
WINDOW_XS = (14, 18, 22, 26)
WINDOW_Z = 90
VINE_DECORATION_XS = (14, 19, 24, 29)
VINE_DECORATION_Y = GROUND_Y + 3
VINE_DECORATION_Z = 94
ROTATED_BARREL = ("Barrel_A", 30, 90, 45)
VINE_WALL_XS = range(12, 64)
VINE_WALL_Z = 100
VINE_WALL_TOP_Y = 14
VINE_TREES = [(16, 1), (20, 2)]
VINE_COLUMN_XS = (16, 20, 24)
VINE_COLUMN_Z = 97
VINE_COLUMN_TOP_Y = 13
VINE_OVERHANG_ZS = (97, 98, 99)
SPARSE_VINE_COLUMN_X = 24
SPARSE_VINE_STRAIGHT = 23
SPARSE_VINE_END = 30
VINE_PART_Z = 99
VINE_PART_START_X = 13
VINE_PART_SPACING = 2
VINE_PART_ROWS = [
    (10, [19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30]),
    (12, [31, 32, 33, 34, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 43, 44, 45, 46, 50, 51, 52, 53]),
]
IVY_ROW_Y = GROUND_Y + 1
IVY_ROW_PARTS = (22, 28, 34)
IVY_ROW_COUNT = 18
IVY_TEST_Y = GROUND_Y + 2
IVY_TEST_XS = (40, 43, 46, 49, 52, 55)
IVY_TEST_ROWS = [(85, 19), (88, 25), (91, 31)]
SPARSE_PLANT_MATERIALS = ["Plant_Green"]
BLOCK_ROW_MATERIALS = (135, 132, 8, 134, 131, 133)
BLOCK_ROW_X = 14
BLOCK_ROW_Z = 57
BLOCK_ROW_STRIP = 3
BLOCK_ROW_WALL_HEIGHT = 3


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


def damaged_blocks():
    return [{"x": DAMAGED_BLOCK_X + i * DAMAGED_BLOCK_SPACING, "y": GROUND_Y, "z": z,
             "block": DAMAGED_BLOCK, "meta": 0, "health": health}
            for z in DAMAGED_BLOCK_ZS for i, health in enumerate(DAMAGED_BLOCK_HEALTH)]


def chest_gallery(z):
    with open(os.path.join(os.path.dirname(GEODATA), "Entities.json"), encoding="utf-8") as f:
        entities = {e["Name"] for e in json.load(f)["Entities"]}
    with open(GEODATA, encoding="utf-8") as f:
        names = sorted(r["Name"] for r in json.load(f)["Resources"] if r["Name"].startswith("Chest") and r["Name"] in entities)
    return [{"name": name,
             "x": EDGE + (i % CHESTS_PER_ROW) * CHEST_SPACING,
             "y": GROUND_Y,
             "z": z + CHEST_ROWS_Z[i // CHESTS_PER_ROW]}
            for i, name in enumerate(names) if name not in EXCLUDED_CHESTS]


def mental_state_row(entities):
    by_name = {e["name"]: e for e in entities}
    creature, neighbour = by_name.get(MENTAL_STATE_CREATURE), by_name.get(MENTAL_STATE_NEIGHBOUR)
    if creature is None or neighbour is None:
        return []
    creature["x"] = neighbour["x"] - 2
    return [{"name": MENTAL_STATE_CREATURE, "x": creature["x"] - (i + 1) * MENTAL_STATE_SPACING, "y": GROUND_Y,
             "z": creature["z"], "yaw": FACING_START_YAW, "mentalState": state}
            for i, state in enumerate(MENTAL_STATES)]


def terrain_block(x, y, z):
    if z >= SEA_START_Z:
        if y == SEA_STEPS.get(z) and x in SEA_STEPS_X:
            return STONE_BLOCK
        if y > SEA_LEVEL:
            return None
        return SUBFLOOR_BLOCK if y <= SEA_BED_Y else WATER_BLOCK
    if z in POOL_ZS and any(x in xs for xs in POOL_XS):
        if y > SEA_LEVEL:
            return None
        return SUBFLOOR_BLOCK if y <= POOL_BED_Y else WATER_BLOCK
    return FLOOR_BLOCK if y == FLOOR_Y else SUBFLOOR_BLOCK


def terrain(sx, sz):
    return [{"x": x, "y": y, "z": z, "block": block, "meta": 0}
            for y in range(FLOOR_Y + 1) for x in range(sx) for z in range(sz)
            if (block := terrain_block(x, y, z)) is not None]


def stone(x, y, z):
    return {"x": x, "y": y, "z": z, "block": STONE_BLOCK, "meta": 0}


def vine_wall_blocks():
    wall = [stone(x, y, VINE_WALL_Z) for x in VINE_WALL_XS for y in range(GROUND_Y, VINE_WALL_TOP_Y + 1)]
    overhangs = [stone(x, VINE_WALL_TOP_Y, z) for x in VINE_COLUMN_XS for z in VINE_OVERHANG_ZS]
    ivy_tests = [stone(x, IVY_TEST_Y, z) for z, _ in IVY_TEST_ROWS for x in IVY_TEST_XS]
    return wall + overhangs + ivy_tests


def block_row():
    width = len(BLOCK_ROW_MATERIALS) * BLOCK_ROW_STRIP
    material = lambda x: BLOCK_ROW_MATERIALS[(x - BLOCK_ROW_X) // BLOCK_ROW_STRIP]
    xs = range(BLOCK_ROW_X, BLOCK_ROW_X + width)
    floor = {(x, FLOOR_Y, z): material(x) for x in xs for z in range(BLOCK_ROW_Z, BLOCK_ROW_Z + BLOCK_ROW_STRIP)}
    wall = [{"x": x, "y": y, "z": BLOCK_ROW_Z + BLOCK_ROW_STRIP, "block": material(x), "meta": 0}
            for x in xs for y in range(GROUND_Y, GROUND_Y + BLOCK_ROW_WALL_HEIGHT)]
    return floor, wall


def sparse_entity(name, tree_type, x, y, z, parts):
    return {"name": name, "x": x, "y": y, "z": z, "treeType": tree_type, "materials": SPARSE_PLANT_MATERIALS,
            "sparseParts": [{"x": px, "y": py, "z": pz, "part": part, "facing": 0} for px, py, pz, part in parts]}


def decorations():
    windows = [{"name": "Window_Stone_A", "x": x, "y": GROUND_Y, "z": WINDOW_Z, "yaw": yaw}
               for x, yaw in zip(WINDOW_XS, DECORATION_YAWS)]
    vines = [{"name": "Vine_Jungle_A", "x": x, "y": VINE_DECORATION_Y, "z": VINE_DECORATION_Z, "yaw": yaw}
             for x, yaw in zip(VINE_DECORATION_XS, DECORATION_YAWS)]
    name, x, z, yaw = ROTATED_BARREL
    return windows + vines + [{"name": name, "x": x, "y": GROUND_Y, "z": z, "yaw": yaw}]


def vines_and_ivy():
    ivy_row = [(VINE_PART_START_X + i * VINE_PART_SPACING, IVY_ROW_Y, VINE_WALL_Z, IVY_ROW_PARTS[i % len(IVY_ROW_PARTS)])
               for i in range(IVY_ROW_COUNT)]
    ivy_tests = [(x, IVY_TEST_Y, z, first + i) for z, first in IVY_TEST_ROWS for i, x in enumerate(IVY_TEST_XS)]
    vine_rows = [(VINE_PART_START_X + i * VINE_PART_SPACING, y, VINE_PART_Z, part)
                 for y, parts in VINE_PART_ROWS for i, part in enumerate(parts)]
    column = [(SPARSE_VINE_COLUMN_X, y, VINE_COLUMN_Z, SPARSE_VINE_STRAIGHT) for y in range(VINE_COLUMN_TOP_Y, GROUND_Y, -1)]
    column.append((SPARSE_VINE_COLUMN_X, GROUND_Y, VINE_COLUMN_Z, SPARSE_VINE_END))
    trees = [{"name": "Tree", "x": x, "y": VINE_COLUMN_TOP_Y, "z": VINE_COLUMN_Z, "treeType": "Vine", "seed": seed}
             for x, seed in VINE_TREES]
    return [sparse_entity("SparseTreeIvy", "Ivy", VINE_PART_START_X, IVY_ROW_Y, VINE_PART_Z, ivy_row + ivy_tests),
            sparse_entity("SparseTreeVine", "Vine", VINE_PART_START_X, VINE_PART_ROWS[0][0], VINE_PART_Z, vine_rows),
            sparse_entity("SparseTreeVine", "Vine", SPARSE_VINE_COLUMN_X, GROUND_Y, VINE_COLUMN_Z, column)] + trees


def adventure_row():
    return [{"name": name, "x": x, "y": y, "z": z, "onlyIn": "Adventure"} for name, x, y, z in ADVENTURE_ROW]


def main():
    all_types = "--all" in sys.argv
    sx, sz = SIZE_CHUNKS[0] * 32, SIZE_CHUNKS[2] * 32
    voxels = terrain(sx, sz)
    row_floor, row_wall = block_row()
    for voxel in voxels:
        voxel["block"] = row_floor.get((voxel["x"], voxel["y"], voxel["z"]), voxel["block"])

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
                             "z": gallery_end_z + 8 + (i // per_row) * 6,
                             "yaw": FACING_START_YAW})
    creatures = entities[1:]
    if "--broken" not in sys.argv and not all_types:
        entities += mental_state_row(creatures)
        for creature in creatures:
            if creature["name"] == HOSTILE_CREATURE:
                creature["x"], creature["z"] = HOSTILE_POSITION
    if BUILD in CHEST_BUILDS:
        entities += chest_gallery(gallery_end_z + 4)
    entities += adventure_row()
    entities += decorations()
    entities += vines_and_ivy()
    entities += [{"name": "Tree", "x": x, "y": GROUND_Y, "z": z, "treeType": tree_type, "seed": seed}
                 for tree_type, x, z, seed in TREES]
    entities.append({"name": "SeaLevel", "x": 0, "y": 0, "z": 0, "seaLevel": SEA_LEVEL, "seaFloorLevel": SEA_FLOOR_LEVEL})
    entities.append({"name": "Airship",
                     "x": AIRSHIP_POSITION[0],
                     "y": GROUND_Y,
                     "z": AIRSHIP_POSITION[1]})

    world = {"mapSizeChunks": {"x": SIZE_CHUNKS[0], "y": SIZE_CHUNKS[1], "z": SIZE_CHUNKS[2]},
             "biomeName": "Forest", "gameMode": 1, "voxels": voxels + damaged_blocks() + vine_wall_blocks() + row_wall, "entities": entities}
    out = os.path.join(GAME, "Data", "TestZoo.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(world, f, separators=(",", ":"))
    print(f"{out}: {len(voxels)} voxels, {len(types)} voxel types, {len(names)} creatures and NPCs")


if __name__ == "__main__":
    main()
