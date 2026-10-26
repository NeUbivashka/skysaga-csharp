using System.Globalization;
using System.Linq;

namespace SkySaga.Game;

public sealed record Perk(string Name, bool Stackable, string[] Effects);

public sealed record PeriodicPerkEffect(string Action, float IntervalSeconds, float Hp);

public sealed record PerkEntry(string Perk, float DurationSeconds, string? Condition, bool InvertCondition);

public sealed record PerkCondition(string Name, string? Event, int Hits, string[] AnyOf, bool Invert);

public static class PerkTable
{
    private sealed record Data(Dictionary<string, Perk> Perks, Dictionary<string, PeriodicPerkEffect> Periodic,
        Dictionary<string, PerkEntry[]> Actions, Dictionary<string, PerkCondition> Conditions);

    private static readonly Lazy<Data> Table = new(Load);

    private static Data Load()
    {
        var data = new Data(new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase),
            new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Perks.txt");

        if (!File.Exists(path))
            return data;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var fields = parts.Skip(2).Where(part => part.Contains('=')).Select(part => part.Split('=', 2))
                .ToDictionary(field => field[0], field => field[1]);

            switch (parts)
            {
                case ["perk", var name, ..]:
                    data.Perks[name] = new Perk(name, fields["stackable"] == "1", fields["effects"] == "-" ? [] : fields["effects"].Split(','));
                    break;
                case ["perkeffect", var name, "periodic", ..]:
                    data.Periodic[name] = new PeriodicPerkEffect(fields["action"], Number(fields["interval"]), Number(fields["hp"]));
                    break;
                case ["applyperk", var action, ..]:
                    data.Actions[action] = [.. parts[2..].Select(Entry)];
                    break;
                case ["perkcondition", var name, "or", var refs, ..]:
                    data.Conditions[name] = new PerkCondition(name, null, 0, refs.Split(','), fields["invert"] == "1");
                    break;
                case ["perkcondition", var name, ..] when fields.ContainsKey("event"):
                    data.Conditions[name] = new PerkCondition(name, fields["event"], int.Parse(fields["hits"]), [], fields["invert"] == "1");
                    break;
            }
        }

        return data;

        static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);

        static PerkEntry Entry(string field)
        {
            var entry = field.Split(':');
            var condition = entry[2] == "-" ? null : entry[2].TrimStart('!');
            return new PerkEntry(entry[0], Number(entry[1]), condition, entry[2].StartsWith('!'));
        }
    }

    public static bool TryGetPerk(string name, out Perk perk) => Table.Value.Perks.TryGetValue(name, out perk!);

    public static bool TryGetPeriodic(string effect, out PeriodicPerkEffect periodic) => Table.Value.Periodic.TryGetValue(effect, out periodic!);

    public static bool TryGetAction(string entityAction, out PerkEntry[] entries) => Table.Value.Actions.TryGetValue(entityAction, out entries!);

    public static bool TryGetCondition(string name, out PerkCondition condition) => Table.Value.Conditions.TryGetValue(name, out condition!);
}
