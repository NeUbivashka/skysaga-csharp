using System.Linq;

namespace SkySaga.Game;

public static class JobChallengeTable
{
    private static readonly Lazy<List<string>> Names = new(Load);

    private static List<string> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "JobChallenges.txt");

        return File.Exists(path) ? [.. File.ReadLines(path).Select(line => line.Trim()).Where(line => line.Length > 0)] : [];
    }

    public static IReadOnlyList<string> All => Names.Value;

    public static int Count => Names.Value.Count;
}
