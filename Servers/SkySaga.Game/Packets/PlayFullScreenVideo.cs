namespace SkySaga.Game.Packets;

/// <summary>
/// Alpha 10 layout, read out of the client.
///
/// Stub: FUN_007F9670 -> FUN_00BB3E50(1, 0x6B, ...)
///
///   VideoIndex   FUN_007D8310   ranged 16-bit index, width from the client's Videos list
///   Unknown      inline         32 bits, full width (so big-endian)
///
/// The video is picked by its position in the client's own Videos list, not by name. The old
/// code sent a CRC32 of the name, so the client took the top two bits of that hash as the index
/// and then tried to read 32 more bits that were not in the packet. Whichever video it started
/// was down to those two bits of the hash.
///
/// Indices are 1-based with 0 meaning "none" - the client's logger special-cases 0 and bounds-
/// checks the rest against the list length + 1. GeoData has three videos, so the count is 4 and
/// the field is 2 bits:
///
///     1  CGIntro_Video1          -> transitions to the Eva_Intro animated sequence
///     2  CGIntro_Video2          -> transitions to TeleportToWorld
///     3  GCIntro_StoryTextVideo  -> looping, file SkySaga_IntroStoryBackground_Loop,
///                                   story text INTRO_STORY_TEXT, transitions to CGIntro_Video1
///
/// The trailing 32-bit field is not identified. The client's logger prints it as a plain number
/// and the name pointer does not survive decompilation. It is the last thing in the packet, so a
/// wrong value cannot shift anything - but it is a guess, and zero is only a placeholder.
/// </summary>
public class PlayFullScreenVideo : ISerializablePacket
{
    /// <summary>Three videos in GeoData plus the "none" slot at 0.</summary>
    private const uint VideoCount = 4;

    public const uint CGIntroVideo1 = 1;
    public const uint CGIntroVideo2 = 2;
    public const uint GCIntroStoryTextVideo = 3;

    public uint VideoIndex;

    /// <summary>Purpose unknown - see the note on the class.</summary>
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
