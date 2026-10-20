import json
import os

from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "ActionStats.txt")


def stat(properties, name):
    return float(((properties or {}).get("Stat") or {}).get(name, {}).get("Value", 0.0))


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    properties = {p["Name"]: p for p in geo["PhysicalProperties"]}
    consume_all = {a["Name"] for a in geo["AlwaysActions"]
                   if (a.get("ConsumeChargeBarAction") or {}).get("ConsumeAll")}

    rows = []
    for action in geo["EquippedActions"]:
        stats = properties.get(action.get("PhysicalProperties"))
        boost = stat(stats, "ChargeBarBoost")
        modifier = stat(stats, "WeaponLevelModifier")
        entered = (action.get("OnActiveWindowEntered") or {}).get("AlwaysActions") or []
        consumes = any(a.get("ActionAlways") in consume_all for a in entered)
        if boost or modifier or consumes:
            rows.append(f"{action['Name']} {boost:g} {modifier:g} {int(consumes)}")

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("# EquippedAction ChargeBarBoost WeaponLevelModifier ConsumesChargeBar\n")
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} actions -> {OUT}")


if __name__ == "__main__":
    main()
