namespace SkySaga.Game.Packets;

public class PlayFullScreenVideo : ISerializablePacket
{
    private static uint VideoCount => GeoIndex.Count("Videos");

    public static uint CGIntroVideo1 => GeoIndex.WireIndex("Videos", "CGIntro_Video1");
    public static uint CGIntroVideo2 => GeoIndex.WireIndex("Videos", "CGIntro_Video2");
    public static uint GCIntroStoryTextVideo => GeoIndex.WireIndex("Videos", "GCIntro_StoryTextVideo");

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
