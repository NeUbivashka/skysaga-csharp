namespace SkySaga.Game.Packets;

public class PlayFullScreenVideo : ISerializablePacket
{
    private const uint VideoCount = 4;

    public const uint CGIntroVideo1 = 1;
    public const uint CGIntroVideo2 = 2;
    public const uint GCIntroStoryTextVideo = 3;

    public uint VideoIndex;

    public uint Unknown;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayFullScreenVideo);

        bitStream.WriteRangedIndex(VideoIndex, VideoCount);

        var unknownBytes = BitConverter.GetBytes(Unknown);

        Array.Reverse(unknownBytes);

        bitStream.WriteBits(unknownBytes, 32, true);

        return bitStream;
    }
}
