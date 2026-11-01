using System.IO.Compression;
using System.Security.Cryptography;
using SkySaga.Data.Entities;

namespace SkySaga.Data;

public static class WorldCatalog
{
    public const int DefaultSeed = 1337;

    public const string HomeIslandName = "Forest Ruins";

    private static readonly Lock CreateLock = new();

    public static string Template(string dataDirectory, WorldKind kind)
    {
        foreach (var line in File.ReadAllLines(Path.Combine(dataDirectory, "WorldTemplates.txt")))
            if (line.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var name, var template]
                && Enum.TryParse<WorldKind>(name, out var parsed) && parsed == kind)
                return template;

        throw new InvalidOperationException($"WorldTemplates.txt has no template for {kind} worlds.");
    }

    public static byte[] ReadTemplateFile(string dataDirectory, string template)
    {
        var path = Path.Combine(dataDirectory, template + ".json");

        if (File.Exists(path))
            return File.ReadAllBytes(path);

        if (File.Exists(path + ".gz"))
            return Decompress(File.ReadAllBytes(path + ".gz"));

        throw new FileNotFoundException($"The world template {template} is missing.", path);
    }

    public static string Snapshot(SkySagaDbContext db, string dataDirectory, string template)
    {
        var content = ReadTemplateFile(dataDirectory, template);
        var id = Convert.ToHexStringLower(SHA256.HashData(content));

        if (!db.MapTemplates.Any(t => t.Id == id))
        {
            db.MapTemplates.Add(new MapTemplate { Id = id, Name = template, Content = Compress(content), CreatedAt = DateTimeOffset.UtcNow });
            db.SaveChanges();
        }

        return id;
    }

    public static World Hub(SkySagaDbContext db, string dataDirectory)
    {
        lock (CreateLock)
        {
            var hub = db.Worlds.Where(w => w.Kind == WorldKind.Hub).OrderBy(w => w.CreatedAt).FirstOrDefault();

            if (hub is not null)
                return hub;

            hub = new World { Id = Guid.NewGuid(), Kind = WorldKind.Hub, Template = Template(dataDirectory, WorldKind.Hub), Seed = DefaultSeed, CreatedAt = DateTimeOffset.UtcNow };
            db.Worlds.Add(hub);
            db.SaveChanges();
            return hub;
        }
    }

    public static World HomeIsland(SkySagaDbContext db, string dataDirectory, Guid characterId)
    {
        lock (CreateLock)
        {
            var island = db.Worlds.Where(w => w.Kind == WorldKind.Island && w.OwnerCharacterId == characterId)
                .OrderBy(w => w.CreatedAt).FirstOrDefault();

            if (island is not null)
                return island;

            var template = Template(dataDirectory, WorldKind.Island);

            island = new World
            {
                Id = Guid.NewGuid(),
                Kind = WorldKind.Island,
                Template = template,
                TemplateVersion = Snapshot(db, dataDirectory, template),
                Seed = DefaultSeed,
                OwnerCharacterId = characterId,
                Name = HomeIslandName,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Worlds.Add(island);
            if (!InitialKeystoneVeins.AddToNewIsland(db, island, ReadTemplateFile(dataDirectory, template)))
                Console.WriteLine($"[WorldCatalog] No safe space found for keystone veins on new island {island.Id}.");
            db.SaveChanges();
            return island;
        }
    }

    public static byte[] Decompress(byte[] content)
    {
        using var input = new GZipStream(new MemoryStream(content), CompressionMode.Decompress);
        using var output = new MemoryStream();

        input.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Compress(byte[] content)
    {
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
            gzip.Write(content);

        return output.ToArray();
    }
}
