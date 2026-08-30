namespace SkySaga.Game.Packets;

public class CraftingNotification : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CraftingNotification);

        return bitStream;
    }
}
