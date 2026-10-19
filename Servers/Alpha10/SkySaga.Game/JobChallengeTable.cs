using System.Linq;
using System.Text.Json;

namespace SkySaga.Game;

public record JobChallengeInfo(
    string Name,
    string Job,
    string Action,
    int Number,
    int Xp,
    int GrinderXp,
    bool Dailies,
    bool Master,
    bool AutoActivate,
    bool InQuestLog,
    bool Reactivate,
    string Condition,
    bool ConditionInvert,
    string Next,
    string HandInToNpc,
    JsonElement Params)
{
    public int Index { get; set; }
    public List<string> Events { get; init; } = [];
    public bool EventsInvert { get; init; }
    public string UnlockJob { get; init; } = "";
    public int UnlockRank { get; init; }
    public int UnlockRankRange { get; init; }
    public string UnlockChallenge { get; init; } = "";
    public string LockChallenge { get; init; } = "";
    public int Weight { get; init; }
    public bool OnlyAtDay { get; init; }
    public bool OnlyAtNight { get; init; }
    public int FriendsRequired { get; init; }
    public bool CompleteWhenProgressReached { get; init; }
    public bool CanBeCancelled { get; init; }
    public string LootTable { get; init; } = "";
    public bool PutAwardsInInventory { get; init; }
}

public static class JobChallengeTable
{
    private static readonly Lazy<List<JobChallengeInfo>> Challenges = new(Load);
    private static readonly Lazy<Dictionary<string, JobChallengeInfo>> ByName = new(()
        => Challenges.Value.ToDictionary(challenge => challenge.Name, StringComparer.OrdinalIgnoreCase));
    private static readonly Lazy<Dictionary<uint, JobChallengeInfo>> ByCrc = new(()
        => Challenges.Value.ToDictionary(challenge => Util.ComputeCrc32(challenge.Name)));

    private static List<JobChallengeInfo> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "JobChallenges.json");

        if (!File.Exists(path))
            return [];

        var list = JsonSerializer.Deserialize<List<JobChallengeInfo>>(File.ReadAllText(path)) ?? [];

        for (var i = 0; i < list.Count; i++)
            list[i].Index = i;

        return list;
    }

    public static IReadOnlyList<JobChallengeInfo> All => Challenges.Value;

    public static int Count => Challenges.Value.Count;

    public static bool TryGet(string name, out JobChallengeInfo challenge) => ByName.Value.TryGetValue(name, out challenge!);

    public static bool TryGet(uint crc, out JobChallengeInfo challenge) => ByCrc.Value.TryGetValue(crc, out challenge!);
}
