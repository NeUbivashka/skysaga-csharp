namespace SkySaga.Game.Packets;

public class CombatResult : ISerializablePacket
{
    private const uint DirectionMax = 0x80;
    private const int DirectionOffset = 0x40;
    private const uint VoxelXZMax = 0x400;
    private const uint VoxelYMax = 0xE0;
    private const uint VoxelFaceMax = 7;
    private const uint BlockResultMax = 4;
    private const uint StunStrengthMax = 0x28;
    private const uint ListEscape = 10;
    private const uint DamageMax = 0x7FFF;

    public int AttackerEntityId;
    public int VictimEntityId;
    public Vector3 Position;
    public uint Damage;
    public uint DamageType;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.CombatResult);

        InventoryItemRef.WriteUInt32(bitStream, (uint)AttackerEntityId);
        InventoryItemRef.WriteUInt32(bitStream, 0);
        new InventoryItemRef().Encode(bitStream);

        ClientResourcePickupComponent.WritePosition(bitStream, Position);

        for (var axis = 0; axis < 3; axis++)
            bitStream.WriteRanged(DirectionOffset, DirectionMax);

        bitStream.Write0();

        InventoryItemRef.WriteUInt32(bitStream, (uint)VictimEntityId);
        new InventoryItemRef().Encode(bitStream);

        bitStream.Write((byte)0);
        bitStream.WriteRanged(0, VoxelXZMax);
        bitStream.WriteRanged(0, VoxelYMax);
        bitStream.WriteRanged(0, VoxelXZMax);
        bitStream.WriteRanged(0, VoxelFaceMax);

        bitStream.WriteRanged(0, BlockResultMax);
        bitStream.WriteRanged(0, StunStrengthMax);

        bitStream.Write0();
        bitStream.Write0();
        bitStream.Write0();

        bitStream.WriteEscapedCount(1, ListEscape);
        bitStream.WriteRanged(Math.Min(Damage, DamageMax), DamageMax);
        bitStream.WriteRanged(Math.Min(DamageType, DamageMax), DamageMax);

        bitStream.WriteEscapedCount(0, ListEscape);
        bitStream.WriteEscapedCount(0, ListEscape);

        return bitStream;
    }
}
