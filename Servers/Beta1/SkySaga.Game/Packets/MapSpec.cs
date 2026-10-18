using System.Linq;

namespace SkySaga.Game.Packets;

public class MapSpec
{
    private static readonly string?[] SearchableTables =
    [
        "Biomes", "Regions", "Adventures", null, "BiomePalettes", "CreatureSets", "CreatureSets", "CreatureSets",
        "TimeOfDayPresets", "TimeOfDayPresetLists", "MapSizeCategories", "MapSizes", "TerrainGenerators",
    ];

    private const uint DifficultyMax = 3;

    private const uint MasteryLevelCountMax = 12;

    private const int BiomeSlot = 0;
    private const int AdventureSlot = 2;
    private const int PaletteSlot = 4;
    private const int TimeOfDayPresetSlot = 8;
    private const int TimeOfDayPresetListSlot = 9;
    private const int MapSizeCategorySlot = 10;
    public const int MapSizeSlot = 11;

    public uint[] Searchable = new uint[13];

    public string AdventureType = string.Empty;

    public uint Seed;

    public string FeatureName = string.Empty;

    public string ActiveEvents = string.Empty;

    public string NonSearchableName = string.Empty;

    public uint NonSearchableIndex;

    public List<MasteryLevel> MasteryLevels = [];

    public string Version = string.Empty;

    public readonly struct MasteryLevel(uint biomeId, uint level)
    {
        public uint BiomeId { get; } = biomeId;
        public uint Level { get; } = level;
    }

    private static MapSpec Create(uint seed, string adventure, string palette, string? timeOfDayPreset, string? timeOfDayPresetList, string mapSize, string featureName)
    {
        var spec = new MapSpec { Seed = seed, FeatureName = featureName, NonSearchableIndex = GeoIndex.WireIndex("AdventureCosts", "Adv_Cost_Generic") };

        spec.Searchable[BiomeSlot] = GeoIndex.WireIndex("Biomes", "Forest");
        spec.Searchable[AdventureSlot] = GeoIndex.WireIndex("Adventures", adventure);
        spec.Searchable[PaletteSlot] = GeoIndex.WireIndex("BiomePalettes", palette);
        spec.Searchable[MapSizeCategorySlot] = GeoIndex.WireIndex("MapSizeCategories", "Special");
        spec.Searchable[MapSizeSlot] = GeoIndex.WireIndex("MapSizes", mapSize);

        if (timeOfDayPreset is not null)
            spec.Searchable[TimeOfDayPresetSlot] = GeoIndex.WireIndex("TimeOfDayPresets", timeOfDayPreset);

        if (timeOfDayPresetList is not null)
            spec.Searchable[TimeOfDayPresetListSlot] = GeoIndex.WireIndex("TimeOfDayPresetLists", timeOfDayPresetList);

        return spec;
    }

    public static MapSpec HomeIsland(uint seed)
        => Create(seed, "Home_Island_Adventure", "BiomePalette_Forest_Traditonal", "Home_Island", null, "HomeIsland_6x4x6", "Home_Island_World");

    public static MapSpec DestinationSelection(uint seed)
        => Create(seed, "DestinationSelection", "BiomePalette_Forest_Traditonal", "MidDay_Fixed", null, "HomeIsland_6x4x6", "CharacterSelect");

    public static MapSpec Ftue(uint seed)
    {
        var spec = Create(seed, "FTUE_The_Old_City", "Palette_FTUE_The_Old_City", null, "FTUE_The_Old_City", "MapSize_10x5x10", "FeatureGroup_Overground_FTUE_The_Old_City");

        spec.AdventureType = "Adventure";

        return spec;
    }

    public bool IsFtue => Searchable[AdventureSlot] == GeoIndex.WireIndex("Adventures", "FTUE_The_Old_City");

    public uint Palette => Searchable[PaletteSlot];

    public (bool Fixed, float Hours, float CycleMinutes) TimeOfDay
        => TimeOfDayTable.Resolve(
            GeoIndex.NameOf("Adventures", Searchable[AdventureSlot]),
            GeoIndex.NameOf("TimeOfDayPresets", Searchable[TimeOfDayPresetSlot]),
            GeoIndex.NameOf("TimeOfDayPresetLists", Searchable[TimeOfDayPresetListSlot]),
            GeoIndex.NameOf("Biomes", Searchable[BiomeSlot]));

    public void SetBiome(string biome) => Searchable[BiomeSlot] = GeoIndex.WireIndex("Biomes", biome);

    public static uint HomeIslandMapSizeIndex => GeoIndex.WireIndex("MapSizes", "HomeIsland_6x4x6");

    public static MapSpec FromEnvironment(uint seed)
        => Environment.GetEnvironmentVariable("SKYSAGA_ADVENTURE")?.ToLowerInvariant() switch
        {
            "home" => HomeIsland(seed),
            "airship" => DestinationSelection(seed),
            "ftue" => Ftue(seed),
            _ => PlayerSaveStore.Load()?.IsCharacterCustomised == true ? HomeIsland(seed) : DestinationSelection(seed),
        };

    private static readonly Lazy<Dictionary<(int X, int Y, int Z), uint>> MapSizeWireIndex = new(() =>
    {
        var sizes = new Dictionary<(int X, int Y, int Z), uint>();

        foreach (var (name, index) in GeoIndex.Entries("MapSizes").OrderBy(entry => entry.Name.StartsWith("MapSize_") ? 0 : 1))
        {
            var match = System.Text.RegularExpressions.Regex.Match(name, @"(\d+)x(\d+)x(\d+)$");

            if (match.Success && !name.StartsWith("HomeIsland_"))
                sizes.TryAdd((int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value)), index);
        }

        return sizes;
    });

    public static bool TryGetMapSizeWireIndex(int x, int y, int z, out uint index)
        => MapSizeWireIndex.Value.TryGetValue((x, y, z), out index);

    public void Encode(BitStream bitStream)
    {
        for (var slot = 0; slot < 3; slot++)
            WriteSearchable(bitStream, slot);

        bitStream.WriteRanged(Math.Min(Searchable[3], DifficultyMax), DifficultyMax);

        bitStream.WriteString(AdventureType);
        WriteUInt32(bitStream, Seed);

        for (var slot = 4; slot < SearchableTables.Length; slot++)
            WriteSearchable(bitStream, slot);

        bitStream.WriteString(FeatureName);
        bitStream.WriteString(ActiveEvents);

        EncodeNonSearchable(bitStream);
        EncodeMasteryLevels(bitStream);

        bitStream.WriteString(Version);
    }

    private void WriteSearchable(BitStream bitStream, int slot)
        => bitStream.WriteRangedIndex(Searchable[slot], GeoIndex.Count(SearchableTables[slot]!));

    private void EncodeNonSearchable(BitStream bitStream)
    {
        bitStream.WriteString(NonSearchableName);
        bitStream.WriteRangedIndex(NonSearchableIndex, GeoIndex.Count("AdventureCosts"));
    }

    private void EncodeMasteryLevels(BitStream bitStream)
    {
        var count = (uint)MasteryLevels.Count;

        bitStream.WriteRanged(Math.Min(count, MasteryLevelCountMax), MasteryLevelCountMax);

        if (count >= MasteryLevelCountMax)
        {
            bitStream.Write1();
            WriteUInt32(bitStream, count);
        }

        foreach (var entry in MasteryLevels)
            bitStream.WriteRangedIndex(entry.BiomeId, GeoIndex.Count("Biomes"));

        foreach (var entry in MasteryLevels)
            WriteUInt32(bitStream, entry.Level);
    }

    private static void WriteUInt32(BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }
}
