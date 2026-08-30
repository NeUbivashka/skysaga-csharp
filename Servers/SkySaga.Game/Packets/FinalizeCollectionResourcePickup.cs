namespace SkySaga.Game.Packets;

public class FinalizeCollectionResourcePickup : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.FinalizeCollectionResourcePickup);

        return bitStream;
    }
}
