using System.Globalization;
using System.Linq;

namespace SkySaga.Game;

public sealed record TreePartInfo(byte Id, int Mesh, string MeshName, Vector3 Rotation);

public sealed record TreeTypeInfo(string Name, uint Crc, Dictionary<byte, TreePartInfo> Parts);

public static class TreeTable
{
    private static readonly Lazy<Dictionary<string, TreeTypeInfo>> Table = new(Load);

    private static Dictionary<string, TreeTypeInfo> Load()
    {
        var types = new Dictionary<string, TreeTypeInfo>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Trees.txt");

        if (!File.Exists(path))
            return types;

        foreach (var line in File.ReadLines(path))
        {
            if (line.Split(' ', StringSplitOptions.RemoveEmptyEntries) is not ["treetype", var name, var crc, .. var parts])
                continue;

            types[name] = new TreeTypeInfo(name, uint.Parse(crc, NumberStyles.HexNumber), parts
                .Select(part => part.Split(':'))
                .Select(field => new TreePartInfo(byte.Parse(field[0]), int.Parse(field[1]), field[2],
                    new Vector3(Number(field[3]), Number(field[4]), Number(field[5]))))
                .ToDictionary(part => part.Id));
        }

        return types;

        static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);
    }

    public static bool TryGet(string name, out TreeTypeInfo type) => Table.Value.TryGetValue(name, out type!);

    public static IEnumerable<string> Names => Table.Value.Keys;
}
