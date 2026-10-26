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
            if (row.get("Condition") or {}).get("Condition"):
                continue
            if row.get("BlueprintName") == PLAYER_BLUEPRINT:
                return row["RelationshipDescription"]
            if fallback is None and not row.get("BlueprintName") and not row.get("EntityTypes"):
                fallback = row["RelationshipDescription"]
        name = table.get("BaseTable")
    return fallback or "Ignore"


def relation_rows(geo):
    tables = by_name(geo["AIEntityRelationshipTables"])
    descriptions = by_name(geo["AIRelationshipDescriptions"])
    rows = []
    for table in geo["AIEntityRelationshipTables"]:
        name, seen = table["Name"], set()
        current = table
        while current and current["Name"].lower() not in seen:
            seen.add(current["Name"].lower())
            for row in current["Relationships"]:
                if (row.get("Condition") or {}).get("Condition") or row.get("BlueprintName") == PLAYER_BLUEPRINT:
                    continue
                matcher = (f"bp:{row['BlueprintName']}" if row.get("BlueprintName")
                           else f"type:{row['EntityTypes']}" if row.get("EntityTypes") else "*")
                properties = descriptions[row["RelationshipDescription"].lower()]["RelationshipProperty"]
                rows.append(" ".join(["relation", name, matcher] + [properties[r]["Relationship"] for r in REACTIONS]))
            current = tables.get((current.get("BaseTable") or "").lower())
    return rows


AI_TESTS = ("Ai_IsInBehaviour", "Ai_HasActionInRange", "Ai_CanPathToTarget", "Ai_IsEntityUsingAbilityTag",
            "Ai_HasImmobilisedPlayer", "Constant")


def condition_ref(name, invert):
    return f"{'!' if invert else ''}{name}"


def condition_row(condition):
    test = condition.get("Test")
    name = condition["Name"]
    invert = "1" if condition.get("Invert") else "0"
    if test in ("ConditionAndList", "ConditionOrList") or condition.get("ConditionAndList") or condition.get("ConditionOrList"):
        kind = "and" if condition.get("ConditionAndList") else "or"
        refs = (condition.get("ConditionAndList") or condition.get("ConditionOrList"))["ConditionList"]["ConditionReferences"]
        return f"condition {name} {invert} {kind} " + ",".join(
            condition_ref(r["ConditionReference"]["Condition"], r["ConditionReference"].get("Invert")) for r in refs)
    if test not in AI_TESTS:
        return f"condition {name} {invert} unsupported {test}"
    body = condition.get(test) or {}
    if test == "Ai_IsInBehaviour":
        return f"condition {name} {invert} behaviour {body['Behaviour']}"
    if test in ("Ai_HasActionInRange", "Ai_IsEntityUsingAbilityTag"):
        tags = ",".join(v["Value"] for v in body["ActionTagList"]["Values"])
        kind = "inrange" if test == "Ai_HasActionInRange" else f"targetusing:{condition.get('EntityType', 'TargetEntity')}"
        return f"condition {name} {invert} {kind} {tags}"
    if test == "Ai_CanPathToTarget":
        return f"condition {name} {invert} canpath"
    if test == "Ai_HasImmobilisedPlayer":
        return f"condition {name} {invert} immobilised"
    return f"condition {name} {invert} constant {body.get('Value', 'False')}"


