namespace SkySaga.Game.Packets;

/// <summary>
/// Alpha 10 layout, read out of the client.
///
/// Reader:    FUN_007E9810  (searchable block: FUN_007E9970, masteryLevels: FUN_007D7AF0)
/// Log/names: FUN_007BA0C0  (top level) and FUN_007BA2E0 (searchable), which print every
///            field with its real name
///
/// The client's MapSpec has four top-level members, in this wire order:
///
///     searchable       17 items, see the table in Encode()
///     nonSearchable    a string and one 16-bit index
///     masteryLevels    a count, then that many biome ids, then that many 32-bit levels
///     version          a string
///
/// The old code wrote the searchable block and then a `Cost` field. `Cost` does not exist in
/// this build - nothing reads it - so it added 27 stray bits, and nonSearchable, masteryLevels
/// and version were missing entirely. Everything the client read after MapSpec inside
/// MapDefinition (AdventureType, MapFileName, GroupLock, GameQueueCRC, GameMode) was therefore
/// shifted.
///
/// Slot names are no longer guesses. FUN_007BA2E0 names them, and slot 4 really is the palette,
/// with 5, 6 and 7 being feature, terrain and cave creature sets in that order - so the
/// PaletteSlot sweep is unnecessary.
///
/// Index widths: the client sizes each index field from the length of the matching list in its
/// own loaded game data, as (bits needed for count - 1), read as a 16-bit value. Our
/// WriteRangedIndex produces the same width, so the table below only has to hold the right
/// counts. See the address list on <see cref="SearchableCounts"/> for how to read the real ones
/// out of a running client.
/// </summary>
public class MapSpec
{
    /// <summary>
    /// Slot order, as the client reads it (FUN_007E9970):
    ///
    ///   slot  field                  count read from
    ///   ----  ---------------------  -------------------------
    ///    0    biome                  [0x01487BCC] + 0x1A04
    ///    1    region                 [0x01487BCC] + 0x1F14
    ///    2    adventure              [0x01487BCC] + 0x1998
    ///    3    difficulty             hardcoded max 3 -> 2 bits
    ///    4    palette                [0x01487BCC] + 0x19E0
    ///    5    featureCreatureSet     [0x01487BCC] + 0x1B00
    ///    6    terrainCreatureSet     [0x01487BCC] + 0x1B00   (same list as 5)
    ///    7    caveCreatureSet        [0x01487BCC] + 0x1B00   (same list as 5)
    ///    8    timeOfDayPreset        [0x01487BCC] + 0x1FF8
    ///    9    timeOfDayPresetList    [0x01487BCC] + 0x1FEC
    ///   10    mapSizeCategory        [0x01487BCC] + 0x1DAC
    ///   11    mapSize                [0x01487BCC] + 0x1DB8
    ///   12    terrainGenerator       [0x01487BCC] + 0x1FE0
    ///   13    activeEvent            [0x01487BCC] + 0x1B90
    ///
    /// These are the values we inferred from GeoData.json. They cannot be confirmed from the
    /// static dump - the global is null there - so read the dwords at the addresses above from a
    /// running client and correct any that differ. Slot 3 is independent confirmation that the
    /// method works: the client hardcodes max 3 for difficulty, which is 2 bits, and a count of
    /// 4 here produces exactly 2 bits.
    /// </summary>
    private static readonly uint[] SearchableCounts = { 6, 25, 145, 4, 46, 46, 46, 46, 17, 6, 6, 32, 11, 4 };

    /// <summary>
    /// The nonSearchable index (client reads it via FUN_007D7970, count at
    /// [0x01487BCC] + 0x1980). That offset is AdventureCosts: the client's count table is the
    /// GeoData lists in ASCII order at 12 bytes per descriptor, and 0x1980 is rank 22, two
    /// descriptors before Adventures at 0x1998. GeoData has 27 AdventureCosts, so the count is
    /// 28 and the field is 5 bits.
    ///
    /// This was guessed at 16 before, which gave 4 bits and left MapSpec one bit short - every
    /// field after it in MapDefinition was shifted. It also explains the old Cost field: that
    /// value is real, it just belongs here rather than at the end of the spec.
    /// </summary>
    private const uint NonSearchableCount = 28;

    /// <summary>Client hardcodes this one rather than reading a list length.</summary>
    private const uint DifficultyMax = 3;

    /// <summary>Above this the count carries an escape bit plus a full 32-bit count.</summary>
    private const uint MasteryLevelCountMax = 12;

    public uint[] Searchable = new uint[14];

    public string AdventureType = string.Empty;

    /// <summary>The unnamed 32-bit field between adventureType and palette.</summary>
    public uint Seed;

    public string FeatureName = string.Empty;

    public string NonSearchableName = string.Empty;

    public uint NonSearchableIndex;

    /// <summary>Biome id paired with a level. Empty is a legal value and costs 4 bits.</summary>
    public List<MasteryLevel> MasteryLevels = [];

    public string Version = string.Empty;

    public readonly struct MasteryLevel(uint biomeId, uint level)
    {
        public uint BiomeId { get; } = biomeId;
        public uint Level { get; } = level;
    }

    /// <summary>
    /// Home_Island_Adventure (wire 83): Forest biome, BiomePalette_Forest_HomeIsland, the
    /// Home_Island time-of-day preset, MapSize HomeIsland_6x4x6. The adventure itself leaves
    /// BiomePalette blank (the live game picked one per island), so the Forest home palette is
    /// filled in here.
    /// </summary>
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

