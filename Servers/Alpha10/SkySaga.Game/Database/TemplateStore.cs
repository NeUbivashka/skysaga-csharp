using System.Linq;
using SkySaga.Data;

namespace SkySaga.Game.Database;

public static class TemplateStore
{
    private static readonly Dictionary<string, byte[]> Versions = [];
    private static readonly Lock VersionsLock = new();

    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Data");

    public static string FilePath(string template) => Path.Combine(DataDirectory, template + ".json");

    public static byte[] Read(WorldInfo world)
        => world.TemplateVersion is { } version ? ReadVersion(version) : WorldCatalog.ReadTemplateFile(DataDirectory, world.Template);

    private static byte[] ReadVersion(string id)
    {
        lock (VersionsLock)
            if (Versions.TryGetValue(id, out var cached))
                return cached;

        using var context = GameDatabase.Context();
        var stored = context.MapTemplates.Where(t => t.Id == id).Select(t => t.Content).SingleOrDefault()
            ?? throw new InvalidOperationException($"Template version {id} is not in the database.");
        var content = WorldCatalog.Decompress(stored);

        lock (VersionsLock)
            Versions[id] = content;

        return content;
    }
}
