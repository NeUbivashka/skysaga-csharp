namespace SkySaga.Game.Packets;

public class ActionShowDialog : ISerializablePacket
{
    private const uint DialogIndexMax = 0x7D;

    public uint DialogIndex;

    public uint? EntityId;

    public uint? TargetId;

    public bool Flag;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ActionShowDialog);

        bitStream.WriteRanged(Math.Min(DialogIndex, DialogIndexMax), DialogIndexMax);

        InventoryItemRef.WriteOptionalUInt32(bitStream, EntityId);
        InventoryItemRef.WriteOptionalUInt32(bitStream, TargetId);

        bitStream.Write(Flag);

        return bitStream;
    }
}
