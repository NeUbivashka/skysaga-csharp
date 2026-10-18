import json
import os

from targets import BUNDLE, GAME, GEODATA
OUT = os.path.join(GAME, "Data", "ToolStats.txt")


def stat(properties, name, default):
    return properties.get("Stat", {}).get(name, {}).get("Value", default) if properties else default


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    properties = {p["Name"]: p for p in geo["PhysicalProperties"]}
    actions = {a["Name"]: a for a in geo["EquippedActions"]}
    rows = []
    for resource in geo["Resources"]:
        action = actions.get(resource.get("EquippedAction"))
        tool = properties.get(resource.get("PhysicalProperties"))
        if not action or not tool:
            continue
        action_properties = properties.get(action.get("PhysicalProperties"))
        dig_level = stat(action_properties, "DigLevel", 0) + stat(tool, "DigLevel", 0)
        tier = stat(action_properties, "MiningPowerTier", 1) * stat(tool, "MiningPowerTier", 1)
        rows.append(f"{resource['Name']} {dig_level:g} {tier:g}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(sorted(rows)) + "\n")
    print(f"{len(rows)} tools -> {OUT}")


if __name__ == "__main__":
    main()
