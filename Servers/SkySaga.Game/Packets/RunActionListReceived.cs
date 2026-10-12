using System.Collections.Concurrent;
using System.Numerics;

namespace SkySaga.Game.Packets;

public static class RunActionListReceived
{
    private const int ChunkSize = 32;
    private const int PickaxeVoxelStrength = 75;
    private const int DefaultVoxelDurability = 160;
    private const byte Air = 0;

    private static readonly ConcurrentDictionary<Vector3Int, int> VoxelDamage = new();

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var actionListId)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var entityId)) return false;
        if (!ReadOptionalUInt32(bitStream, out _)) return false;
        if (!ReadOptionalUInt32(bitStream, out _)) return false;
        if (!ReadPosition(bitStream, out _)) return false;
        if (!ReadDirection(bitStream, out _)) return false;
        if (!bitStream.ReadInt32(0x20, out _)) return false;

        if (!bitStream.ReadInt32(1, out var hasVoxel)) return false;
        if (!bitStream.ReadInt32(0x400, out var voxelX)) return false;
        if (!bitStream.ReadInt32(0xE0, out var voxelY)) return false;
        if (!bitStream.ReadInt32(0x400, out var voxelZ)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var targetEntityId)) return false;
        if (!ReadPosition(bitStream, out _)) return false;
        if (!ReadDirection(bitStream, out var hitNormal)) return false;

        var voxel = new Vector3Int(voxelX, voxelY, voxelZ);

        Console.WriteLine($"[RunActionList] list {actionListId} entity {entityId} voxel {(hasVoxel == 1 ? voxel.ToString() : "-")} target {targetEntityId} normal {hitNormal}");

        if (hasVoxel != 1 || targetEntityId != 0)
            return true;

        if (connection.RightHandBlockId is int blockId && blockId != 0)
        {
            var placeAt = new Vector3Int(
                voxel.X + (int)MathF.Round(hitNormal.X),
                voxel.Y + (int)MathF.Round(hitNormal.Y),
                voxel.Z + (int)MathF.Round(hitNormal.Z));

            SetWorldVoxel(connection, placeAt, (byte)blockId);
            VoxelDamage.TryRemove(placeAt, out _);

            Console.WriteLine($"[Voxel] placed {blockId} at {placeAt}");
            return true;
        }

        var damage = VoxelDamage.AddOrUpdate(voxel, PickaxeVoxelStrength, (_, current) => current + PickaxeVoxelStrength);

        if (damage < DefaultVoxelDurability)
        {
            Console.WriteLine($"[Voxel] {voxel} damage {damage}/{DefaultVoxelDurability}");
            return true;
        }

        VoxelDamage.TryRemove(voxel, out _);
        SetWorldVoxel(connection, voxel, Air);

        Console.WriteLine($"[Voxel] removed {voxel}");
        return true;
    }

    private static void SetWorldVoxel(PlayerConnection connection, Vector3Int voxel, byte blockType)
    {
        if (voxel.X < 0 || voxel.Y < 0 || voxel.Z < 0)
            return;

        var chunk = new Vector3Int(voxel.X / ChunkSize, voxel.Y / ChunkSize, voxel.Z / ChunkSize);
        var local = new Vector3Int(voxel.X % ChunkSize, voxel.Y % ChunkSize, voxel.Z % ChunkSize);

        connection.WorldManager.ChunkManager.SetVoxel(chunk, local, blockType);
    }

    private static bool ReadOptionalUInt32(BitStream bitStream, out uint? value)
    {
        value = null;

        if (!bitStream.ReadInt32(1, out var present)) return false;
        if (present == 0) return true;
        if (!bitStream.ReadFullWidthUInt32(out var raw)) return false;

        value = raw;
        return true;
    }

    private static bool ReadPosition(BitStream bitStream, out Vector3 position)
    {
        position = default;

        if (!bitStream.ReadInt32(0x10000, out var x)) return false;
        if (!bitStream.ReadInt32(0x7F80, out var y)) return false;
        if (!bitStream.ReadInt32(0x10000, out var z)) return false;

        position = new Vector3(x / 64f, (y - 0x3FC0) / 64f, z / 64f);
        return true;
    }

    private static bool ReadDirection(BitStream bitStream, out Vector3 direction)
    {
        direction = default;

        if (!bitStream.ReadInt32(0x80, out var x)) return false;
        if (!bitStream.ReadInt32(0x80, out var y)) return false;
        if (!bitStream.ReadInt32(0x80, out var z)) return false;

        direction = new Vector3((x - 0x40) / 64f, (y - 0x40) / 64f, (z - 0x40) / 64f);
        return true;
    }
}
