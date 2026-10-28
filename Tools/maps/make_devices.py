import json
import os

from targets import BUNDLE, GAME, GEODATA
OUT = os.path.join(GAME, "Data", "Devices.txt")


def entity_creating_items(geo):
    categories = {v["Name"]: v["Category"] for v in geo["VoxelActions"]}
    actions = {a["Name"]: a for a in geo["EquippedActions"]}
    abilities = {a["Name"]: a for a in geo["Abilities"]}
    sets = {s["Name"]: s for s in geo["AbilitiesSets"]}

    def creates(name):
        action = actions.get(name, {})
        voxel_actions = action.get("OnActiveWindowEntered", {}).get("VoxelActions", [])
        return any(categories.get(v["ActionVoxel"]) == "CreateEntityAction" for v in voxel_actions)

    items = set()
    for resource in geo["Resources"]:
        tagged = sets.get(resource.get("AbilitiesSets") or "", {}).get("TaggedAbilities", [])
        names = [a["Action"] for t in tagged for a in abilities.get(t["Abilities"], {}).get("Actions", [])]
        if any(creates(n) for n in names):
            items.add(resource["Name"])
    return items


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    placeable = entity_creating_items(geo)
    with open(os.path.join(BUNDLE, "Entities.json"), encoding="utf-8") as f:
        entities = {e["Name"] for e in json.load(f)["Entities"]}
    entity_infos = {e["Name"]: e for e in geo["ExportedEntityInfos"]}
    mesh_infos = {m["Name"]: m for m in geo["ExportedMeshInfos"]}
    rows = []
    for resource in geo["Resources"]:
        if not str(resource.get("EquippedAction", "")).startswith("Create_") and resource["Name"] not in placeable:
            continue
        entity = resource.get("EntityBlueprint") or resource["Name"]
        if entity not in entities:
            continue
        mesh = entity_infos.get(entity, {}).get("Mesh")
        occupancy = mesh_infos.get(mesh, {}).get("VoxelOccupancy") or [{"X": 0, "Y": 0, "Z": 0}]
        xs, ys, zs = ([o[axis] for o in occupancy] for axis in "XYZ")
        cells = ";".join(f"{o['X']},{o['Y']},{o['Z']}" for o in occupancy)
        rows.append(f"{resource['Name']} {entity} {min(xs)} {max(xs)} {min(ys)} {min(zs)} {max(zs)} {cells}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(sorted(rows)) + "\n")
    print(f"{len(rows)} devices -> {OUT}")


if __name__ == "__main__":
    main()
