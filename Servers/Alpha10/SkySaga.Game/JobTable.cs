using System.Linq;

namespace SkySaga.Game;

public readonly record struct JobInfo(int Index, string Name, bool Hidden, int[] RankExperience);

public static class JobTable
{
    public const string HudJob = "Unified";

    private static readonly Lazy<List<JobInfo>> Jobs = new(Load);

    private static List<JobInfo> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Jobs.txt");

        if (!File.Exists(path))
            return [];

        return [.. File.ReadLines(path)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2)
            .Select((parts, index) => new JobInfo(index, parts[0], parts[1] == "1", [.. parts.Skip(2).Select(int.Parse)]))];
    }

    public static IReadOnlyList<JobInfo> All => Jobs.Value;

    public static int Count => Jobs.Value.Count;

    public static bool TryGet(string name, out JobInfo job)
    {
        job = Jobs.Value.FirstOrDefault(entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return job.Name is not null;
    }
}