def selection_rows(geo):
    conditions = by_name(geo["Conditions"])
    rows = []
    needed = []
    for table in geo["AbilitySelectionTable"]:
        rules = [f"{r['AbilitySelectionList']}:{condition_ref(r['Condition']['Condition'], r['Condition'].get('Invert'))}"
                 for r in table["AbilityTableSelectionRule"]]
        needed += [r["Condition"]["Condition"] for r in table["AbilityTableSelectionRule"]]
        rows.append(f"selectiontable {table['Name']} " + " ".join(rules))
    for selection in geo["AbilitySelectionList"]:
        rules = [",".join(v["Value"] for v in r["ActionTagList"]["Values"])
                 + f":{condition_ref(r['Condition']['Condition'], r['Condition'].get('Invert'))}:{int(bool(r.get('SelectIfTrue', True)))}"
                 for r in selection["AbilitySelectionRule"]]
        needed += [r["Condition"]["Condition"] for r in selection["AbilitySelectionRule"]]
        rows.append(f"selectionlist {selection['Name']} " + " ".join(rules))
    done = set()
    while needed:
        name = needed.pop()
        if name.lower() in done or name.lower() not in conditions:
            continue
        done.add(name.lower())
        condition = conditions[name.lower()]
        row = condition_row(condition)
        rows.append(row)
        if " and " in row or " or " in row:
            needed += [ref.lstrip("!") for ref in row.split(" ")[-1].split(",")]
    return rows


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
    hearings = by_name(geo["AIHearingTraits"])
    movements = by_name(geo["AIMovementTraits"])
    slots = by_name(geo["AITimeSlots"])
    sleeps = by_name(geo["AISleepTraits"])
    restlessness = by_name(geo["AIRestlessnessTraits"])
    always = by_name(geo["AlwaysActions"])
    projectile_data = by_name(geo["ProjectileComponentData"])
    blueprints = {e["Name"].lower(): e for e in entities}
    loudness = geo["GlobalConstants"][0]["SensoryEventLoudness"]
    default_time = geo["UseTimes"][0]["Time"]
    lut = geo["GlobalConstantsCombat"][0]["CreatureVersusPlayerDamage"]["ValueLookup"]
    dodge_control = geo["GlobalConstants"][0]["Control"]["Dodge"]

    rows = ["damage " + ",".join(f"{p['Input']:g}:{p['Output']:g}" for p in lut)]
    rows.append("damagecreature " + ",".join(f"{p['Input']:g}:{p['Output']:g}"
                                             for p in geo["GlobalConstantsCombat"][0]["CreatureVersusCreatureDamage"]["ValueLookup"]))
    constants = geo["GlobalConstants"][0]
    rows.append(f"invisibility spawn={constants['AIInvisibilityOnSpawnTime']:g} respawn={constants['AIInvisibilityOnRespawnTime']:g}")
    rows += [f"spawnparams {spawn['Name']} home={spawn['HomeRange']:g} roam={spawn['MaxDistanceFromSpawnPosition']:g}"
             for spawn in geo["SpawnParams"]]
    rows += [f"loudness {event} {value['Loudness']}" for event, value in loudness.items() if value.get("Loudness")]
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

        hearing = hearings.get(capability.get("AIHearingTrait", "").lower(), {})
        heard = description["RelationshipProperty"]["OnHearing"]
        movement = {m["MovementState"]: m["ConfigData"]
                    for m in movements.get(capability.get("MovementTrait", "").lower(), {}).get("MovementStates", [])}
        back_off = movement.get("BackOffAfterAttack")

        def restless(slot_name):
            slot = slots.get(slot_name.lower(), {})
            trait = restlessness.get(slot.get("RestlessnessTrait", "").lower(), {})
            return f"{trait.get('MinTimeToIdleInSeconds', 0):g}:{trait.get('MaxTimeToIdleInSeconds', 0):g}"

        def sleep(slot_name):
            slot = slots.get(slot_name.lower(), {})
            trait = sleeps.get(slot.get("SleepTrait", "").lower(), {})
            if not trait.get("CanSleep"):
                return "0:0:0:0"
            return ":".join(f"{trait[k]:g}" for k in ("MinTimeIdleToSleepInSeconds", "MaxTimeIdleToSleepInSeconds",
                                                       "MinTimeToSleepInSeconds", "MaxTimeToSleepInSeconds"))

        fields = {
            "walk": stat(creature, "WalkSpeed"), "run": stat(creature, "RunSpeed"), "sprint": stat(creature, "SprintSpeed"),
            "jump": stat(creature, "JumpHeight"),
            "seen": aware.get("AggroSeenDistance", 0), "seenangle": aware.get("AggroSeenMinAngle", 0),
            "close": aware.get("AggroCloseRangeDistance", 0), "closeangle": aware.get("AggroCloseRangeAngle", 0),
            "wake": aware.get("WakeFromSleepDistance", 0),
            "hunt": persist.get("HuntTimeAfterHitInSeconds", 0),
            "nextmin": attack_time.get("NextAttackMinTime", 0), "nextmax": attack_time.get("NextAttackMaxTime", 0),
            "nearmin": fight.get("NearRangeMin", 0), "nearmax": fight.get("NearRangeMax", 0),
            "farmin": fight.get("FarRangeMin", 0), "farmax": fight.get("FarRangeMax", 0),
            "squad": ":".join(f"{fight.get('SquadAttackScoring', {}).get(k, 0):g}" for k in (
                "AttackPriorityScore", "NearRangeZoneScore", "FarRangeZoneScore", "HitRecentlyScore", "OutOfCombatZoneScore")),
            "leader": ":".join(f"{fight.get('SquadLeaderValues', {}).get(k, 0):g}" for k in (
                "DesiredNearFighters", "DesiredFarFighters", "MinAngleToNextAI")),
            "hitrecently": capability.get("TimeForHitRecently", 0),
            "circle": int(bool(fight.get("CircleTarget"))),
            "attackmod": attack_time.get("AttackTimeModifier", 0),
            "attackmodmin": attack_time.get("MinNumForAttackTimeModification", 0),
            "onsight": reactions[0], "onclose": reactions[1], "onhit": reactions[2], "onhearing": heard["Relationship"],
            "hearing": ",".join(v["Value"] for v in heard["HearingEventTypes"]["Values"]) or "-",
            "quiet": hearing.get("Awake1QuietRange", 0), "normal": hearing.get("Awake2NormalRange", 0), "loud": hearing.get("Awake3LoudRange", 0),
            "sleepquiet": hearing.get("Sleep1QuietRange", 0), "sleepnormal": hearing.get("Sleep2NormalRange", 0), "sleeploud": hearing.get("Sleep3LoudRange", 0),
            "alert": anim.get("AlertTime", 0), "lost": anim.get("LostTime", 0),
            "type": profile.get("EntityTypes") or "-",
            "broadcast": anim.get("BroadcastTime", 0), "broadcastdelay": anim.get("BroadcastDelay", 0),
            "taunt": anim.get("TauntTime", 0), "ignoretaunt": persist.get("IgnoreAfterTauntTime", 0),
            "selection": fight.get("AbilitySelectionTable") or "-", "ranged": int(bool(fight.get("IsRangedFighter"))),
            "dodge": fight.get("DodgeProbability", 0), "dodgedistance": stat(creature, "DodgeDistance"),
            "dodgespeed": stat(creature, "DodgeSpeed"), "dodgegap": dodge_control["MinTimeBetweenDodges"],
            "dodgeside": int(bool(dodge_control["PrioritiseSideDodge"])),
            "runfromhealth": capability.get("RunFromTargetHealthThreshold", 0),
            "avoidance": capability.get("AvoidanceRange", 0),
            "relations": profile["RelationshipTable"] or "-",
            "toattack": movement.get("ToAttack", {}).get("MovementType", "Run"),
            "totarget": movement.get("ToTarget", {}).get("MovementType", "Run"),
            "backoff": back_off["MovementType"] if back_off else "-",
            "backoffmin": (back_off or {}).get("MinAngleOffset", 0), "backoffmax": (back_off or {}).get("MaxAngleOffset", 0),
            "backofflook": int(bool((back_off or {}).get("LookAtTarget"))),
            "idle": restless(capability.get("TimeSegmentDefault", "")),
            "sleep": sleep(capability.get("TimeSegmentDefault", "")),
            "segments": ";".join(f"{seg['StartTime']:g}:{seg['EndTime']:g}:{sleep(seg['AITimeSlot'])}"
                                 for seg in capability.get("TimeSegment", [])) or "-",
        }
        name = entity["Name"]
        rows.append(" ".join(["creature", name] + [f"{k}={v:g}" if isinstance(v, float) else f"{k}={v}" for k, v in fields.items()]))

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

    rows += selection_rows(geo)
    rows += relation_rows(geo)

    for action in geo["EquippedActions"]:
        for entry in (action.get("OnActiveWindowEntered") or {}).get("AlwaysActions") or []:
            block = always.get(entry.get("ActionAlways", "").lower(), {}).get("BlockAction")
            if block:
                rows.append(f"block {action['Name']} {block['Angle']:g}")

    launched = set()
    for action in geo["EquippedActions"]:
        for entry in (action.get("OnActiveWindowEntered") or {}).get("AlwaysActions") or []:
            launch = always.get(entry.get("ActionAlways", "").lower(), {}).get("LaunchProjectileAction")
            if not launch or action["Name"] in launched:
                continue
            blueprint = blueprints.get(launch["Projectile"].lower())
            data_name = ((blueprint or {}).get("parameters", {}).get("projectilecomponentdata", {}).get("value") or "").lower()
            data = projectile_data.get(data_name)
            if not blueprint or not data:
                continue
            launched.add(action["Name"])
            transform = launch.get("Transform", {})
            offset = transform.get("UseOffset")
            position = transform.get("UsePositionOffset")
            impact = properties.get(data.get("OnImpactPhysicalProperties", "").lower())
            fields = {
                "entity": blueprint["Name"],
                "dataindex": data["Index"], "datacount": len(geo["ProjectileComponentData"]),
                "speed": float(launch["MaxProjectileSpeed"]), "gravity": float(launch["GravityFactor"]),
                "inaccmin": float(launch["MinInaccuracyAngle"]), "inaccmax": float(launch["MaxInaccuracyAngle"]),
                "offx": float(transform.get("OffsetX", 0)) if position else 0.0,
                "offy": float(transform.get("OffsetY", 0)) if position else 0.0,
                "offz": float(transform.get("OffsetZ", 0)) if position else 0.0,
                "pitch": float(transform.get("OffsetPitch", 0)) if offset else 0.0,
                "yaw": float(transform.get("OffsetYaw", 0)) if offset else 0.0,
                "lifetime": float(data.get("Lifetime", 0)),
                "impactlevel": stat(impact, "WeaponLevelModifier"),
                "homing": float(launch.get("HomingMaxAngle", 0)),
            }
            rows.append(" ".join(["projectile", action["Name"]] + [f"{k}={v:g}" if isinstance(v, float) else f"{k}={v}" for k, v in fields.items()]))

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{sum(r.startswith('creature') for r in rows)} creatures, {sum(r.startswith('ability') for r in rows)} abilities -> {OUT}")


if __name__ == "__main__":
    main()
