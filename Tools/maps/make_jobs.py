import json
import os

from targets import GAME, GEODATA

OUT = os.path.join(GAME, "Data", "Jobs.txt")


def main():
    with open(GEODATA, encoding="utf-8") as f:
        jobs = sorted(json.load(f)["Jobs"], key=lambda job: job["Index"])
    rows = [" ".join([job["Name"], "1" if job.get("Hidden") else "0", job.get("Faction") or "None",
                      str(job.get("NumberOfActiveChallenges", 0))]
                     + [str(value["Value"]) for value in job["RankExperienceValues"]])
            for job in jobs]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(rows) + "\n")
    print(f"{len(rows)} jobs -> {OUT}")


if __name__ == "__main__":
    main()
