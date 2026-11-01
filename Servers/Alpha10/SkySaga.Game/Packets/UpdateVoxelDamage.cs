namespace SkySaga.Game.Packets;

public class UpdateVoxelDamage : ISerializablePacket
{
    private const uint HealthMax = 0x7FFF;
    private const int VoxelIndexBits = 26;
    private const uint HomeIslandMapSize = 0;
    private const ulong NoRegeneration = long.MaxValue / 2;

    private const uint PositionHorizontalMax = 0x10000;
    private const uint PositionVerticalMax = 0x7F80;
    private const int PositionVerticalBias = 0x3FC0;
    private const float PositionScale = 64f;

    private static readonly Dictionary<(int X, int Y, int Z), uint> ClientMapSize = new()
    {
        [(2, 2, 2)] = 2,    [(8, 4, 16)] = 5,   [(10, 4, 14)] = 6,  [(11, 4, 13)] = 7,
        [(12, 4, 12)] = 8,  [(13, 4, 11)] = 9,  [(14, 4, 10)] = 10, [(16, 4, 8)] = 11,
        [(7, 4, 11)] = 12,  [(8, 4, 10)] = 13,  [(9, 4, 9)] = 14,   [(10, 4, 8)] = 15,
        [(11, 4, 7)] = 16,  [(4, 4, 8)] = 17,   [(5, 4, 7)] = 18,   [(6, 4, 6)] = 19,
        [(7, 4, 5)] = 20,   [(8, 4, 4)] = 21,   [(1, 1, 1)] = 22,   [(4, 4, 4)] = 23,
        [(6, 3, 6)] = 24,   [(6, 6, 6)] = 25,   [(9, 7, 9)] = 26,   [(10, 5, 10)] = 27,
        [(17, 2, 17)] = 28, [(4, 4, 5)] = 29,
    };

    public uint VoxelId;
    public uint Health;
    public int EntityId;
    public int SourceId;
    public Vector3 Position;
    public ulong Timestamp;

    public static bool TryPackVoxelId(MapDefinition map, Vector3Int voxel, out uint id)
    {
        var size = map.MapSizeChunks;
        var isHomeIsland = !map.Spec.IsFtue && map.Spec.Searchable[MapSpec.MapSizeSlot] == MapSpec.HomeIslandMapSizeIndex;

        if (!isHomeIsland && !ClientMapSize.ContainsKey((size.X, size.Y, size.Z)))
        {
            id = 0;
            return false;
        }

        var mapSize = isHomeIsland ? HomeIslandMapSize : ClientMapSize[(size.X, size.Y, size.Z)];
        var sizeX = (uint)size.X * 32;
        var sizeZ = (uint)size.Z * 32;
        var index = (sizeZ * (uint)voxel.Y + (uint)voxel.Z) * sizeX + (uint)voxel.X;

        id = index | mapSize << VoxelIndexBits;
        return true;
    }

    public static void SendAll(PlayerConnection connection)
    {
        foreach (var (voxel, damage) in connection.WorldManager.VoxelDamage)
            if (VoxelTable.TryGet(RunActionListReceived.GetWorldVoxel(connection, voxel), out var stats)
                && For(connection.WorldManager.Definition, voxel, stats.Durability - damage, 0) is { } packet)
            {
                connection.Send(packet);
                Console.WriteLine($"[Voxel] sent stored damage at {voxel}: id 0x{packet.VoxelId:X8}, health {packet.Health}/{stats.Durability}");
            }
    }

    public static UpdateVoxelDamage? For(MapDefinition map, Vector3Int voxel, float health, int sourceId)
        => TryPackVoxelId(map, voxel, out var id)
            ? new UpdateVoxelDamage
            {
                VoxelId = id,
                Health = (uint)MathF.Ceiling(health),
                EntityId = 0,
                SourceId = sourceId,
                Position = new Vector3(voxel.X, voxel.Y, voxel.Z),
                Timestamp = NoRegeneration
            }
            : null;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.UpdateVoxelDamage);

        InventoryItemRef.WriteUInt32(bitStream, VoxelId);

        bitStream.WriteRanged(Math.Min(Health, HealthMax), HealthMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceId);

        WriteHorizontal(bitStream, Position.X);
        WriteVertical(bitStream, Position.Y);
        WriteHorizontal(bitStream, Position.Z);

        PvPWaitingInfo.WriteUInt64(bitStream, Timestamp);

        return bitStream;
    }

    private static void WriteHorizontal(BitStream bitStream, float value)
    {
        bitStream.WriteBits(
            BitConverter.GetBytes((int)(value * PositionScale)),
            32 - Util.NumBitsRequiredUInt32(PositionHorizontalMax),
            true);
    }

    private static void WriteVertical(BitStream bitStream, float value)
    {
        var units = Math.Clamp((int)(value * PositionScale) + PositionVerticalBias, 0, (int)PositionVerticalMax);

        bitStream.WriteBits(
            BitConverter.GetBytes(units),
            32 - Util.NumBitsRequiredUInt32(PositionVerticalMax),
            true);
    }
}
