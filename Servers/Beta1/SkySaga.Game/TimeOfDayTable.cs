using System.Globalization;

namespace SkySaga.Game;

public static class TimeOfDayTable
{
    private const float DefaultCycleMinutes = 10f;

    private sealed record Adventure(string? Preset, string? List, float? CycleMinutes);

    private sealed class Tables
    {
        public readonly Dictionary<string, (bool Fixed, float Hours)> Presets = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Lists = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, Adventure> Adventures = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, float> Biomes = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly Lazy<Tables> Data = new(Load);

    private static Tables Load()
    {
        var tables = new Tables();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "TimeOfDay.txt");

        if (!File.Exists(path))
        {
            Console.WriteLine($"[TimeOfDay] {path} missing.");
            return tables;
        }

        static string? Name(string value) => value == "-" ? null : value;
        static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ');

            if (line.StartsWith('#'))
                continue;

            switch (parts[0])
            {
                case "preset" when parts.Length == 4:
                    tables.Presets[parts[1]] = (parts[2] == "1", Number(parts[3]));
                    break;
                case "list" when parts.Length == 3:
                    tables.Lists[parts[1]] = parts[2];
                    break;
                case "adventure" when parts.Length == 5:
                    tables.Adventures[parts[1]] = new Adventure(Name(parts[2]), Name(parts[3]), Name(parts[4]) is { } cycle ? Number(cycle) : null);
                    break;
                case "biome" when parts.Length == 3:
                    tables.Biomes[parts[1]] = Number(parts[2]);
                    break;
            }
        }

        return tables;
    }

    public static (bool Fixed, float Hours, float CycleMinutes) Resolve(string? adventure, string? preset, string? list, string? biome)
    {
        var data = Data.Value;
        var fromAdventure = adventure is not null ? data.Adventures.GetValueOrDefault(adventure) : null;

        preset ??= list is not null ? data.Lists.GetValueOrDefault(list) : null;
        preset ??= fromAdventure?.Preset;
        preset ??= fromAdventure?.List is { } adventureList ? data.Lists.GetValueOrDefault(adventureList) : null;

        var (isFixed, hours) = preset is not null && data.Presets.TryGetValue(preset, out var found) ? found : (true, 12f);

        var cycle = fromAdventure?.CycleMinutes
            ?? (biome is not null && data.Biomes.TryGetValue(biome, out var biomeCycle) ? biomeCycle : DefaultCycleMinutes);

        return (isFixed, hours, cycle);
    }
}
