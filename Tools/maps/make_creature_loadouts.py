import json
import os

from targets import BUILD, BUNDLE, GAME, GEODATA
OUT = os.path.join(GAME, "Data", "CreatureLoadouts.txt")

SLOTS = (["LeftHand", "RightHand", "Head", "Torso", "Legs", "Arms", "Device", "Backpack", "AdditionalHeadFeature"]
         if BUILD == "36731" else
         ["LeftHand", "RightHand", "Head", "Torso", "Legs", "Arms", "Backpack", "Special", "AdditionalHeadFeature",
          "InfiniteLeftHand", "InfiniteRightHand"])
MATERIAL_ORDER = ["Primary", "Secondary", "Tertiary", "Quaternary"]


def biome_of(entity):
    for tag in entity.get("parameters", {}).get("taglist", {}).get("value") or []:
        if tag.lower().startswith("nativeto"):
            return tag[len("nativeto"):].lower()
    return None


def pick(items, biome):
    def allowed(item):
        rule = item.get("Filter", {}).get("ValidBiomes", {})
        values = [v["Biome"].lower() for v in rule.get("Values", [])]
        if not values:
            return True
        return (biome in values) != rule.get("Invert", False)

    def unfiltered(item):
        return not item.get("Filter", {}).get("ValidBiomes", {}).get("Values")

    return (next((i for i in items if biome and allowed(i)), None)
            or next((i for i in items if unfiltered(i)), None)
            or items[0])


def main():
    d = json.load(open(GEODATA, encoding="utf-8"))
    loadouts = {r["Name"].lower(): r for r in d["InventoryLoadOuts"]}
    lists = {r["Name"]: r for r in d["InventoryLoadOutLists"]}

    defaults = {}
    with open(os.path.join(GAME, "Data", "Materials.txt"), encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if len(parts) == 5:
                defaults[parts[0]] = parts[1:]

    data = json.load(open(os.path.join(GAME, "Data", "Entities.json"), encoding="utf-8"))
    entities = data["Entities"] if isinstance(data, dict) else data

    lines = []
    for e in entities:
        if "clientequippeditemcomponent" not in e.get("client", {}).get("components", {}):
            continue
        name = (e.get("parameters", {}).get("inventoryloadout", {}).get("value") or "").lower()
        loadout = loadouts.get(name)
        if not loadout:
            continue
        equipment = []
        for entry in loadout["Entries"]:
            slots = entry.get("Slots") or []
            items = lists.get(entry["InventoryLoadOutList"], {}).get("Entries") or []
            if not slots or not items:
                continue
            spec = pick(items, biome_of(e))["ItemSpec"]
            resource = spec["Resource"]
            if resource not in defaults:
                continue
            materials = list(defaults[resource])
            for i, key in enumerate(MATERIAL_ORDER):
                chosen = spec["Materials"].get(key, {}).get("MaterialOrDefault", {})
                if chosen.get("Type") == "Specific" and chosen.get("Material") and materials[i] != "-":
                    materials[i] = chosen["Material"]
            materials = [m for m in materials if m != "-"]
            equipment.append(f"{SLOTS.index(slots[0])}={resource}:{','.join(materials)}")
        if equipment:
            lines.append(f"{e['Name'].lower()} {' '.join(equipment)}")

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{OUT}: {len(lines)} entity types")


if __name__ == "__main__":
    main()
