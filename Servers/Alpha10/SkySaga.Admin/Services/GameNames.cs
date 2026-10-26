using System.Text;
using System.Text.Json;

namespace SkySaga.Admin.Services;

public class GameNames
{
    private readonly Dictionary<uint, string> _items = [];
    private readonly Dictionary<uint, string> _materials = [];

    public GameNames(GameDataDirectory directory)
    {
        var path = Path.Combine(directory.Path, "Resources.txt");

        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadLines(path))
        {
            var name = line.Trim();

            if (name.Length == 0 || name.StartsWith('#'))
                continue;

            _items.TryAdd(Crc32(name), name);
        }

        var geoData = Path.GetFullPath(Path.Combine(directory.Path, "..", "..", "..", "..", "Bundled", "36731", "GeoData.json"));

        if (!File.Exists(geoData))
            return;

        using var stream = File.OpenRead(geoData);
        using var document = JsonDocument.Parse(stream);

        foreach (var material in document.RootElement.GetProperty("Materials").EnumerateArray())
            if (material.GetProperty("Name").GetString() is { } name)
                _materials.TryAdd(Crc32(name), name);
    }

    public string Material(long nameHash) => _materials.TryGetValue((uint)nameHash, out var name) ? name : $"0x{nameHash:X8}";

    public string Item(long nameHash) => _items.TryGetValue((uint)nameHash, out var name) ? name : $"0x{nameHash:X8}";

    public int Count => _items.Count;

    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            var crc = i << 24;

            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;

            table[i] = crc;
        }

        return table;
    }

    public static uint Crc32(string value)
    {
        var crc = 0u;

        foreach (var b in Encoding.UTF8.GetBytes(value.ToLowerInvariant()))
            crc = Table[(byte)(crc >> 24 ^ b)] ^ (crc << 8);

        return crc;
    }
}

public record GameDataDirectory(string Path);
