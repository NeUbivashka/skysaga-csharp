namespace SkySaga.Data;

public static class FeatureUnlocks
{
    public static readonly string[] All =
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

    public static IEnumerable<string> Unlocked => All.Except(KeptLocked);
}
