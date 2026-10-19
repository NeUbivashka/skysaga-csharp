import json
import os

from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "StackLimits.txt")
OBJECT_TYPES = ["Voxel", "Generic", "Generated", "Tool", "CraftingDevice", "Currency", "Clothing", "Consumable",
                "Furniture", "PortalStone", "MysteryBox", "Key", "SpawnCreature", "LogicNetworkDevice",
                "InteractableDevice", "VoxelProp"]
TYPE_LIMITS = [999, 999, 999, 1, 999, 9999, 1, 999, 999, 999, 1, 999, 999, 999, 999, 999]


def limit(resource):
    if resource.get("RequiresDurability") or resource.get("EntityBlueprint"):
        return 1
    if resource.get("IsOverridingStackLimit"):
        return resource.get("StackLimitOverride", 0)
    return TYPE_LIMITS[OBJECT_TYPES.index(resource.get("ObjectType", "Generic"))]


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    rows = [f"{r['Name']} {limit(r)}" for r in geo["Resources"]]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} items -> {OUT}")


if __name__ == "__main__":
    main()
