import base64
import zlib
import json
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
GEODATA = os.path.join(ROOT, "Bundled", "36731", "GeoData.json")
OUT = os.path.join(ROOT, "Servers", "SkySaga.Game", "Data", "Customisations.txt")

MATERIAL_SLOTS = ["Skin", "Eye", "Signature"]
ATTACHMENT_SLOTS = ["HeadTop", "HeadSides", "HeadFront", "Back", "Tail"]
CUSTOMISATION_SLOTS = ["PatternA", "PatternB", "PatternEmissive", "PatternPalette",
                       "DecalA", "DecalB", "DecalPalette", "Eye", "Mouth"]
GENDERS = {"Male": 0, "Female": 1}
TRIBE_CATEGORIES = {"Skin": "CharacterSkin", "Eye": "CharacterEyes"}
OTHER_GENDER_MARK = {"Male": "_Fem", "Female": "_Mal"}
TRIBE_CUSTOMISATIONS = {"Eye": "Eye", "Mouth": "Mouth", "DecalA": "Decal", "DecalPalette": "DecalPalette"}


class Bits:
    def __init__(self):
        self.bits = []

    def ranged(self, value, width):
        full, rem = divmod(width, 8)
        for i in range(full):
            byte = (value >> (8 * i)) & 0xFF
            self.bits += [(byte >> (7 - j)) & 1 for j in range(8)]
        if rem:
            byte = (value >> (8 * full)) & ((1 << rem) - 1)
            self.bits += [(byte >> (rem - 1 - j)) & 1 for j in range(rem)]

    def count(self, value, width, escape):
        self.ranged(min(value, escape), width)
        if value >= escape:
            self.bits.append(0 if value == escape else 1)
            if value != escape:
                raise ValueError("long lists are not needed here")

    def to_bytes(self):
        full = len(self.bits) // 8 * 8
        data = [int("".join(map(str, self.bits[i:i + 8])), 2) for i in range(0, full, 8)]
        if full < len(self.bits):
            data.append(int("".join(map(str, self.bits[full:])), 2))
        return bytes(data)


def main():
    d = json.load(open(GEODATA, encoding="utf-8"))
    tables = {k: {e["Name"]: e["Index"] + 1 for e in d[k]}
              for k in ["Tribes", "Materials", "CharacterAttachments", "MaterialCustomisations", "Backstories"]}
    widths = {k: len(d[k]).bit_length() for k in tables}

    def index(table, name):
        return tables[table].get(name or "", 0)

    tribe_materials = {}
    for m in sorted(d["Materials"], key=lambda m: (not m["IsDefaultForCategory"], m["SortId"], m["Index"])):
        tribe_materials.setdefault((m["Category"], m["LockedToTribe"]), m["Name"])

    def choose(options, seed):
        options = sorted(options)
        return options[zlib.crc32(seed.encode()) % len(options)] if options else ""

    def first(values, key):
        values = values.get("Values", [])
        return values[0].get(key, "") if values else ""

    lines = []
    for lst in d["CharacterCustomisationLists"]:
        if not lst.get("Entries"):
            continue
        e = lst["Entries"][0]
        tribe = e.get("Tribe")
        gender = first(e.get("ValidGenders", {}), "Gender") or "Male"
        seed = lst["Name"].lower()
        b = Bits()
        b.ranged(GENDERS.get(gender, 0), 2)
        b.ranged(index("Tribes", e.get("Tribe")), widths["Tribes"])

        materials = e.get("MaterialEntries", {})
        b.count(len(MATERIAL_SLOTS), 2, 3)
        for slot in MATERIAL_SLOTS:
            name = first(materials.get(slot, {}).get("ValidMaterials", {}), "Material")
            if not name and slot in TRIBE_CATEGORIES:
                name = tribe_materials.get((TRIBE_CATEGORIES[slot], tribe), "")
            b.ranged(index("Materials", name), widths["Materials"])

        attachments = e.get("AttachmentEntries", {})
        b.count(len(ATTACHMENT_SLOTS), 3, 5)
        for slot in ATTACHMENT_SLOTS:
            entry = attachments.get(slot, {})
            attachment = first(entry.get("ValidAttachments", {}), "CharacterAttachment")
            material = first(entry.get("ValidAttachmentMaterials", {}), "Material")
            if not attachment and slot == "HeadTop":
                attachment = choose([a["Name"] for a in d["CharacterAttachments"]
                                     if a["LockedToTribe"] == tribe and a["PrimaryLocation"] == slot
                                     and a["Gender"] == gender], seed + slot)
            if attachment and not material:
                material = tribe_materials.get(("CharacterHair", tribe), "")
            b.ranged(index("CharacterAttachments", attachment), widths["CharacterAttachments"])
            b.ranged(index("Materials", material), widths["Materials"])

        customisations = e.get("MaterialCustomisationEntries", {})
        b.count(len(CUSTOMISATION_SLOTS), 4, 9)
        for slot in CUSTOMISATION_SLOTS:
            name = first(customisations.get(slot, {}).get("ValidMaterialCustomisations", {}), "MaterialCustomisation")
            if not name and slot in TRIBE_CUSTOMISATIONS:
                name = choose([m["Name"] for m in d["MaterialCustomisations"]
                               if m["LockedToTribe"] == tribe and m["Type"] == TRIBE_CUSTOMISATIONS[slot]
                               and OTHER_GENDER_MARK[gender] not in m["Name"]], seed + slot)
            b.ranged(index("MaterialCustomisations", name), widths["MaterialCustomisations"])

        b.ranged(0, widths["Backstories"])
        if len(b.bits) != 231:
            raise ValueError(f"{lst['Name']}: {len(b.bits)} bits")
        lines.append(f"{lst['Name'].lower()} {base64.b64encode(b.to_bytes()).decode()}")

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{OUT}: {len(lines)} customisation lists")


if __name__ == "__main__":
    main()
