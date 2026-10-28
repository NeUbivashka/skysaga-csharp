import json
import os

from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "Unequippable.txt")


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    rows = [r["Name"] for r in geo["Resources"] if r.get("IsEquippableItem") is False]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} items -> {OUT}")


if __name__ == "__main__":
    main()
