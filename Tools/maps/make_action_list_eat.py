import json
import os

from action_lists import ALPHA, equipped_action_lists, list_entries
from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "ActionListEat.txt")


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    table = geo["AlwaysActions"] if ALPHA else geo["Actions"]
    eats = {a["Name"]: a["EatAction"] for a in table if a.get("EatAction")}
    rows = []
    for list_id, _, action, actions in equipped_action_lists(geo):
        for name in list_entries(actions, ("AlwaysActions", "ActionAlways")):
            eat = eats.get(name)
            if eat is None:
                continue
            rows.append((list_id, f"{list_id} {action['Name']} {name} {eat.get('HealthBoost', 0):g}"))
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(row for _, row in sorted(rows, key=lambda r: r[0])) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
