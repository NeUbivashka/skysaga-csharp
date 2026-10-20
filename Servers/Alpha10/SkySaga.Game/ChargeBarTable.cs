using System.Globalization;

namespace SkySaga.Game;

public static class ChargeBarTable
{
    private sealed record Data(float Max, float PlayerBoost, float RecoveryRate, float PauseSeconds, Dictionary<string, float> Actions, Dictionary<uint, float> Items);

    private static readonly Lazy<Data> Table = new(Load);

    private static Data Load()
    {
        var data = new Data(0, 0, 0, 0, new(StringComparer.OrdinalIgnoreCase), []);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ChargeBar.txt");

        if (!File.Exists(path))
            return data;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            float Number(int i) => float.Parse(parts[i], CultureInfo.InvariantCulture);

            switch (parts)
            {
                case ["player", _, _, _]:
                    data = data with { Max = Number(1), PlayerBoost = Number(2), RecoveryRate = Number(3) };
                    break;
                case ["pause", _]:
                    data = data with { PauseSeconds = Number(1) };
                    break;
                case ["action", _, _]:
                    data.Actions[parts[1]] = Number(2);
                    break;
                case ["item", _, _]:
                    data.Items[Util.ComputeCrc32(parts[1])] = Number(2);
                    break;
            }
        }

        return data;
    }

    public static float Max => Table.Value.Max;

    public static float RecoveryRate => Table.Value.RecoveryRate;

    public static float PauseSeconds => Table.Value.PauseSeconds;

    public static float HitGain(string equippedAction, uint? itemCrc)
    {
        if (!Table.Value.Actions.TryGetValue(equippedAction, out var action))
            return 0;

        var equipment = itemCrc is uint crc ? Table.Value.Items.GetValueOrDefault(crc) : 1f;

        return action * Table.Value.PlayerBoost * equipment;
    }
}
