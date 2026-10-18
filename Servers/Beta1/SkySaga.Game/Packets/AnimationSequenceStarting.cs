namespace SkySaga.Game.Packets;

public class AnimationSequenceStarting : ISerializablePacket
{
    public int EntityId;

    public ushort SequenceIndex;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.AnimationSequenceStarting);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteBits(BitConverter.GetBytes(SequenceIndex), 16, true);

        return bitStream;
    }
}
