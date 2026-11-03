using System.Linq;

namespace SkySaga.Game;

public readonly record struct DeviceInfo(string Entity, int MinX, int MaxX, int MinY, int MinZ, int MaxZ, Vector3Int[] Cells);

public static class DeviceTable
{
    private static readonly Lazy<Dictionary<uint, DeviceInfo>> DevicesByItem = new(Load);
    private static readonly Lazy<Dictionary<string, DeviceInfo>> DevicesByEntity = new(() =>
        DevicesByItem.Value.Values.GroupBy(device => device.Entity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase));

    private static Dictionary<uint, DeviceInfo> Load()
    {
        var map = new Dictionary<uint, DeviceInfo>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Devices.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 8)
                continue;

            var cells = parts[7].Split(';').Select(cell => cell.Split(',').Select(int.Parse).ToArray())
                .Select(cell => new Vector3Int(cell[0], cell[1], cell[2])).ToArray();

            map[Util.ComputeCrc32(parts[0])] = new DeviceInfo(parts[1],
                int.Parse(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]), int.Parse(parts[5]), int.Parse(parts[6]), cells);
        }

        return map;
    }

    private static readonly Lazy<Dictionary<uint, string[]>> LootByItem = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "DeviceLoot.txt");

        return File.Exists(path)
            ? File.ReadLines(path)
                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => Util.ComputeCrc32(parts[0]), parts => parts[1].Split(','))
            : [];
    });

    public static bool TryGetLoot(uint itemCrc, out string[] tables) => LootByItem.Value.TryGetValue(itemCrc, out tables!);

    public static bool TryGet(uint itemCrc, out DeviceInfo device) => DevicesByItem.Value.TryGetValue(itemCrc, out device);

    public static bool TryGetByEntity(string entity, out DeviceInfo device) => DevicesByEntity.Value.TryGetValue(entity, out device);

    public static Vector3 Origin(DeviceInfo device, Vector3Int voxel, float yawDegrees = 0)
    {
        var (x, z) = ((device.MinX + device.MaxX) / 2f, (device.MinZ + device.MaxZ) / 2f);

        for (var turns = ((int)MathF.Round(yawDegrees / 90f) % 4 + 4) % 4; turns > 0; turns--)
            (x, z) = (z, -x);

        return new(voxel.X + x + 0.5f, voxel.Y - device.MinY, voxel.Z + z + 0.5f);
    }
}
