using System.Linq;
using System.Text.Json;

namespace SkySaga.Game;

public record ConditionReference(string Condition, bool Invert);

public record ConditionInfo(string Name, string Test, bool Invert, string? JobChallenge, List<ConditionReference>? References);

public static class ConditionTable
{
    private const int MaxDepth = 16;

    private static readonly Lazy<Dictionary<string, ConditionInfo>> Conditions = new(Load);

    private static Dictionary<string, ConditionInfo> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Conditions.json");

        if (!File.Exists(path))
            return [];

        return (JsonSerializer.Deserialize<List<ConditionInfo>>(File.ReadAllText(path)) ?? [])
            .ToDictionary(condition => condition.Name, StringComparer.OrdinalIgnoreCase);
    }

    public static bool Holds(string name, bool invert, Func<string, bool> isComplete, Func<string, bool> isActive)
        => string.IsNullOrEmpty(name) || Evaluate(name, isComplete, isActive, 0) is bool result && result != invert;

    private static bool? Evaluate(string name, Func<string, bool> isComplete, Func<string, bool> isActive, int depth)
    {
        if (depth > MaxDepth || !Conditions.Value.TryGetValue(name, out var condition))
            return null;

        bool? result = condition.Test switch
        {
            "JobChallengeComplete" => condition.JobChallenge is { } challenge && isComplete(challenge),
            "JobChallengeActive" => condition.JobChallenge is { } challenge && isActive(challenge),
            "ConditionOrList" => Combine(condition, isComplete, isActive, depth, any: true),
            "ConditionAndList" => Combine(condition, isComplete, isActive, depth, any: false),
            _ => null
        };

        return result is bool value ? value != condition.Invert : null;
    }

    private static bool? Combine(ConditionInfo condition, Func<string, bool> isComplete, Func<string, bool> isActive, int depth, bool any)
    {
        var values = (condition.References ?? [])
            .Select(reference => Evaluate(reference.Condition, isComplete, isActive, depth + 1) is bool value ? value != reference.Invert : (bool?)null)
            .ToList();

        if (values.Any(value => value is null))
            return any && values.Any(value => value == true) ? true : null;

        return any ? values.Any(value => value == true) : values.All(value => value == true);
    }
}
