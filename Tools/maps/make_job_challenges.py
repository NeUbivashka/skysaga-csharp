import json
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
GEODATA = os.path.join(ROOT, "Bundled", "36731", "GeoData.json")
OUT = os.path.join(ROOT, "Servers", "SkySaga.Game", "Data", "JobChallenges.txt")


def main():
    d = json.load(open(GEODATA, encoding="utf-8"))
    names = [c["Name"] for c in d["JobChallenges"]]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(names) + "\n")
    print(f"{OUT}: {len(names)} job challenges")


if __name__ == "__main__":
    main()
