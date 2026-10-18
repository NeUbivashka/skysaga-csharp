import json
import os

from targets import BUNDLE, GAME, GEODATA
OUT = os.path.join(GAME, "Data", "JobChallenges.txt")


def main():
    d = json.load(open(GEODATA, encoding="utf-8"))
    names = [c["Name"] for c in d["JobChallenges"]]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(names) + "\n")
    print(f"{OUT}: {len(names)} job challenges")


if __name__ == "__main__":
    main()
