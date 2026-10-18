using System.Linq;

namespace SkySaga.Game.Components;

public class FeatureUnlockComponent : Component
{
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

    public static readonly string[] KeptLocked = [];

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
