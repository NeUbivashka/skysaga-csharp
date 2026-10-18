import json
import os

from action_lists import ALPHA, equipped_action_lists, list_entries
from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "ActionListAttacks.txt")


def lut(attack):
    if not attack.get("OverrideDamageLUT"):
        return "global"
    points = attack.get("DamageLUT", {}).get("ValueLookup", [])
    return ",".join(f"{p['Input']:g}:{p['Output']:g}" for p in points) or "global"


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    table = geo["EntityActions"] if ALPHA else geo["Actions"]
    attacks = {a["Name"]: a["AttackAction"] for a in table if a.get("AttackAction")}
    rows = []
    for list_id, _, action, actions in equipped_action_lists(geo):
        for name in list_entries(actions, ("EntityActions", "ActionEntity")):
            attack = attacks.get(name)
            if attack is None:
                continue
            zero = 1 if attack.get("ForceZeroDamage") else 0
            rows.append((list_id, f"{list_id} {action['Name']} {name} {zero} {lut(attack)}"))
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(row for _, row in sorted(rows, key=lambda r: r[0])) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
