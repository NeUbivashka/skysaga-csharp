using System.Linq;

namespace SkySaga.Game.Components;

/// <summary>
/// The player's unlocked features (GlobalConstantsLockableFeatures in GeoData: Hotbar, HealthBar,
/// Chat, Map, ...). A feature missing from the set is locked, and its UI stays hidden - which is
/// what made every world look like the start of the FTUE.
///
/// Parameter unlockedfeaturecrcset, client bindings vtable 0x0134605C, read function FUN_00C42590
/// -> FUN_00C426B0:
///
///   count     ranged, max 62 (6 bits, FUN_00C42380). 62 is an escape: a presence bit, then the
///             real count as a full-width (big-endian) u32.
///   entries   count times: presence bit + full-width u32 CRC of the feature name (absent = the
///             empty hash). The CRC is the engine's lower-case CRC32, Util.ComputeCrc32.
///
/// The entries must be sorted by CRC, ascending (unsigned). The client keeps them as received and
/// IsFeatureUnlocked (FUN_00C42220 -> FUN_00C42320 -> FUN_00C2BFF0) binary-searches them, so an
/// unsorted set makes most lookups miss: the features were "in" the set but read as locked, and the
/// HUD (hotbar = Hotbar, enum 5) never showed.
///
/// The old server synced "featureislockedstatuslist", a name this build does not have, so the
/// set was never sent.
/// </summary>
public class FeatureUnlockComponent : Component
{
    /// <summary>GlobalConstantsLockableFeatures.LockableFeatureConfigurations (36731), all 62.</summary>
    public static readonly string[] AllFeatures =
    [
        "AddPersonalQuests", "ArmourSlots", "Backpack", "BlueprintStudio", "BonusAdventures", "ChargeBar",
        "Chat", "CoFL", "ContentRotationAdventures", "CurrencyConverter", "DailyAdventures",
        "DailyLoginRewardTrack", "DestinationSelectionBiomes", "DestinationSelectionFTUEForest",
        "DestinationSelectionHotkey", "DestroyItem", "DetailedItemTooltips", "DirectionalIndicator",
        "DiscardItem", "DismantleItem", "EquipItem", "FactionMarkIcon", "Friends", "GameMessages", "GameQueue",
        "GameQueue_EndGame", "GameQueue_GladiatorPVP", "GameQueue_SettlerPVP", "GlobalShop", "Groups", "GuildUI",
        "HandCrafting", "HardCurrencyIcon", "HealthBar", "HomeIslandStorageChests", "Hotbar", "Hotkeys",
        "ImmobiliseWarning", "IslandPublishing", "JumpToLocation", "KnotsIcon", "Mail", "Map",
        "MessageOfTheDay", "MysteryBoxShop", "Photo", "PickupEntities", "Profile", "ProfileUI_Islands",
        "QuestLog", "RepairItem", "ReticuleEntityText", "Sigils", "SocialUI", "ToggleCursor", "UGC_Blueprints",
        "UGC_CommunityIslands", "UGC_CommunityIslands_FTUE", "Vault", "WelcomeHint", "WelcomeIslandTitle",
        "XPBar",
    ];

    /// <summary>
    /// Features the server leaves locked because unlocking them crashes the client with what the
    /// server sends today. Sigils: the playername component then walks its default
    /// AvailablePlayerSigilList and an id falls outside the sigil table (FTL at 0x00948F99 in
    /// FUN_00948D80, index past [0x01487BCC]+0x1EBC). Needs a real sigil list first.
    /// </summary>
    public static readonly string[] KeptLocked = ["Sigils"];

    private const uint CountMax = 62;

    public List<string> UnlockedFeatures { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (!parameterName.Equals("UnlockedFeatureCRCSet", StringComparison.OrdinalIgnoreCase))
            return false;

        var crcs = UnlockedFeatures.Select(feature => Util.ComputeCrc32(feature)).Distinct().Order().ToList();

        bitStream.WriteEscapedCount((uint)crcs.Count, CountMax);

        foreach (var crc in crcs)
        {
            bitStream.Write1();
            bitStream.WriteFullWidthUInt32(crc);
        }

        return true;
    }
}
