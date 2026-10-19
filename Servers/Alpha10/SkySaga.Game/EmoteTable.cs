using System.Linq;

namespace SkySaga.Game;

public static class EmoteTable
{
    private static readonly Lazy<List<(string Name, bool ForNewPlayers)>> Emotes = new(Load);

    private static List<(string Name, bool ForNewPlayers)> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Emotes.txt");

        if (!File.Exists(path))
            return [];

        return [.. File.ReadLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2)
            .Select(parts => (parts[0], parts[1] == "1"))];
    }

    public static uint Max => (uint)Emotes.Value.Count;

    public static ushort IndexOf(string name)
    {
        var index = Emotes.Value.FindIndex(emote => emote.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return (ushort)(index + 1);
    }

    public static string NameOf(ushort index) => index >= 1 && index <= Emotes.Value.Count ? Emotes.Value[index - 1].Name : "";

    public static IEnumerable<string> ForNewPlayers => Emotes.Value.Where(emote => emote.ForNewPlayers).Select(emote => emote.Name);
}
