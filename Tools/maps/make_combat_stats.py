import json
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
BUNDLE = os.path.join(ROOT, "Bundled", "36731")
OUT = os.path.join(ROOT, "Servers", "SkySaga.Game", "Data", "CombatStats.txt")


def stat(properties, name):
    return properties.get("Stat", {}).get(name, {}).get("Value", 0) if properties else 0


def levels(properties):
    weapon = stat(properties, "WeaponLevel") + stat(properties, "WeaponLevelModifier")
    armour = stat(properties, "ArmourLevel") + stat(properties, "ArmourLevelModifier")
    return weapon, armour


def main():
    with open(os.path.join(BUNDLE, "GeoData.json"), encoding="utf-8") as f:
        geo = json.load(f)
    with open(os.path.join(BUNDLE, "Entities.json"), encoding="utf-8") as f:
        entities = next(iter(json.load(f).values()))
    properties = {p["Name"].lower(): p for p in geo["PhysicalProperties"]}
    rows = []
    for entity in entities:
        if "clienthealthcomponent" not in entity.get("client", {}).get("components", {}):
            continue
        name = entity["parameters"].get("physicalproperties", {}).get("value")
        entity_properties = properties.get(name.lower()) if name else None
        if not entity_properties:
            continue
        weapon, armour = levels(entity_properties)
        rows.append(f"{entity['Name']} {stat(entity_properties, 'Health'):g} {weapon:g} {armour:g}")
    for resource in geo["Resources"]:
        resource_properties = properties.get(resource.get("PhysicalProperties", "").lower())
        weapon, armour = levels(resource_properties)
        if weapon or armour:
            rows.append(f"{resource['Name']} 0 {weapon:g} {armour:g}")
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(sorted(rows)) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
