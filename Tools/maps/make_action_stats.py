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
    player = properties["Creature_Player"]
    default_time = geo["UseTimes"][0]["Time"]
    consume_all = {a["Name"] for a in geo["AlwaysActions"]
                   if (a.get("ConsumeChargeBarAction") or {}).get("ConsumeAll")}

    rows = []
    for action in geo["EquippedActions"]:
        stats = properties.get(action.get("PhysicalProperties"))
        boost = stat(stats, "ChargeBarBoost")
        modifier = stat(stats, "WeaponLevelModifier")
        creature_modifier = stat(stats, "CreatureLevelModifier")
        entered = (action.get("OnActiveWindowEntered") or {}).get("AlwaysActions") or []
        consumes = any(a.get("ActionAlways") in consume_all for a in entered)
        tags = [t["Value"] for t in action.get("TagList", {}).get("Values", [])]
        duration = sum(stat(stats, f"UseTime{part}") * stat(player, f"UseTime{part}") * default_time[part]["Value"]
                       for part in ("Charge", "Active", "Recover"))
        if boost or modifier or creature_modifier or consumes or tags:
            rows.append(f"{action['Name']} {boost:g} {modifier:g} {int(consumes)} {','.join(tags) or '-'} {duration:g} {creature_modifier:g}")

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("# EquippedAction ChargeBarBoost WeaponLevelModifier ConsumesChargeBar Tags PlayerDuration CreatureLevelModifier\n")
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} actions -> {OUT}")


if __name__ == "__main__":
    main()
