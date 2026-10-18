namespace SkySaga.Game.Packets;

public class HandInCQItems : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.HandInCQItems);

        return bitStream;
    }
}
