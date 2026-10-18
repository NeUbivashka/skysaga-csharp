namespace SkySaga.Game.Packets;

public class UISettingsSetMiniMapSettings : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.UISettingsSetMiniMapSettings);

        return bitStream;
    }
}
