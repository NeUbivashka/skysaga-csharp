namespace SkySaga.Game.Managers.World;

using System.Linq;
using System.Text.RegularExpressions;

public static class MeandrosSceneLoader
{
    public const string DefaultPath = @"Data\WorldScene.meandros.txt";

    private static readonly Regex NameLineRegex = new(@"^""([^""]+)""$", RegexOptions.Compiled);
    private static readonly Regex SimpleIntRegex = new(@"^-?\d+$", RegexOptions.Compiled);
    private static readonly Regex Float3Regex = new(
        @"^-?\d+(?:\.\d+)?(?:e-?\d+)?\s+-?\d+(?:\.\d+)?(?:e-?\d+)?\s+-?\d+(?:\.\d+)?(?:e-?\d+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Float4Regex = new(
        @"^-?\d+(?:\.\d+)?(?:e-?\d+)?\s+-?\d+(?:\.\d+)?(?:e-?\d+)?\s+-?\d+(?:\.\d+)?(?:e-?\d+)?\s+-?\d+(?:\.\d+)?(?:e-?\d+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private record struct RawPlacement(string Name, double X, double Y, double Z);

    public static bool TryLoad(IMapChunkManager chunkManager, out MapDefinition? definition, out List<WorldMapEntityData> entities, double scale = 1.0, string path = DefaultPath)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);

        definition = null;
        entities = [];

        if (!File.Exists(path))
            return false;

        string text;

        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[MeandrosSceneLoader] Failed to read '{path}': {ex.Message}");
            return false;
        }

        if (!text.Contains("Meandros Editor"))
        {
            Console.WriteLine($"[MeandrosSceneLoader] '{path}' doesn't look like a Meandros scene file (no 'Meandros Editor' marker) - ignoring.");
            return false;
        }

        var placements = ExtractPlacements(text);

        if (placements.Count == 0)
        {
            Console.WriteLine($"[MeandrosSceneLoader] '{path}' parsed but no placement records found - ignoring.");
            return false;
        }

        double minX = placements.Min(p => p.X);
        double minY = placements.Min(p => p.Y);
        double minZ = placements.Min(p => p.Z);
        double maxX = placements.Max(p => p.X);
        double maxZ = placements.Max(p => p.Z);

        entities = placements
            .Select(p => new WorldMapEntityData
            {
                Name = p.Name,
                X = (int)Math.Round((p.X - minX) * scale),
                Y = 1,
                Z = (int)Math.Round((p.Z - minZ) * scale),
            })
            .ToList();

        int spanX = (int)Math.Round((maxX - minX) * scale) + 1;
        int spanZ = (int)Math.Round((maxZ - minZ) * scale) + 1;

        const int chunkSize = 32;
        int sizeXChunks = Math.Clamp((spanX + chunkSize - 1) / chunkSize, 1, 32);
        int sizeZChunks = Math.Clamp((spanZ + chunkSize - 1) / chunkSize, 1, 32);
        const int sizeYChunks = 4;

        int widthVoxels = sizeXChunks * chunkSize;
        int depthVoxels = sizeZChunks * chunkSize;

        int placedVoxels = 0;

        for (int vx = 0; vx < widthVoxels; vx++)
        {
            for (int vz = 0; vz < depthVoxels; vz++)
            {
                var chunkPos = new Vector3Int(vx / chunkSize, 0, vz / chunkSize);
                var chunk = chunkManager.GetOrCreateChunk(chunkPos);
                chunk.SetVoxel(vx % chunkSize, 0, vz % chunkSize, 1, 0);
                placedVoxels++;
            }
        }

        definition = new MapDefinition
        {
            MapSizeChunks = new Vector3Int(sizeXChunks, sizeYChunks, sizeZChunks),

            Spec = MapSpec.Ftue(seed: 1337),
            GameMode = 1,
        };

        Console.WriteLine(
            $"[MeandrosSceneLoader] Loaded '{path}': {placements.Count} placement(s) found, " +
            $"{entities.Count} entities, {placedVoxels} placeholder platform voxels " +
            $"({sizeXChunks}x{sizeYChunks}x{sizeZChunks} chunks). NOTE: platform is a flat " +
            "placeholder, not real terrain - see MeandrosSceneLoader's doc comment.");

        return true;
    }

    private static List<RawPlacement> ExtractPlacements(string text)
    {
        var lines = text.Split("\r\n");
        var placements = new List<RawPlacement>();

        for (int i = 0; i < lines.Length - 7; i++)
        {
            var nameMatch = NameLineRegex.Match(lines[i]);
            if (!nameMatch.Success)
                continue;

            string name = nameMatch.Groups[1].Value;
            if (string.IsNullOrEmpty(name))
                continue;

            string flag1 = lines[i + 1];
            string flag2 = lines[i + 2];
            string flag3 = lines[i + 3];
            string posLine = lines[i + 4];
            string scaleLine = lines[i + 5];
            string rotLine = lines[i + 6];

            if (!SimpleIntRegex.IsMatch(flag1) || !SimpleIntRegex.IsMatch(flag2) || !SimpleIntRegex.IsMatch(flag3))
                continue;
            if (!Float3Regex.IsMatch(posLine))
                continue;
            if (!Float3Regex.IsMatch(scaleLine))
                continue;
            if (!Float4Regex.IsMatch(rotLine))
                continue;

            var posParts = posLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (posParts.Length != 3)
                continue;
            if (!double.TryParse(posParts[0], System.Globalization.CultureInfo.InvariantCulture, out double x))
                continue;
            if (!double.TryParse(posParts[1], System.Globalization.CultureInfo.InvariantCulture, out double y))
                continue;
            if (!double.TryParse(posParts[2], System.Globalization.CultureInfo.InvariantCulture, out double z))
                continue;

            placements.Add(new RawPlacement(name, x, y, z));

            i += 6;
        }

        return placements;
    }
}
