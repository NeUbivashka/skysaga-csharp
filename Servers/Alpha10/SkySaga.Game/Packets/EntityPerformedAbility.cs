namespace SkySaga.Game.Packets;

public class EntityPerformedAbility : ISerializablePacket
{
    private const uint PowerMax = 0x20;
    private const float PowerScale = 0.03125f;
    private const uint SlotMax = 8;

    public int EntityId;
    public int AbilityId;
    public uint? Origin;
    public uint? Target;
    public Vector3 Position;

    public Vector3Int Direction;

    public float Power;

    public AbilityTarget Impact = new();

    public uint? Charge;
    public bool Flag;
    public uint Slot;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EntityPerformedAbility);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)AbilityId);

        InventoryItemRef.WriteOptionalUInt32(bitStream, Origin);
        InventoryItemRef.WriteOptionalUInt32(bitStream, Target);

        AbilityTarget.WriteHorizontal(bitStream, Position.X);
        AbilityTarget.WriteVertical(bitStream, Position.Y);
        AbilityTarget.WriteHorizontal(bitStream, Position.Z);

        AbilityTarget.WriteDirection(bitStream, Direction.X);
        AbilityTarget.WriteDirection(bitStream, Direction.Y);
        AbilityTarget.WriteDirection(bitStream, Direction.Z);

        var powerUnits = (uint)Math.Clamp((int)(Power / PowerScale), 0, (int)PowerMax);
        bitStream.WriteRanged(powerUnits, PowerMax);

        Impact.Encode(bitStream);

        InventoryItemRef.WriteOptionalUInt32(bitStream, Charge);

        bitStream.Write(Flag);

        bitStream.WriteRanged(Math.Min(Slot, SlotMax), SlotMax);

        return bitStream;
    }
}
