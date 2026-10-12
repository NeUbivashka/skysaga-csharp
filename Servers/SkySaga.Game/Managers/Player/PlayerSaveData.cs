namespace SkySaga.Game.Managers.Player;

public class PlayerSaveData
{
    public string? Playername { get; set; }

    public bool IsCharacterCustomised { get; set; }

    /// <summary>ClientCharacterCustomisationComponent.CustomisationData, base64 of the raw bits.</summary>
    public string? CustomisationData { get; set; }

    public bool HasPosition { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float YawDegrees { get; set; }

    /// <summary>ClientSettingsComponent (RPCUISettings*). Null for saves from before it existed.</summary>
    public SettingsEntry? Settings { get; set; }

    public int InitialHP { get; set; }
    public int WholeHearts { get; set; }
    public int HalfHearts { get; set; }

    public List<CurrencyEntry> Currencies { get; set; } = [];

    public List<InventoryEntry> Inventory { get; set; } = [];

    public class SettingsEntry
    {
        public bool DisableAutoQuestTracking { get; set; }
        public bool DisableSocialNotifications { get; set; }
        public bool DisableMysteryBoxNotifications { get; set; }
        public uint DisplayTimeMode { get; set; }
        public uint QuestTrackerSort { get; set; }
        public bool ThirdPersonCamera { get; set; } = true;
        public uint CameraState { get; set; }
        public bool ControlFlag { get; set; }
        public uint LastActivatedSlot { get; set; }

        /// <summary>Null for saves from before the hotbar was kept: the initializer fills a default.</summary>
        public List<HotbarEntry>? Hotbar { get; set; }
    }

    public class HotbarEntry
    {
        public uint Slot { get; set; }
        public uint Hand { get; set; }
        public string Item { get; set; } = "";
        public Guid? ItemUuid { get; set; }
    }

    public class CurrencyEntry
    {
        public uint NameHash { get; set; }
        public int Value { get; set; }
    }

    public class InventoryEntry
    {
        public int Slot { get; set; }
        public uint NameHash { get; set; }
        public int Count { get; set; }
        public string? ItemUuid { get; set; }
    }
}
