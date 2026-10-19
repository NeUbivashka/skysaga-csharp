import json
import os

from targets import BUNDLE, GAME, GEODATA
OUT = os.path.join(GAME, "Data", "Devices.txt")


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    with open(os.path.join(BUNDLE, "Entities.json"), encoding="utf-8") as f:
        entities = {e["Name"] for e in json.load(f)["Entities"]}
    entity_infos = {e["Name"]: e for e in geo["ExportedEntityInfos"]}
    mesh_infos = {m["Name"]: m for m in geo["ExportedMeshInfos"]}
    rows = []
    for resource in geo["Resources"]:
        if not str(resource.get("EquippedAction", "")).startswith("Create_"):
            continue
        entity = resource.get("EntityBlueprint") or resource["Name"]
        if entity not in entities:
            continue
        mesh = entity_infos.get(entity, {}).get("Mesh")
        occupancy = mesh_infos.get(mesh, {}).get("VoxelOccupancy") or [{"X": 0, "Y": 0, "Z": 0}]
        xs, ys, zs = ([o[axis] for o in occupancy] for axis in "XYZ")
        rows.append(f"{resource['Name']} {entity} {min(xs)} {max(xs)} {min(ys)} {min(zs)} {max(zs)}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(sorted(rows)) + "\n")
    print(f"{len(rows)} devices -> {OUT}")


if __name__ == "__main__":
    main()
