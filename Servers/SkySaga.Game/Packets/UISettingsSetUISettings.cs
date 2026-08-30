namespace SkySaga.Game.Packets;

public class UISettingsSetUISettings : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.UISettingsSetUISettings);

        return bitStream;
    }
}
