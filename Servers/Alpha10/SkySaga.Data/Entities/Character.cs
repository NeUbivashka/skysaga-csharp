namespace SkySaga.Data.Entities;

public class Character
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public required string Name { get; set; }
    public bool IsCustomised { get; set; }
    public string? CustomisationData { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }

    public Guid? CurrentWorldId { get; set; }
    public Guid? OnlineWorldId { get; set; }

    public string? PositionMap { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float YawDegrees { get; set; }
    public float LookPitch { get; set; }

    public int InitialHp { get; set; }
    public int WholeHearts { get; set; }
    public int HalfHearts { get; set; }

    public CharacterSettings? Settings { get; set; }
    public long? ChallengeRefreshIndex { get; set; }
    public List<string>? AvailableEmotes { get; set; }
    public int? CurrentSigil { get; set; }
    public List<string>? KeyboundEmotes { get; set; }
    public List<string>? KnownRecipes { get; set; }
    public List<string>? CompletedChallenges { get; set; }

    public List<CharacterCurrency> Currencies { get; set; } = [];
    public List<InventoryItem> Inventory { get; set; } = [];
    public List<CharacterJob> Jobs { get; set; } = [];
    public List<ActiveChallenge> ActiveChallenges { get; set; } = [];
}

public class CharacterSettings
{
    public bool DisableAutoQuestTracking { get; set; }
    public bool DisableSocialNotifications { get; set; }
    public bool DisableMysteryBoxNotifications { get; set; }
    public long DisplayTimeMode { get; set; }
    public long QuestTrackerSort { get; set; }
    public bool ThirdPersonCamera { get; set; } = true;
    public long CameraState { get; set; }
    public bool ControlFlag { get; set; }
    public long LastActivatedSlot { get; set; }
    public string? MiniMap { get; set; }
    public List<HotbarSlot>? Hotbar { get; set; }
}

public class HotbarSlot
{
    public long Slot { get; set; }
    public long Hand { get; set; }
    public string Item { get; set; } = "";
    public Guid? ItemUuid { get; set; }
}

public class CharacterCurrency
{
    public Guid CharacterId { get; set; }
    public long NameHash { get; set; }
    public int Value { get; set; }
}

public class InventoryItem
{
    public Guid CharacterId { get; set; }
    public int Slot { get; set; }
    public long NameHash { get; set; }
    public int Count { get; set; }
    public string? ItemUuid { get; set; }
    public long?[]? Materials { get; set; }
}

public class CharacterJob
{
    public Guid CharacterId { get; set; }
    public required string Name { get; set; }
    public short Rank { get; set; }
    public long Experience { get; set; }
    public long PrestigeRank { get; set; }
    public List<string>? Available { get; set; }
}

public class ActiveChallenge
{
    public Guid CharacterId { get; set; }
    public required string Name { get; set; }
    public long Progress { get; set; }
    public decimal TodoTimestamp { get; set; }
}
