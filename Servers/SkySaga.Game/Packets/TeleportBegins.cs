namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 35, server to client. Reader: FUN_00964EE0 -> FUN_00BB3E50(1, 0x23, ...).
///
/// The whole body is ONE MapSpec:
///
///     FUN_00C0CC30();                    // construct a MapSpec
///     FUN_007E9810(param_2, local_544);  // read it
///     // nothing else is read - what follows is only the logging block, and it prints the
///     // result through FUN_007BA0C0, the MapSpec logger
///
/// No MapSizeChunks, no AdventureType, no MapFileName, no GroupLock, no GameQueueCRC, no
/// GameMode, no trailing word. This packet is not a copy of MapDefinition's body; it just happens
/// to start with the same field.
///
/// The old version wrote six extra fields after the spec. Those were never read - a receiver
/// simply stops consuming - so they cost bandwidth and nothing else. The part that did hurt was
/// the manually appended trailing float in EnteredGameplay: back when it was added, MapSpec.Encode
/// was short of nonSearchable, masteryLevels and version, so the client ran off the end of the
/// spec and into whatever followed, and a zero landed in a duration that FUN_00D45E60 divides by -
/// hence STATUS_FLOAT_MULTIPLE_TRAPS at 0x00D45EDC. The float papered over an under-run.
///
/// MapSpec is complete now, so the under-run is gone and the float has to go with it. Leaving it
/// on is harmless to the parse but keeps a workaround alive for a bug that no longer exists.
/// </summary>
public class TeleportBegins : ISerializablePacket
{
    public MapSpec Spec = new();

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TeleportBegins);

        Spec.Encode(bitStream);

        return bitStream;
    }
}
