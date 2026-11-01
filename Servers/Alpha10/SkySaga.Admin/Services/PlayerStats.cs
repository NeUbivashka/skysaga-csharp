using System.Globalization;

namespace SkySaga.Admin.Services;

public class PlayerStats
{
    public int MaxHealth { get; }

    public PlayerStats(GameDataDirectory directory)
    {
        var path = Path.Combine(directory.Path, "CombatStats.txt");

        if (!File.Exists(path))
            return;

        var row = File.ReadLines(path)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(parts => parts is ["Entity", "Player", _, ..]);

        if (row is not null && float.TryParse(row[2], CultureInfo.InvariantCulture, out var health))
            MaxHealth = (int)health;
    }
}
