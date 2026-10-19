import json
import os

from targets import GAME, GEODATA

NAMES_OUT = os.path.join(GAME, "Data", "JobChallenges.txt")
DATA_OUT = os.path.join(GAME, "Data", "JobChallenges.json")
CONDITIONS_OUT = os.path.join(GAME, "Data", "Conditions.json")

CONDITION_TESTS = {"JobChallengeComplete", "JobChallengeActive", "ConditionOrList", "ConditionAndList"}


def challenge(c):
    params = c.get(c["ActionName"] + "Params", {})
    condition = c.get("Condition") or {}
    unlocked = c.get("UnlockedBy") or {}
    time_of_day = c.get("TimeOfDay") or {}
    return {
        "Name": c["Name"],
        "Job": c.get("Job", ""),
        "Action": c["ActionName"],
        "Number": c.get("NumberRequired", 1),
        "Xp": (c.get("Awards") or {}).get("Experience", 0),
        "GrinderXp": (c.get("GrinderAwards") or {}).get("Experience", 0),
        "Dailies": bool(c.get("AvailableForDailies")),
        "Master": bool(c.get("IsMasterChallenge")),
        "AutoActivate": bool(c.get("AutoActivate")),
        "InQuestLog": bool(c.get("ShowInQuestLog")),
        "Reactivate": bool((c.get("Repeatability") or {}).get("CanBeReactivated")),
        "Condition": condition.get("Condition", ""),
        "ConditionInvert": bool(condition.get("Invert")),
        "Next": (c.get("Chain") or {}).get("NextChallengeInChain", ""),
        "HandInToNpc": c.get("HandInToNPC", ""),
        "Events": [e["Event"] for e in (c.get("ValidEvents") or {}).get("Values", [])],
        "EventsInvert": bool((c.get("ValidEvents") or {}).get("Invert")),
        "UnlockJob": unlocked.get("Job", ""),
        "UnlockRank": unlocked.get("JobRank", 0),
        "UnlockRankRange": unlocked.get("JobRankRange", 0),
        "UnlockChallenge": unlocked.get("JobChallengeRequired", ""),
        "LockChallenge": unlocked.get("JobChallengeRequiredToLock", ""),
        "Weight": c.get("RandomFrequencyWeighting", 0),
        "OnlyAtDay": bool(time_of_day.get("OnlyAtDay")),
        "OnlyAtNight": bool(time_of_day.get("OnlyAtNight")),
        "FriendsRequired": c.get("LocalFriendsRequired", 0),
        "CompleteWhenProgressReached": bool(c.get("CompleteWhenProgressReached")),
        "CanBeCancelled": bool(c.get("CanBeCancelled")),
        "LootTable": (c.get("Awards") or {}).get("LootTable", ""),
        "PutAwardsInInventory": bool(c.get("PutAwardsInInventory")),
        "Params": params,
    }


def condition(c):
    test = c["Test"]
    result = {"Name": c["Name"], "Test": test if test in CONDITION_TESTS else "Unsupported", "Invert": bool(c.get("Invert"))}
    if test in ("JobChallengeComplete", "JobChallengeActive"):
        result["JobChallenge"] = c[test]["JobChallenge"]
    elif test in ("ConditionOrList", "ConditionAndList"):
        result["References"] = [{"Condition": r["ConditionReference"]["Condition"], "Invert": bool(r["ConditionReference"]["Invert"])}
                                for r in c[test]["ConditionList"]["ConditionReferences"]]
    return result


def main():
    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    challenges = sorted(geo["JobChallenges"], key=lambda c: c["Index"])
    with open(NAMES_OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(c["Name"] for c in challenges) + "\n")
    with open(DATA_OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump([challenge(c) for c in challenges], f, separators=(",", ":"))
    with open(CONDITIONS_OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump([condition(c) for c in geo["Conditions"]], f, separators=(",", ":"))
    print(f"{len(challenges)} job challenges, {len(geo['Conditions'])} conditions")


if __name__ == "__main__":
    main()
