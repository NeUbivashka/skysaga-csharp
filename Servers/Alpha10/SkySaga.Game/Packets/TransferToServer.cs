namespace SkySaga.Game.Packets;

public class TransferToServer : ISerializablePacket
{
    public Guid? WorldUuid;

    public Guid? ServerUuid;

    public string? Host;

    public int Port;

    public byte[] Token = [];

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.TransferToServer);

        InventoryItemRef.WriteOptionalUuid(bitStream, WorldUuid);
        InventoryItemRef.WriteOptionalUuid(bitStream, ServerUuid);

        bitStream.WriteString(Host);

        InventoryItemRef.WriteUInt32(bitStream, (uint)Port);

        WriteBlob(bitStream, Token);

        return bitStream;
    }

    internal static void WriteBlob(BitStream bitStream, byte[] data)
    {
        var lengthBytes = BitConverter.GetBytes(data.Length);

        Array.Reverse(lengthBytes);

        bitStream.WriteBits(lengthBytes, 32, true);

        if (data.Length > 0)
            bitStream.WriteBits(data, (uint)data.Length * 8, true);
    }
}
