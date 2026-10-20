import json
import os

from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "ChargeBar.txt")
PLAYER_PROPERTIES = "Creature_Player"


def stat(properties, name):
    return float(((properties or {}).get("Stat") or {}).get(name, {}).get("Value", 0.0))


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    properties = {p["Name"]: p for p in geo["PhysicalProperties"]}
    player = properties[PLAYER_PROPERTIES]
    constants = geo["GlobalConstants"][0]["ChargeBar"]

    rows = [
        f"player {stat(player, 'ChargeBar'):g} {stat(player, 'ChargeBarBoost'):g} {stat(player, 'ChargeBarRecoveryRate'):g}",
        f"pause {float(constants['PauseTime']):g}",
    ]
    actions = [(a["Name"], stat(properties.get(a.get("PhysicalProperties")), "ChargeBarBoost")) for a in geo["EquippedActions"]]
    rows += [f"action {name} {boost:g}" for name, boost in actions if boost]
    items = [(r["Name"], stat(properties.get(r.get("PhysicalProperties")), "ChargeBarBoost")) for r in geo["Resources"]]
    rows += [f"item {name} {boost:g}" for name, boost in items if boost]

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{sum(1 for _, b in actions if b)} actions, {sum(1 for _, b in items if b)} items -> {OUT}")


if __name__ == "__main__":
    main()
