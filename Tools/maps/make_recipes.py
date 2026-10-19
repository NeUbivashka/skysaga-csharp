import json
import os

from targets import GAME, GEODATA

OUT = os.path.join(GAME, "Data", "Recipes.txt")
SLOTS = ("Primary", "Secondary", "Tertiary", "Quaternary")


def entry(value):
    if not value or not value.get("Resource"):
        return "-"
    return f"{value['Resource']}*{value.get('Quantity', 1)}"


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    resources = {r["Name"]: r for r in geo["Resources"]}
    variants = {}
    for variant in sorted(geo["RecipeVariants"], key=lambda v: v["Index"]):
        variants.setdefault(variant["Recipe"], []).append(str(variant["Index"] + 1))
    rows = []
    for recipe in sorted(geo["Recipes"], key=lambda r: r["Index"]):
        output = recipe.get("Output") or {}
        categories = resources.get(output.get("Resource"), {}).get("MaterialCategories", {})
        mask = "".join("1" if (categories.get(slot) or {}).get("Category") else "0" for slot in SLOTS)
        inputs = [entry(recipe["MaterialInputs"].get(slot)) for slot in SLOTS]
        inputs += [entry(extra) for extra in recipe.get("ExtraInputs", [])]
        rows.append(" ".join([
            recipe["Name"],
            recipe.get("CraftingDevice") or "-",
            str(recipe.get("ExecutionTimeInSeconds", 0)),
            "1" if recipe.get("MakeAvailableToNewPlayers") else "0",
            output.get("Resource") or "-",
            str(output.get("Quantity", 0)),
            str(resources.get(output.get("Resource"), {}).get("TotalValue", 0)),
            mask,
            ",".join(inputs),
            ",".join(variants.get(recipe["Name"], [])) or "-",
        ]))
    xp = geo["GlobalConstantsCrafting"][0]["ResourceValueToXP"]
    header = [
        "# GeoData.json Recipes in Index order. Line n (1-based, comments excluded) is wire index n;",
        "# the client's recipe table has a null entry 0. Columns: name device seconds newplayers output",
        "# quantity output-value output-material-slots inputs(Primary,Secondary,Tertiary,Quaternary,extras) variant-wire-indices",
        f"# variants {len(geo['RecipeVariants'])}",
        "# xp " + " ".join(str(xp[key]) for key in ("BaseXP", "ValueStepPowerTenForBaseXP", "OutputXPMin", "OutputXPMax")),
    ]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(header + rows) + "\n")
    print(f"{len(rows)} recipes -> {OUT}")


if __name__ == "__main__":
    main()
