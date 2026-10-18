import json
import os

from action_lists import equipped_action_lists
from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "EquippedActionLists.txt")


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    rows = []
    for list_id, slot, action, _ in equipped_action_lists(geo):
        rows.append((list_id, f"{list_id} {action['Name']} {slot}"))
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(row for _, row in sorted(rows)) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
