import os

from targets import BUNDLE, GAME
SCENE = os.path.join(BUNDLE, "w_Trees.d")
OUT = os.path.join(GAME, "Data", "Trees.txt")

TREE_TYPE = 0xDA4C317A
MESH_LIST = 0x1B4087AB
PARTS_LIST = 0xEDEA00EC
PART = 0x8BE33A9E
MESH = 0x89E36A51
ROTATION = (0x62821875, 0x9082B213, 0x8242510E)
TYPE_NAMES = ("ForestTree", "DesertCactus", "MountainTree", "PalmTree", "BambooTree", "Vine", "Root", "Ivy",
              "Carbon_Deposit", "Metal_Deposit", "Keystone_Deposit", "Ivy_Jungle", "Ivy_Arctic", "Ivy_Desert")


def crc32_name(name):
    value = 0
    for byte in name.lower().encode():
        value ^= byte << 24
        for _ in range(8):
            value = ((value << 1) ^ 0x04C11DB7) & 0xFFFFFFFF if value & 0x80000000 else (value << 1) & 0xFFFFFFFF
    return value


def read_record(lines, i):
    i += 5
    fields = []
    while lines[i] != "-1":
        field = int(lines[i + 1], 16)
        if lines[i + 3].startswith('"'):
            count = int(lines[i + 2])
            i += 3
            items = []
            for _ in range(count):
                item, i = read_record(lines, i)
                items.append(item)
            fields.append((field, items))
        else:
            fields.append((field, lines[i + 2]))
            i += 3
    return dict(fields), i + 1


def number(text):
    return float(text.rstrip("."))


def main():
    with open(SCENE, encoding="latin1") as f:
        lines = [line.strip() for line in f]
    definitions, _ = read_record(lines, lines.index('"TreeDefinitionList" 1'))
    names = {crc32_name(name): name for name in TYPE_NAMES}
    meshes = {crc32_name(name): name for name in {line.strip('"') for line in lines if line.startswith('"Scatter')}}

    rows = []
    for tree in next(iter(definitions.values())):
        tree_type = int(tree[TREE_TYPE], 16)
        slots = [meshes.get(int(mesh[next(iter(mesh))].split()[0], 16), "-") for mesh in tree[MESH_LIST]]
        parts = []
        for part in tree[PARTS_LIST]:
            mesh = int(part[MESH])
            rotation = ":".join(f"{number(part[axis]):g}" for axis in ROTATION)
            parts.append(f"{part[PART]}:{mesh}:{slots[mesh] if mesh < len(slots) else '-'}:{rotation}")
        rows.append(f"treetype {names.get(tree_type, f'{tree_type:08x}')} {tree_type:08x} " + " ".join(parts))

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} tree types -> {OUT}")


if __name__ == "__main__":
    main()
