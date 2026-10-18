import json
import os

from targets import BUNDLE, GAME, GEODATA
OUT = os.path.join(GAME, "Data", "ActionListVoxelActions.txt")
FIRST_EQUIPPED_ACTION_LIST_ID = 1262


def action_lists(action):
    area = action.get("AreaOfEffect", {}).get("ContextCollectionParams", {}).get("Actions", {})
    return [area, action.get("OnActiveWindowEntered", {}), action.get("OnActiveWindowExited", {})]


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    voxel_actions = {a["Name"]: a for a in geo["VoxelActions"]}
    rows = []
    for action in geo["EquippedActions"]:
        for slot, actions in enumerate(action_lists(action)):
            list_id = FIRST_EQUIPPED_ACTION_LIST_ID + 3 * action["Index"] + slot
            for entry in actions.get("VoxelActions", []):
                voxel_action = voxel_actions.get(entry.get("ActionVoxel"))
                if voxel_action is None:
                    continue
                category = voxel_action["Category"]
                on_floor = 1 if voxel_action.get(category, {}).get("MustBeOnFloor") else 0
                rows.append(f"{list_id} {action['Name']} {voxel_action['Name']} {category} {on_floor}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
