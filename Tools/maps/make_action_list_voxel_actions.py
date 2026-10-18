import json
import os

from action_lists import ALPHA, equipped_action_lists, list_entries
from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "ActionListVoxelActions.txt")
CATEGORIES = {"DigAction", "PlaceVoxelAction"}


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    voxel_actions = {a["Name"]: a for a in (geo["VoxelActions"] if ALPHA else geo["Actions"])}
    rows = []
    for list_id, _, action, actions in equipped_action_lists(geo):
        for name in list_entries(actions, ("VoxelActions", "ActionVoxel")):
            voxel_action = voxel_actions.get(name)
            if voxel_action is None or not ALPHA and voxel_action["Category"] not in CATEGORIES:
                continue
            category = voxel_action["Category"]
            on_floor = 1 if voxel_action.get(category, {}).get("MustBeOnFloor") else 0
            rows.append((list_id, f"{list_id} {action['Name']} {voxel_action['Name']} {category} {on_floor}"))
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(row for _, row in sorted(rows, key=lambda r: r[0])) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
