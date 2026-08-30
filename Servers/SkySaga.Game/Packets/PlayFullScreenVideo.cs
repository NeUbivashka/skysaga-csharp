namespace SkySaga.Game.Packets;

public class PlayFullScreenVideo : ISerializablePacket
{


    public required string VideoName;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayFullScreenVideo);

        bitStream.WriteString(VideoName);

        return bitStream;
    }
}

