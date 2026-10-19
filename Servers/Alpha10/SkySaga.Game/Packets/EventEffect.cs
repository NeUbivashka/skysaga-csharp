namespace SkySaga.Game.Packets;

public class EventEffect : ISerializablePacket
{
    private const uint EffectIndexMax = 0xD0;
    private const uint MagnitudeMax = 0x7FFF;
    private const uint DirectionMax = 0x80;
    private const int DirectionOffset = 0x40;

    public uint EffectIndex;

    public int EntityId;

    public int SourceId;

    public ushort Item;

    public ushort SourceItem;

    public Vector3 Position;

    public Vector3 Direction;

    public uint Magnitude;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EventEffect);

        bitStream.WriteRanged(Math.Min(EffectIndex, EffectIndexMax), EffectIndexMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceId);

        bitStream.WriteRangedIndex(Item, InventoryItemRef.ItemTableCount);
        bitStream.WriteRangedIndex(SourceItem, InventoryItemRef.ItemTableCount);

        ClientResourcePickupComponent.WritePosition(bitStream, Position);

        WriteDirection(bitStream, Direction.X);
        WriteDirection(bitStream, Direction.Y);
        WriteDirection(bitStream, Direction.Z);

        bitStream.WriteRanged(Math.Min(Magnitude, MagnitudeMax), MagnitudeMax);

        return bitStream;
    }

    private static void WriteDirection(BitStream bitStream, float component)
        => bitStream.WriteRanged((uint)Math.Clamp((int)MathF.Round(component * 64f) + DirectionOffset, 0, (int)DirectionMax), DirectionMax);
}
