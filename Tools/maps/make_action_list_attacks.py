import json
import os

from targets import BUNDLE, GAME, GEODATA
OUT = os.path.join(GAME, "Data", "ActionListAttacks.txt")
FIRST_EQUIPPED_ACTION_LIST_ID = 1262


def lut(attack):
    if not attack.get("OverrideDamageLUT"):
        return "global"
    points = attack.get("DamageLUT", {}).get("ValueLookup", [])
    return ",".join(f"{p['Input']:g}:{p['Output']:g}" for p in points) or "global"


def action_lists(action):
    area = action.get("AreaOfEffect", {}).get("ContextCollectionParams", {}).get("Actions", {})
    return [area, action.get("OnActiveWindowEntered", {}), action.get("OnActiveWindowExited", {})]


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    attacks = {a["Name"]: a["AttackAction"] for a in geo["EntityActions"] if a.get("AttackAction")}
    rows = []
    for action in geo["EquippedActions"]:
        for slot, actions in enumerate(action_lists(action)):
            list_id = FIRST_EQUIPPED_ACTION_LIST_ID + 3 * action["Index"] + slot
            for entry in actions.get("EntityActions", []):
                attack = attacks.get(entry.get("ActionEntity"))
                if attack is None:
                    continue
                zero = 1 if attack.get("ForceZeroDamage") else 0
                rows.append(f"{list_id} {action['Name']} {entry['ActionEntity']} {zero} {lut(attack)}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
