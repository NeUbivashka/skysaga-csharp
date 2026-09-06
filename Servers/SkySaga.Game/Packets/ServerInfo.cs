namespace SkySaga.Game.Packets;

/// <summary>
/// Alpha 10 layout, read straight out of the client.
///
/// Reader:    FUN_007E9C60
/// Log/names: FUN_00805DD0 (prints every field with its real name)
/// Stub:      FUN_007FDFD0  ->  FUN_00BB3E50(1, 0x49, ...)
///
/// Wire order and widths, exactly as the client consumes them:
///
///     ServerOwnerGUID                       1 bit present + 4 x 32 bits
///     WorldUUID                             1 bit present + 4 x 32 bits
///     ServerUUID                            1 bit present + 4 x 32 bits
///     ServerOwnerName                       string
///     ChatHost                              string
///     MaxUsers                              ranged, max 32  -> 6 bits
///     MinUsersRequiredToPlay                ranged, max 32  -> 6 bits
///     ChatPort                              16 bits
///     GameModeEntityID                      32 bits
///     IsOpenedToMatchmakingForMorePlayers    1 bit
///
/// The old alpha 3 shape (ServerBiome, ServerAdventureCrc, MapHeaderSeed, IsHomeWorld,
/// IsMyWorld, and the GUIDs written as strings) does not exist in this build. Since this is
/// the first packet the client receives after connecting, every one of those fields shifted
/// the whole stream and everything read afterwards was garbage.
///
/// The three UUIDs are read as four consecutive 32-bit words. Byte order inside a UUID only
/// affects how the value prints - what matters for stream alignment is the width: 129 bits
/// when present, 1 bit when absent.
/// </summary>
public class ServerInfo : ISerializablePacket
{
    /// <summary>Client clamps both user counts to this, and sizes the field from it.</summary>
    private const uint UserCountMax = 32;

    public Guid? ServerOwnerGuid;
    public Guid? WorldUuid;
    public Guid? ServerUuid;

    public string? ServerOwnerName;
    public string? ChatHost;

    public uint MaxUsers;
    public uint MinUsersRequiredToPlay;

    public ushort ChatPort;

    public int GameModeEntityId;

    public bool IsOpenedToMatchmakingForMorePlayers;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ServerInfo);

        WriteUuid(bitStream, ServerOwnerGuid);
        WriteUuid(bitStream, WorldUuid);
        WriteUuid(bitStream, ServerUuid);

        bitStream.WriteString(ServerOwnerName);
        bitStream.WriteString(ChatHost);

        bitStream.WriteRanged(Math.Min(MaxUsers, UserCountMax), UserCountMax);
        bitStream.WriteRanged(Math.Min(MinUsersRequiredToPlay, UserCountMax), UserCountMax);

        bitStream.Write(ChatPort);

        bitStream.Write(GameModeEntityId);

        bitStream.Write(IsOpenedToMatchmakingForMorePlayers);

        return bitStream;
    }

    /// <summary>
    /// One presence bit, then - only if present - the 16 bytes as four 32-bit words, written
    /// the same way every other 32-bit field in this project is written.
    /// </summary>
    private static void WriteUuid(BitStream bitStream, Guid? value)
    {
        if (value is null)
        {
            bitStream.Write0();

            return;
        }

        bitStream.Write1();

        var bytes = value.Value.ToByteArray();

        for (var offset = 0; offset < 16; offset += 4)
            bitStream.WriteBits(BitConverter.GetBytes(BitConverter.ToUInt32(bytes, offset)), 32, true);
    }
}
