namespace SkySaga.Game.Packets;

public class Batch : ISerializablePacket
{
    private const uint ChannelMax = 7;

    private const uint SequenceShortMax = 0xFFFF;

    public byte[] Payload = [];

    public uint Channel;

    public uint Sequence;

    public bool Flag;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.Batch);

        TransferToServer.WriteBlob(bitStream, Payload);

        bitStream.WriteRanged(Math.Min(Channel, ChannelMax), ChannelMax);

        if (Sequence <= SequenceShortMax)
        {
            bitStream.Write0();
            bitStream.WriteBits(BitConverter.GetBytes((ushort)Sequence), 16, true);
        }
        else
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, Sequence);
        }

        bitStream.Write(Flag);

        return bitStream;
    }
}
