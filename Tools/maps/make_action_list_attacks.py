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


def knockback(attack, properties):
    stats = properties.get("Stat", {}) if properties else {}
    distance = stats.get("KnockbackDistance", {}).get("Value", 0.0)
    strength = stats.get("KnockbackStrength", {}).get("Value", 0.0)
    offset = attack.get("UseKnockbackOffset", False)
    pitch = attack.get("KnockbackOffsetPitch", 0.0) if offset else 0.0
    yaw = attack.get("KnockbackOffsetYaw", 0.0) if offset else 0.0
    return f"{distance:g} {strength:g} {pitch:g} {yaw:g}"


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    table = geo["EntityActions"] if ALPHA else geo["Actions"]
    attacks = {a["Name"]: a["AttackAction"] for a in table if a.get("AttackAction")}
    properties = {p["Name"]: p for p in geo["PhysicalProperties"]}
    rows = []
    for list_id, _, action, actions in equipped_action_lists(geo):
        for name in list_entries(actions, ("EntityActions", "ActionEntity")):
            attack = attacks.get(name)
            if attack is None:
                continue
            zero = 1 if attack.get("ForceZeroDamage") else 0
            rows.append((list_id, f"{list_id} {action['Name']} {name} {zero} {lut(attack)} {knockback(attack, properties.get(action.get('PhysicalProperties', '')))}"))
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(row for _, row in sorted(rows, key=lambda r: r[0])) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
