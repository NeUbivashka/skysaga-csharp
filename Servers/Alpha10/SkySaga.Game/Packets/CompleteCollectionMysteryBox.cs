namespace SkySaga.Game.Packets;

public class CompleteCollectionMysteryBox : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CompleteCollectionMysteryBox);

        return bitStream;
    }
}
