import json
import os

from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "Perks.txt")


def by_name(rows):
    return {r["Name"].lower(): r for r in rows}


def condition_name(reference):
    name = (reference or {}).get("Condition") or ""
    if not name:
        return "-"
    return ("!" if reference.get("Invert") else "") + name


def alter_hp(entity_actions, equipped_action):
    total = 0.0
    for entry in (equipped_action.get("OnActiveWindowEntered") or {}).get("EntityActions") or []:
        action = entity_actions.get(entry.get("ActionEntity", "").lower(), {})
        alter = action.get("AlterHPAction")
        if not alter or alter.get("Style") != "Quantity":
            continue
        hp = float((alter.get("QuantityStyle") or {}).get("HP", 0))
        total += -hp if alter.get("Direction") == "Negative" else hp
    return total


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    entity_actions = by_name(geo["EntityActions"])
    equipped = by_name(geo["EquippedActions"])
    conditions = by_name(geo["Conditions"])

    rows = []
    for perk in geo["Perks"]:
        effects = ",".join(e["Effect"] for e in perk.get("Effects") or [] if e.get("Effect")) or "-"
        rows.append(f"perk {perk['Name']} stackable={int(bool(perk.get('Stackable')))} effects={effects}")

    for effect in geo["PerkEffects"]:
        kind = effect.get("EffectType")
        condition = condition_name(effect.get("Condition"))
        if kind == "StatChange":
            change = effect["StatChange"]
            rows.append(f"perkeffect {effect['Name']} statchange condition={condition} stat={change['Stat']} op={change['Operation']} "
                        f"value={float(change['Value']):g} final={int(bool(change.get('ApplyToCharacterFinalStat')))}")
        elif kind == "PeriodicAction":
            periodic = effect["PeriodicActions"]
            action = equipped.get(periodic["Action"].lower(), {})
            rows.append(f"perkeffect {effect['Name']} periodic condition={condition} action={periodic['Action']} "
                        f"interval={float(periodic['IntervalSeconds']):g} hp={alter_hp(entity_actions, action):g}")
        else:
            rows.append(f"perkeffect {effect['Name']} {kind.lower()} condition={condition}")

    used = set()
    for action in geo["EntityActions"]:
        if action.get("Category") != "ApplyPerkAction":
            continue
        entries = []
        for entry in action["ApplyPerkAction"].get("PerkEntries") or []:
            value = entry["Value"]
            duration = value.get("Duration") or {}
            expiry = condition_name(duration.get("Condition"))
            if expiry != "-":
                used.add(expiry.lstrip("!").lower())
            entries.append(f"{value['Perk']}:{float(duration.get('DurationInSeconds', 0)):g}:{expiry}")
        rows.append(" ".join(["applyperk", action["Name"]] + entries))

    pending = sorted(used)
    done = set()
    while pending:
        name = pending.pop()
        if name in done or name not in conditions:
            continue
        done.add(name)
        condition = conditions[name]
        if condition.get("Test") == "EventType":
            event = condition["EventType"]
            rows.append(f"perkcondition {condition['Name']} event={event['EventTypeEnum']} hits={event.get('NumOfHits', 1)} "
                        f"entity={condition.get('EntityType', '-')} invert={int(bool(condition.get('Invert')))}")
        elif condition.get("Test") == "ConditionOrList":
            refs = [r["ConditionReference"] for r in condition["ConditionOrList"]["ConditionList"]["ConditionReferences"]]
            rows.append(f"perkcondition {condition['Name']} or {','.join(condition_name(r) for r in refs)} invert={int(bool(condition.get('Invert')))}")
            pending += [r["Condition"].lower() for r in refs]
        else:
            rows.append(f"perkcondition {condition['Name']} unsupported {condition.get('Test')}")

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(geo['Perks'])} perks, {len(geo['PerkEffects'])} effects -> {OUT}")


if __name__ == "__main__":
    main()
