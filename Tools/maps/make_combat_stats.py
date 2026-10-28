import json
import os

from targets import BUNDLE, GAME, GEODATA
OUT = os.path.join(GAME, "Data", "CombatStats.txt")
GAME_MODE = "HomeIsland"


def stat(properties, name):
    return properties.get("Stat", {}).get(name, {}).get("Value", 0) if properties else 0


def holds(reference, conditions, game_mode):
    name, invert = reference.get("Condition", ""), reference.get("Invert", False)
    if not name:
        return not invert
    entry = conditions.get(name)
    result = False
    if entry and entry["Test"] == "GameModeIs":
        result = entry["GameModeIs"]["GameMode"] == game_mode
    elif entry and entry["Test"] in ("ConditionOrList", "ConditionAndList"):
        outcomes = [holds(r["ConditionReference"], conditions, game_mode)
                    for r in entry[entry["Test"]]["ConditionList"]["ConditionReferences"]]
        result = any(outcomes) if entry["Test"] == "ConditionOrList" else all(outcomes)
    if entry:
        result = result != entry.get("Invert", False)
    return result != invert


def with_perks(properties, perk_names, perks, effects, conditions=None, game_mode=None):
    stats = {name: value.get("Value", 0) for name, value in properties.get("Stat", {}).items()}
    picked = [effects.get(e["Effect"], {}) for name in perk_names
              for e in perks.get(name.lower(), {}).get("Effects", [])]
    if conditions is not None:
        picked = [e for e in picked if not e.get("StatChange", {}).get("ApplyToCharacterFinalStat")
                  and holds(e.get("Condition", {}), conditions, game_mode)]
    changes = [e.get("StatChange") for e in picked]
    changes = [c for c in changes if c and c.get("Stat") in stats]
    for operation in ("Add", "Multiply", "Override"):
        for change in changes:
            if change["Operation"] != operation:
                continue
            if operation == "Add":
                stats[change["Stat"]] += change["Value"]
            elif operation == "Multiply":
                stats[change["Stat"]] *= change["Value"]
            else:
                stats[change["Stat"]] = change["Value"]
    return {"Stat": {name: {"Value": value} for name, value in stats.items()}}


def levels(properties):
    weapon = stat(properties, "WeaponLevel") + stat(properties, "WeaponLevelModifier")
    armour = stat(properties, "ArmourLevel") + stat(properties, "ArmourLevelModifier")
    return weapon, armour


def knockback(properties):
    return tuple(stat(properties, name) for name in ("KnockbackDistance", "KnockbackStrength", "KnockbackResistance"))


def row(kind, name, health, properties):
    weapon, armour = levels(properties)
    distance, strength, resistance = knockback(properties)
    creature = stat(properties, "CreatureLevel") + stat(properties, "CreatureLevelModifier")
    return f"{kind} {name} {health:g} {weapon:g} {armour:g} {distance:g} {strength:g} {resistance:g} {creature:g}"


def main():
    with open(os.path.join(BUNDLE, "GeoData.json"), encoding="utf-8") as f:
        geo = json.load(f)
    with open(os.path.join(BUNDLE, "Entities.json"), encoding="utf-8") as f:
        entities = next(iter(json.load(f).values()))
    properties = {p["Name"].lower(): p for p in geo["PhysicalProperties"]}
    perks = {p["Name"].lower(): p for p in geo["Perks"]}
    effects = {e["Name"]: e for e in geo["PerkEffects"]}
    rows = []
    for entity in entities:
        if "clienthealthcomponent" not in entity.get("client", {}).get("components", {}):
            continue
        name = entity["parameters"].get("physicalproperties", {}).get("value")
        entity_properties = properties.get(name.lower()) if name else None
        if not entity_properties:
            continue
        inherent = entity["parameters"].get("inherentperklist", {}).get("value") or []
        entity_properties = with_perks(entity_properties, inherent, perks, effects)
        rows.append(row("Entity", entity["Name"], stat(entity_properties, "Health"), entity_properties))
    conditions = {c["Name"]: c for c in geo["Conditions"]}
    for resource in geo["Resources"]:
        resource_properties = properties.get(resource.get("PhysicalProperties", "").lower())
        if resource_properties:
            perk_names = [entry["Value"] for entry in resource.get("PerkEntries", [])]
            resource_properties = with_perks(resource_properties, perk_names, perks, effects, conditions, GAME_MODE)
        if any(levels(resource_properties)) or any(knockback(resource_properties)):
            rows.append(row("Item", resource["Name"], 0, resource_properties))
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(sorted(rows)) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
