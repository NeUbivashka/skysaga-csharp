import json
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
GEODATA = os.path.join(ROOT, "Bundled", "36731", "GeoData.json")
OUT = os.path.join(ROOT, "Servers", "SkySaga.Game", "Data", "ActionListAttacks.txt")
ACTION_LIST_ID_OFFSET = 1101


def is_action_list(node):
    return isinstance(node, dict) and all(k in node for k in ("AlwaysActions", "EntityActions", "VoxelActions"))


def collect(node, path, out):
    if isinstance(node, dict):
        if is_action_list(node):
            out.append((path, node))
        for key, value in node.items():
            collect(value, path + [key], out)
    elif isinstance(node, list):
        for value in node:
            collect(value, path + [value.get("Name", "") if isinstance(value, dict) else ""], out)


def lut(attack):
    if not attack.get("OverrideDamageLUT"):
        return "global"
    points = attack.get("DamageLUT", {}).get("ValueLookup", [])
    return ",".join(f"{p['Input']:g}:{p['Output']:g}" for p in points) or "global"


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    attacks = {a["Name"]: a["AttackAction"] for a in geo["EntityActions"] if a.get("AttackAction")}
    lists = []
    collect(geo, [], lists)
    rows = []
    for index, (path, node) in enumerate(lists):
        if len(path) < 2 or path[0] != "EquippedActions":
            continue
        for entry in node["EntityActions"]:
            attack = attacks.get(entry.get("ActionEntity"))
            if attack is None:
                continue
            zero = 1 if attack.get("ForceZeroDamage") else 0
            rows.append(f"{index + ACTION_LIST_ID_OFFSET} {path[1]} {entry['ActionEntity']} {zero} {lut(attack)}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
