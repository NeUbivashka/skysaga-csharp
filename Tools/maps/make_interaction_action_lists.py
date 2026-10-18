import json
import os

from action_lists import INTERACTION_ACTION_LIST_IDS
from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "InteractionActionLists.txt")


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    categories = {a["Name"]: a["Category"] for a in geo["Actions"]}
    lists = geo["GlobalConstantsUI"][0]["InteractionActions"]
    rows = []
    for name, list_id in INTERACTION_ACTION_LIST_IDS.items():
        for entry in lists[name].get("ActionArray", []):
            rows.append((list_id, f"{list_id} {name} {entry['Action']} {categories[entry['Action']]}"))
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(row for _, row in sorted(rows)) + "\n")
    print(f"{len(rows)} rows -> {OUT}")


if __name__ == "__main__":
    main()
