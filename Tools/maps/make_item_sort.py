import json
import os
import sys

from targets import BUILD, GAME, GEODATA

OBJECT_TYPES = (
    "Voxel", "Generic", "Generated", "Tool", "CraftingDevice", "Currency", "Clothing", "Consumable",
    "Furniture", "PortalStone", "MysteryBox", "Key", "SpawnCreature", "LogicNetworkDevice",
    "InteractableDevice", "VoxelProp",
)
RARITY_LEVELS = ("Common", "Uncommon", "Rare", "Epic", "Legendary", "Unknown")


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(GAME, "Data")
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    lines = [
        f"# GeoData.json ({BUILD}) Resources: name, ObjectType index, RarityLevel index.",
        "# Enum orders are the client's name tables (ObjectType at 0x01404B60, RarityLevel at 0x01404B2C).",
    ]
    for r in sorted(geo["Resources"], key=lambda r: r["Index"]):
        lines.append(f"{r['Name']} {OBJECT_TYPES.index(r['ObjectType'])} {RARITY_LEVELS.index(r['RarityLevel'])}")
    path = os.path.join(out_dir, "ItemSort.txt")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{len(lines)} lines -> {path}")


if __name__ == "__main__":
    main()
