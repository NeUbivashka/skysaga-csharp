using System.Linq;
using SkySaga.Game.Packets;

namespace SkySaga.Game;

public static class AdventureRotation
{
    private sealed record Card(string Adventure, uint[] Searchable, uint Cost, string AdventureType, string FeatureName);

    private static readonly Lazy<List<Card>> Cards = new(Load);

    private static List<Card> Load()
    {
        var cards = new List<Card>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "AdventureRotation.txt");

        if (!File.Exists(path))
            return cards;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 18)
                continue;

            cards.Add(new Card(parts[0], [.. parts[1..15].Select(uint.Parse)], uint.Parse(parts[15]),
                parts[16] == "-" ? string.Empty : parts[16], parts[17]));
        }

        return cards;
    }

    public static SetMapRotationList Current()
    {
        var now = DateTime.UtcNow;
        var random = new Random((int)JobChallenges.RefreshIndex(now));

        return new SetMapRotationList
        {
            Specs = [.. Cards.Value.Select(card => new MapSpec
            {
                Searchable = [.. card.Searchable],
                AdventureType = card.AdventureType,
                Seed = (uint)random.Next(),
                FeatureName = card.FeatureName,
                NonSearchableIndex = card.Cost
            })],
            Unnamed = (uint)(now.Date.AddDays(1) - DateTime.UnixEpoch).TotalSeconds
        };
    }
}
