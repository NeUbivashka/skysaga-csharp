import json
import os

from targets import BUNDLE, GAME, GEODATA
OUT = os.path.join(GAME, "Data", "CreatureAi.txt")
PLAYER_BLUEPRINT = "Player"
REACTIONS = ("OnSight", "OnCloseRange", "OnHit")


def by_name(rows):
    return {r["Name"].lower(): r for r in rows}


def stat(properties, name):
    return float(((properties or {}).get("Stat") or {}).get(name, {}).get("Value", 0.0))


def player_relationship(tables, name):
    seen = set()
    fallback = None
    while name and name.lower() not in seen:
        seen.add(name.lower())
        table = tables.get(name.lower())
        if not table:
            break
        for row in table["Relationships"]:
            if row.get("BlueprintName") == PLAYER_BLUEPRINT:
                return row["RelationshipDescription"]
            if fallback is None and not row.get("BlueprintName") and not row.get("EntityTypes"):
                fallback = row["RelationshipDescription"]
        name = table.get("BaseTable")
    return fallback or "Ignore"


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    with open(os.path.join(BUNDLE, "Entities.json"), encoding="utf-8") as f:
        entities = next(iter(json.load(f).values()))

    properties = by_name(geo["PhysicalProperties"])
    profiles = by_name(geo["EnemyAIComponentData"])
    capabilities = by_name(geo["AICapabilities"])
    awareness = by_name(geo["AIAwarenessTraits"])
    persistence = by_name(geo["AIPersistenceTraits"])
    fighting = by_name(geo["AIFightingAbilities"])
    tables = by_name(geo["AIEntityRelationshipTables"])
    descriptions = by_name(geo["AIRelationshipDescriptions"])
    abilities = by_name(geo["Abilities"])
    actions = by_name(geo["EquippedActions"])
    windows = by_name(geo["ActiveWindows"])
    anim_states = by_name(geo["AIAnimStates"])
    default_time = geo["UseTimes"][0]["Time"]
    lut = geo["GlobalConstantsCombat"][0]["CreatureVersusPlayerDamage"]["ValueLookup"]

    rows = ["damage " + ",".join(f"{p['Input']:g}:{p['Output']:g}" for p in lut)]
    for entity in entities:
        parameters = entity["parameters"]
        if "clientenemyaicomponent" not in entity.get("client", {}).get("components", {}):
            continue
        profile = profiles.get((parameters.get("constantdata", {}).get("value") or "").lower())
        creature = properties.get((parameters.get("physicalproperties", {}).get("value") or "").lower())
        if not profile or not creature:
            continue
        capability = capabilities.get(profile["AICapabilities"].lower(), {})
        aware = awareness.get(capability.get("AwarenessTrait", "").lower(), {})
        persist = persistence.get(capability.get("AIPersistenceTrait", "").lower(), {})
        fight = fighting.get(capability.get("AIFightingAbility", "").lower(), {})
        attack_time = fight.get("AttackTime", {})
        anim = anim_states.get(capability.get("AnimState", "").lower(), {})
        description = descriptions[player_relationship(tables, profile["RelationshipTable"]).lower()]
        reactions = [description["RelationshipProperty"][r]["Relationship"] for r in REACTIONS]

        name = entity["Name"]
        rows.append(" ".join([
            "creature", name,
            f"{stat(creature, 'WalkSpeed'):g}", f"{stat(creature, 'RunSpeed'):g}", f"{stat(creature, 'JumpHeight'):g}",
            f"{aware.get('AggroSeenDistance', 0):g}", f"{aware.get('AggroSeenMinAngle', 0):g}",
            f"{aware.get('AggroCloseRangeDistance', 0):g}", f"{aware.get('AggroCloseRangeAngle', 0):g}",
            f"{persist.get('HuntTimeAfterHitInSeconds', 0):g}",
            f"{attack_time.get('NextAttackMinTime', 0):g}", f"{attack_time.get('NextAttackMaxTime', 0):g}",
            *reactions,
            f"{anim.get('AlertTime', 0):g}", f"{anim.get('LostTime', 0):g}",
        ]))

        ability = abilities.get((parameters.get("defaultactions", {}).get("value") or "").lower())
        for entry in (ability or {}).get("Actions", []):
            action = actions.get(entry["Action"].lower())
            if not action:
                continue
            tags = [t["Value"] for t in action.get("TagList", {}).get("Values", [])]
            action_properties = properties.get(action.get("PhysicalProperties", "").lower())
            window = windows.get(action.get("ActiveWindow", "").lower(), {"BeginFactor": 0.0, "EndFactor": 1.0})
            times = [stat(action_properties, f"UseTime{part}") * stat(creature, f"UseTime{part}") * default_time[part]["Value"]
                     for part in ("Charge", "Active", "Recover")]
            rows.append(" ".join([
                "ability", name, action["Name"], action.get("PhysicalProperties") or "-",
                f"{stat(action_properties, 'Reach') * stat(creature, 'Reach'):g}",
                *(f"{t:g}" for t in times),
                f"{window['BeginFactor']:g}", f"{window['EndFactor']:g}",
                ",".join(tags) or "-",
            ]))

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{sum(r.startswith('creature') for r in rows)} creatures, {sum(r.startswith('ability') for r in rows)} abilities -> {OUT}")


if __name__ == "__main__":
    main()
