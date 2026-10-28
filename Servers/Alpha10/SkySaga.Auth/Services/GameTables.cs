namespace SkySaga.Auth.Services;

public sealed class GameTables(GameData gameData)
{
    private const int SignatureMaterialBit = 31;

    private readonly Lazy<Dictionary<uint, string>> _sigils = new(() => Load(gameData.Directory, "Sigils.txt"));
    private readonly Lazy<Dictionary<uint, string>> _materials = new(() => Load(gameData.Directory, "MaterialIndices.txt"));

    public string? SigilName(uint index) => _sigils.Value.GetValueOrDefault(index);

    public string? SignatureMaterial(string? customisationData)
    {
        if (string.IsNullOrEmpty(customisationData))
            return null;

        var data = Convert.FromBase64String(customisationData);

        if (data.Length * 8 < SignatureMaterialBit + 10)
            return null;

        var index = Bits(data, SignatureMaterialBit, 8) | Bits(data, SignatureMaterialBit + 8, 2) << 8;

        return _materials.Value.GetValueOrDefault(index);
    }

    private static uint Bits(byte[] data, int start, int count)
    {
        var value = 0u;

        for (var i = start; i < start + count; i++)
            value = value << 1 | (uint)(data[i / 8] >> (7 - i % 8) & 1);

        return value;
    }

    private static Dictionary<uint, string> Load(string directory, string file)
    {
        var path = Path.Combine(directory, file);

        if (!File.Exists(path))
            return [];

        return File.ReadLines(path)
            .Select(line => line.Split(' ', 2))
            .Where(parts => parts.Length == 2 && uint.TryParse(parts[0], out _))
            .ToDictionary(parts => uint.Parse(parts[0]), parts => parts[1].Trim());
    }
}
