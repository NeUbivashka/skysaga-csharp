import json
import os

from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "TagNames.txt")


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    rows = [f"{t['Name']} {t['QuestString']}" for t in geo["Tags"] if t.get("QuestString")]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} tags -> {OUT}")


if __name__ == "__main__":
    main()
