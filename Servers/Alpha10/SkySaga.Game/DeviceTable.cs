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

    public static bool TryGet(uint itemCrc, out DeviceInfo device) => DevicesByItem.Value.TryGetValue(itemCrc, out device);

    public static bool TryGetByEntity(string entity, out DeviceInfo device) => DevicesByEntity.Value.TryGetValue(entity, out device);

    public static Vector3 Origin(DeviceInfo device, Vector3Int voxel)
        => new(voxel.X + (device.MinX + device.MaxX + 1) / 2f, voxel.Y - device.MinY, voxel.Z + (device.MinZ + device.MaxZ + 1) / 2f);
}
