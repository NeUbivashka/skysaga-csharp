namespace SkySaga.Game.Packets;

public class MapSpec
{
    private static readonly uint[] SearchableCounts = { 6, 25, 145, 4, 46, 46, 46, 46, 17, 6, 6, 32, 11, 4 };

    private const uint NonSearchableCount = 28;

    private const uint DifficultyMax = 3;

    private const uint MasteryLevelCountMax = 12;

    public uint[] Searchable = new uint[14];

    public string AdventureType = string.Empty;

    public uint Seed;

    public string FeatureName = string.Empty;

    public string NonSearchableName = string.Empty;

    public uint NonSearchableIndex;

    public List<MasteryLevel> MasteryLevels = [];

    public string Version = string.Empty;

    public readonly struct MasteryLevel(uint biomeId, uint level)
    {
        public uint BiomeId { get; } = biomeId;
        public uint Level { get; } = level;
    }

    public static MapSpec HomeIsland(uint seed)
    {
        var spec = new MapSpec
        {
            Searchable =
            [
                2,
                0,
                83,
                0,
                0,
                0,
                0,
                0,
                6,
                0,
                5,
                4,
                0,
                3,
            ],
            Seed = seed,
            FeatureName = "Home_Island_World",
        };

        spec.Searchable[PaletteSlot] = HomeIslandPaletteIndex;
        ApplyEnvironmentOverride(spec, "SKYSAGA_BIOME_INDEX", BiomeSlot);
        ApplyEnvironmentOverride(spec, "SKYSAGA_REGION_INDEX", RegionSlot);
        ApplyEnvironmentOverride(spec, "SKYSAGA_ADVENTURE_INDEX", AdventureSlot);
        ApplyEnvironmentOverride(spec, "SKYSAGA_PALETTE", PaletteSlot);

        return spec;
    }

    private static void ApplyEnvironmentOverride(MapSpec spec, string variable, int slot)
    {
        if (uint.TryParse(Environment.GetEnvironmentVariable(variable), out var value)
            && value < SearchableCounts[slot])
            spec.Searchable[slot] = value;
    }

    public static MapSpec SocialHub(uint seed)
    {
        // The City needs its original adventure to initialize its environment,
        // including its skybox and arrival sequence.
        var spec = HomeIsland(seed);
        spec.Searchable[0] = 5; // NPC friend biome
        spec.Searchable[AdventureSlot] = SocialHubAdventureIndex;
        spec.Searchable[PaletteSlot] = SocialHubPaletteIndex;
        spec.Searchable[TimeOfDayPresetSlot] = SocialHubTimeOfDayPresetIndex;
        spec.Searchable[MapSizeSlot] = SocialHubMapSizeIndex;
        spec.FeatureName = "City_Social_Centre_Area";
        return spec;
    }

    public bool IsFtue => Searchable[2] == 79;

    public uint Palette => Searchable[PaletteSlot];

    public const uint HomeIslandMapSizeIndex = 4;

    public static MapSpec DestinationSelection(uint seed)
    {
        return new MapSpec
        {
            Searchable = [2, 0, 65, 0, 26, 0, 0, 0, 10, 0, 5, HomeIslandMapSizeIndex, 0, 3],
            Seed = seed,
            FeatureName = "CharacterSelect",
        };
    }

    public static MapSpec FromEnvironment(uint seed)
        => Environment.GetEnvironmentVariable("SKYSAGA_WORLD")?.ToLowerInvariant() switch
        {
            "social" => SocialHub(seed),
            _ => Environment.GetEnvironmentVariable("SKYSAGA_ADVENTURE")?.ToLowerInvariant() switch
        {
            "home" => HomeIsland(seed),
            "airship" => DestinationSelection(seed),
            "ftue" => Ftue(seed),
            "mastery-forest" => MasteryAdventure(seed, AdventureBiome.Forest),
            "mastery-desert" => MasteryAdventure(seed, AdventureBiome.Desert),
            "mastery-jungle" => MasteryAdventure(seed, AdventureBiome.Jungle),
            _ => PlayerSaveStore.Load()?.IsCharacterCustomised == true ? HomeIsland(seed) : DestinationSelection(seed),
        }
        };

    private const int BiomeSlot = 0;

    private const int RegionSlot = 1;

    private const int AdventureSlot = 2;

    private const int PaletteSlot = 4;

    private const int TimeOfDayPresetSlot = 8;

    private const uint FtuePaletteIndex = 29;

    private const uint HomeIslandPaletteIndex = 23;

    // GeoData indices are encoded with zero reserved for an unset reference.
    // NPC_Friend_Adventure is 109, BiomePalette_SocialHub is 42,
    // MidMorning is 10, and MapSize_10x5x10 is 6 in GeoData.
    private const uint SocialHubAdventureIndex = 110;

    private const uint SocialHubPaletteIndex = 43;

    private const uint SocialHubTimeOfDayPresetIndex = 11;

    private const uint SocialHubMapSizeIndex = 7;

    public const int MapSizeSlot = 11;

