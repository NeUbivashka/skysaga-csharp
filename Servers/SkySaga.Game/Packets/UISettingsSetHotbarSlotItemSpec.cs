namespace SkySaga.Game.Packets;

public class UISettingsSetHotbarSlotItemSpec : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.UISettingsSetHotbarSlotItemSpec);

        return bitStream;
    }
}
