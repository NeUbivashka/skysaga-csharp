namespace SkySaga.Game.Packets;

public class ServerInfo : ISerializablePacket
{
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

    private static void WriteUuid(BitStream bitStream, Guid? value)
    {
        bitStream.WriteClientUuid(value);
    }
}
