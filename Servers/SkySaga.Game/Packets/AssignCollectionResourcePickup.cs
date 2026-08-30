namespace SkySaga.Game.Packets;

public class AssignCollectionResourcePickup : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AssignCollectionResourcePickup);

        return bitStream;
    }
}
