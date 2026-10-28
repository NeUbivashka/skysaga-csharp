import json
import os

from targets import GAME, GEODATA
OUT = os.path.join(GAME, "Data", "Sigils.txt")


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    rows = [f"{s['Index'] + 1} {s['Name']}" for s in sorted(geo["PlayerSigils"], key=lambda s: s["Index"])]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} sigils -> {OUT}")


if __name__ == "__main__":
    main()