        return spec;
    }

    /// <summary>
    /// True for the FTUE adventure (Adventures[78], wire 79): the scripted Old City with its intro
    /// video and cutscenes.
    /// </summary>
    public bool IsFtue => Searchable[2] == 79;

    /// <summary>The BiomePalette wire index this spec asks for (slot 4).</summary>
    public uint Palette => Searchable[PaletteSlot];

    /// <summary>Wire index of MapSizes HomeIsland_6x4x6 (GeoData Index 3).</summary>
    public const uint HomeIslandMapSizeIndex = 4;

    /// <summary>
    /// DestinationSelection (Adventures[64], wire 65): the airship between the FTUE and the rest of
    /// the game, and the place where the character is created. GeoData chain:
    ///
    ///   AdventureStartedClientActions -> DestinationSelectionIntro (Actions[27], conditional on
    ///   HasQuestFromSource being false) -> Trigger_CharacterCreateIntro -> AnimatedSequences[4]
    ///   Character_Create_Intro -> OnComplete Action_ShowCharacterCustomisation ->
    ///   faGame_ShowCharacterCustomisation (FUN_01207B60), which enters GameState_CharacterCreation
    ///   (game state 0xE) when the player's IsCharacterCustomised bit is 0.
    ///
    ///   slot  value  resolves to
    ///    0      2    Forest
    ///    2     65    DestinationSelection
    ///    4     26    BiomePalette_Forest_Traditonal (Index 25)
    ///    8     10    TimeOfDayPresets[9] = MidDay_Fixed
    ///   10      5    Special
    ///   11      4    HomeIsland_6x4x6
    ///   13      3    NoEvent
    /// </summary>
    public static MapSpec DestinationSelection(uint seed)
    {
        return new MapSpec
        {
            Searchable = [2, 0, 65, 0, 26, 0, 0, 0, 10, 0, 5, HomeIslandMapSizeIndex, 0, 3],
            Seed = seed,
            FeatureName = "CharacterSelect",
        };
    }

    /// <summary>
    /// The adventure to host, from SKYSAGA_ADVENTURE: "ftue" (default) for FTUE_The_Old_City,
    /// "airship" for DestinationSelection, "home" for the home island.
    /// </summary>
    public static MapSpec FromEnvironment(uint seed)
        => Environment.GetEnvironmentVariable("SKYSAGA_ADVENTURE")?.ToLowerInvariant() switch
        {
            "home" => HomeIsland(seed),
            "airship" => DestinationSelection(seed),
            _ => Ftue(seed),
        };

    /// <summary>
    /// Slot 4 carries BiomePalette. Confirmed by the client's own field names, not swept.
    /// </summary>
    private const int PaletteSlot = 4;

    /// <summary>
    /// BiomePalette_FTUE_The_Old_City is Index 28 in GeoData.json; indices on the wire are
    /// 1-based with 0 meaning "unset", which is why the count is 46 for a 45-entry list.
    /// </summary>
    private const uint FtuePaletteIndex = 29;

    /// <summary>BiomePalette_Forest_HomeIsland is Index 22 in GeoData.json, so 23 on the wire.</summary>
    private const uint HomeIslandPaletteIndex = 23;

    /// <summary>Slot 11 carries MapSizes (count 32 = 31 entries + "unset").</summary>
    public const int MapSizeSlot = 11;

    /// <summary>
    /// GeoData.json (36731) MapSizes that are plain XxYxZ chunk sizes, mapped to their wire index
    /// (GeoData Index + 1). The client sizes its world - and its top-down sunlight pass - from this
    /// slot, so it has to describe the chunks the server actually sends.
    /// </summary>
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

    /// <summary>
    /// The FTUE map spec, with every slot resolved against GeoData.json.
    ///
    ///   slot  value  resolves to
    ///   ----  -----  ------------------------------------
    ///    0      2    Biomes[1] = Forest
    ///    1      0    unset
    ///    2     79    Adventures[78] = FTUE_The_Old_City
    ///    3      0    unset
    ///    4     29    BiomePalette_FTUE_The_Old_City
    ///   5-7     0    creature sets - FTUE declares none
    ///    8      0    unset (the adventure leaves it blank)
    ///    9      4    TimeOfDayPresetLists[3] = FTUE_The_Old_City
    ///   10      5    MapSizeCategories[4] = Special
    ///   11      7    MapSizes[6] = MapSize_10x5x10
    ///   12      0    unset
    ///   13      3    Events[2] = NoEvent
    /// </summary>
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

    public void Encode(BitStream bitStream)
    {
        // searchable: biome, region, adventure
        for (var slot = 0; slot < 3; slot++)
            bitStream.WriteRangedIndex(Searchable[slot], SearchableCounts[slot]);

        // difficulty - the one slot the client does not size from a list
        bitStream.WriteRanged(Math.Min(Searchable[3], DifficultyMax), DifficultyMax);

        bitStream.WriteString(AdventureType);
        WriteUInt32(bitStream, Seed);

        // palette, the three creature sets, both time-of-day slots, both map-size slots,
        // terrain generator
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

    /// <summary>
    /// Count first, then every biome id, then every level - two parallel runs, not interleaved
    /// pairs. That is how FUN_007D7AF0 reads it: FUN_007E66B0 fills the id list, then the caller
    /// loops the same number of times pulling 32-bit values.
    /// </summary>
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

    /// <summary>
    /// Full-width 32-bit fields go out big-endian. The client reads them through FUN_006FB780,
    /// which byte-swaps whenever DAT_01449B5E is zero - and it is zero in this build, proven by
    /// the ChunkSync data lengths: those are the one place the project already writes
    /// big-endian, and the client accepts them. Ranged fields use the opposite predicate
    /// (FUN_007187F0) and are not swapped, which is why WriteRanged / WriteRangedIndex stay
    /// little-endian.
    ///
    /// Seed went out little-endian before this, so 1337 arrived as 0x39050000.
    /// </summary>
    private static void WriteUInt32(BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }
}
