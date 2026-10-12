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

        if (UsedBlockId(connection) is int blockId && blockId != 0)
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

        var damageId = DamageIdOf(voxel);

        if (damage < DefaultVoxelDurability)
        {
            connection.PlayerConnectionManager.BroadcastToAll(new UpdateVoxelDamage
            {
                DamageId = damageId,
                Amount = (uint)damage,
                EntityId = 0,
                SourceId = connection.PlayerEntity.Id,
                Position = new Vector3(voxel.X, voxel.Y, voxel.Z),
                Timestamp = (ulong)Environment.TickCount64
            });

            Console.WriteLine($"[Voxel] {voxel} damage {damage}/{DefaultVoxelDurability}");
            return true;
        }

        connection.PlayerConnectionManager.BroadcastToAll(new RemoveVoxelDamage { DamageId = damageId });
        VoxelDamage.TryRemove(voxel, out _);
        var removedBlock = GetWorldVoxel(connection, voxel);
        SetWorldVoxel(connection, voxel, Air);
        CollectBlock(connection, removedBlock);

        Console.WriteLine($"[Voxel] removed {voxel}");
        return true;
    }

    private const int StackLimit = 64;

    private static int DamageIdOf(Vector3Int voxel) => (voxel.X << 20) | (voxel.Y << 11) | voxel.Z | 1 << 30;

    private static int? UsedBlockId(PlayerConnection connection)
    {
        if (connection.LastUsedItemId is not int usedId
            || !connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return connection.RightHandBlockId;

        var entityId = inventory.InventoryEntityList.Contains(usedId)
            ? usedId
            : usedId >= 0 && usedId < inventory.InventoryEntityList.Count ? inventory.InventoryEntityList[usedId] : 0;

        if (entityId == 0
            || !connection.WorldManager.EntityManager.TryGetEntity(entityId, out var itemEntity)
            || !itemEntity.TryGetComponent<InventoryItemComponent>(out var item)
            || item.InventorySlotData.Name is not uint crc)
            return connection.RightHandBlockId;

        Console.WriteLine($"[RunActionList] used item {usedId} = {ItemTable.NameOf(ItemTable.IndexOf(crc))}");
        return BlockCrcMapping.GetBlockId(crc);
    }

    private static void CollectBlock(PlayerConnection connection, byte blockId)
    {
        if (blockId == Air || BlockCrcMapping.GetItemCrc(blockId) is not uint itemCrc)
            return;

        if (!connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return;

        foreach (var entityId in inventory.InventoryEntityList)
        {
            if (entityId == 0
                || !connection.WorldManager.EntityManager.TryGetEntity(entityId, out var itemEntity)
                || !itemEntity.TryGetComponent<InventoryItemComponent>(out var item)
                || item.InventorySlotData.Name != itemCrc
                || item.InventorySlotData.Count >= StackLimit)
                continue;

            item.InventorySlotData = item.InventorySlotData.WithCount(item.InventorySlotData.Count + 1);
            Console.WriteLine($"[Voxel] collected {ItemTable.NameOf(ItemTable.IndexOf(itemCrc))}, now {item.InventorySlotData.Count}");
            return;
        }

        Console.WriteLine($"[Voxel] no stack of {ItemTable.NameOf(ItemTable.IndexOf(itemCrc))} to collect into");
    }

    private static byte GetWorldVoxel(PlayerConnection connection, Vector3Int voxel)
    {
        if (voxel.X < 0 || voxel.Y < 0 || voxel.Z < 0)
            return Air;

        var chunk = new Vector3Int(voxel.X / ChunkSize, voxel.Y / ChunkSize, voxel.Z / ChunkSize);
        var local = new Vector3Int(voxel.X % ChunkSize, voxel.Y % ChunkSize, voxel.Z % ChunkSize);

        return connection.WorldManager.ChunkManager.GetVoxel(chunk, local);
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
