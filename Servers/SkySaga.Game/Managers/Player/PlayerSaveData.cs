namespace SkySaga.Game.Managers.Player;

public class PlayerSaveData
{
    public string? Playername { get; set; }

    public bool HasPosition { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float YawDegrees { get; set; }

    public int InitialHP { get; set; }
    public int WholeHearts { get; set; }
    public int HalfHearts { get; set; }

    public List<CurrencyEntry> Currencies { get; set; } = [];

    public List<InventoryEntry> Inventory { get; set; } = [];

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
    }
}
