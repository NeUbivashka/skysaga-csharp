namespace SkySaga.Game.Packets;

public class EntityPerformedAbility : ISerializablePacket
{
    private const uint PowerMax = 0x20;
    private const float PowerScale = 0.03125f;
    private const uint SlotMax = 8;

    public int EntityId;
    public int ItemId;
    public uint? PerformerKey;
    public uint? PropertiesCrc;
    public Vector3 Position;

    public Vector3Int Direction;

    public float Power;

    public AbilityTarget Impact = new();

    public uint? ActionCrc;
    public bool Flag;
    public uint Slot;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EntityPerformedAbility);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)ItemId);

        InventoryItemRef.WriteOptionalUInt32(bitStream, PerformerKey);
        InventoryItemRef.WriteOptionalUInt32(bitStream, PropertiesCrc);

        AbilityTarget.WriteHorizontal(bitStream, Position.X);
        AbilityTarget.WriteVertical(bitStream, Position.Y);
        AbilityTarget.WriteHorizontal(bitStream, Position.Z);

        AbilityTarget.WriteDirection(bitStream, Direction.X);
        AbilityTarget.WriteDirection(bitStream, Direction.Y);
        AbilityTarget.WriteDirection(bitStream, Direction.Z);

        var powerUnits = (uint)Math.Clamp((int)(Power / PowerScale), 0, (int)PowerMax);
        bitStream.WriteRanged(powerUnits, PowerMax);

        Impact.Encode(bitStream);

        InventoryItemRef.WriteOptionalUInt32(bitStream, ActionCrc);

        bitStream.Write(Flag);

        bitStream.WriteRanged(Math.Min(Slot, SlotMax), SlotMax);

        return bitStream;
    }
}
