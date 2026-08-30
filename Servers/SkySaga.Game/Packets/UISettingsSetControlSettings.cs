namespace SkySaga.Game.Packets;

public class UISettingsSetControlSettings : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.UISettingsSetControlSettings);

        return bitStream;
    }
}