    private static readonly Dictionary<(int X, int Y, int Z), uint> MapSizeWireIndex = new()
    {
        [(10, 5, 10)] = 7,  [(10, 4, 14)] = 5,  [(10, 4, 8)] = 6,   [(11, 4, 13)] = 8,
        [(11, 4, 7)] = 9,   [(12, 4, 12)] = 10, [(13, 4, 11)] = 11, [(14, 4, 10)] = 12,
        [(16, 4, 8)] = 13,  [(17, 2, 17)] = 14, [(1, 1, 1)] = 15,   [(4, 4, 4)] = 16,
        [(4, 4, 8)] = 17,   [(5, 4, 7)] = 18,   [(6, 3, 6)] = 19,   [(6, 4, 6)] = 20,
        [(6, 6, 6)] = 21,   [(7, 4, 11)] = 22,  [(7, 4, 5)] = 23,   [(8, 4, 10)] = 24,
        [(8, 4, 16)] = 25,  [(8, 4, 4)] = 26,   [(9, 4, 9)] = 27,   [(9, 7, 9)] = 28,
        [(4, 4, 5)] = 3,    [(2, 2, 2)] = 30,
    };

    public static bool TryGetMapSizeWireIndex(int x, int y, int z, out uint index)
        => MapSizeWireIndex.TryGetValue((x, y, z), out index);

    public static MapSpec Ftue(uint seed)
    {
        var searchable = new uint[]
        {
            2,
            0,
            79,
            0,
            0,
            0,
            0,
            0,
            0,
            4,
            5,
            7,
            0,
            3,
        };

        searchable[PaletteSlot] = FtuePaletteIndex;

        return new MapSpec
        {
            Searchable = searchable,
            Seed = seed,
            FeatureName = "FeatureGroup_Overground_FTUE_The_Old_City",
            AdventureType = "Adventure",
        };
    }

    /// <summary>
    /// Creates one of the first-region mastery-adventure specifications from
    /// Alpha 10 GeoData.  The client resolves its title, coloured keystone
    /// fragments, and entry requirements from these identifiers.
    /// </summary>
    public static MapSpec MasteryAdventure(uint seed, AdventureBiome biome)
    {
        var (region, adventure, palette, timeOfDay, cost) = biome switch
        {
            // AdventureCosts is a separate, non-searchable MapSpec field.
            // These one-based values are the three Alpha 10
            // Adv_Cost_Mastery_<Biome>_Progression_Easy records, not the
            // generic free-entry records.
            AdventureBiome.Forest => (7u, 31u, 26u, 12u, 22u),
            AdventureBiome.Desert => (1u, 21u, 16u, 10u, 19u),
            AdventureBiome.Jungle => (19u, 41u, 37u, 12u, 25u),
            _ => throw new ArgumentOutOfRangeException(nameof(biome), biome, null),
        };

        // GeoData table indices are one-based on the wire.  These values map
        // to AQ_<Biome>_Mastery_01, Region 1, its normal palette, Easy
        // difficulty, and MapSize_12x4x12.
        return new MapSpec
        {
            Searchable =
            [
                (uint)biome,
                region,
                adventure,
                0,
                palette,
                0,
                0,
                0,
                timeOfDay,
                0,
                5,
                10,
                0,
                3,
            ],
            AdventureType = "Adventure",
            Seed = seed,
            FeatureName = "Adventure_Terrain",
            NonSearchableIndex = cost,
        };
    }

    /// <summary>
    /// Reads only the immutable MapSpec prefix that the client places first in
    /// RequestAdventureStart.  The server needs just this reference to choose
    /// an approved destination; it deliberately does not trust client-supplied
    /// ports, UUIDs, or the rest of the action payload.
    /// </summary>
    public static bool TryReadAdventureWireIndex(BitStream bitStream, out uint adventureWireIndex)
    {
        adventureWireIndex = 0;

        return TryReadRangedIndex(bitStream, SearchableCounts[BiomeSlot], out _)
            && TryReadRangedIndex(bitStream, SearchableCounts[RegionSlot], out _)
            && TryReadRangedIndex(bitStream, SearchableCounts[AdventureSlot], out adventureWireIndex);
    }

    private static bool TryReadRangedIndex(BitStream bitStream, uint tableCount, out uint value)
    {
        value = 0;
        if (tableCount == 0 || !bitStream.ReadInt32(tableCount - 1, out var readValue) || readValue < 0)
            return false;

        value = (uint)readValue;
        return value < tableCount;
    }

    public enum AdventureBiome : uint
    {
        Desert = 1,
        Forest = 2,
        Jungle = 3,
    }

    public void Encode(BitStream bitStream)
    {
        for (var slot = 0; slot < 3; slot++)
            bitStream.WriteRangedIndex(Searchable[slot], SearchableCounts[slot]);

        bitStream.WriteRanged(Math.Min(Searchable[3], DifficultyMax), DifficultyMax);

        bitStream.WriteString(AdventureType);
        WriteUInt32(bitStream, Seed);

        for (var slot = 4; slot < 13; slot++)
            bitStream.WriteRangedIndex(Searchable[slot], SearchableCounts[slot]);

        bitStream.WriteString(FeatureName);
        bitStream.WriteRangedIndex(Searchable[13], SearchableCounts[13]);

        EncodeNonSearchable(bitStream);
        EncodeMasteryLevels(bitStream);

        bitStream.WriteString(Version);
    }

    private void EncodeNonSearchable(BitStream bitStream)
    {
        bitStream.WriteString(NonSearchableName);
        bitStream.WriteRangedIndex(NonSearchableIndex, NonSearchableCount);
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
            bitStream.WriteRangedIndex(entry.BiomeId, SearchableCounts[0]);

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
