using System.Collections.Concurrent;
using System.Numerics;

namespace SkySaga.Game.Packets;

public static class RunActionListReceived
{
    private const int ChunkSize = 32;
    private const float HandDigLevel = 1;
    private const float HandMiningPowerTier = 1;
    private const uint SwingPowerMax = 0x20;
    private const byte Air = WorldChunk.Air;
    private static readonly bool SendVoxelDamage = Environment.GetEnvironmentVariable("SKYSAGA_VOXEL_DAMAGE") == "1";

    private static readonly (float Input, float Output)[] DigDamageLut = [(-20, 5), (-10, 13), (0, 38), (10, 63), (20, 125), (40, 500)];

    private static readonly ConcurrentDictionary<Vector3Int, float> VoxelDamage = new();

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var actionListId)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var entityId)) return false;
        if (!ReadOptionalUInt32(bitStream, out _)) return false;
        if (!ReadOptionalUInt32(bitStream, out _)) return false;
        if (!ReadPosition(bitStream, out _)) return false;
        if (!ReadDirection(bitStream, out _)) return false;
        if (!bitStream.ReadInt32(SwingPowerMax, out var swingPower)) return false;

        if (!bitStream.ReadInt32(1, out var hasVoxel)) return false;
        if (!bitStream.ReadInt32(0x400, out var voxelX)) return false;
        if (!bitStream.ReadInt32(0xE0, out var voxelY)) return false;
        if (!bitStream.ReadInt32(0x400, out var voxelZ)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var targetEntityId)) return false;
        if (!ReadPosition(bitStream, out _)) return false;
        if (!ReadDirection(bitStream, out var hitNormal)) return false;

        var voxel = new Vector3Int(voxelX, voxelY, voxelZ);

        Console.WriteLine($"[RunActionList] list {actionListId} entity {entityId} voxel {(hasVoxel == 1 ? voxel.ToString() : "-")} target {targetEntityId} normal {hitNormal}");

        var usedItemCrc = UsedItemCrc(connection);

        if (targetEntityId != 0)
        {
            Combat.PlayerAttack(connection, actionListId, (int)targetEntityId, usedItemCrc);
            return true;
        }

        if (hasVoxel != 1)
            return true;
        var blockId = usedItemCrc is uint crc ? BlockCrcMapping.GetBlockId(crc) : connection.RightHandBlockId;

        if (blockId is int placedBlock)
        {
            var placeAt = new Vector3Int(
                voxel.X + (int)MathF.Round(hitNormal.X),
                voxel.Y + (int)MathF.Round(hitNormal.Y),
                voxel.Z + (int)MathF.Round(hitNormal.Z));

            SetWorldVoxel(connection, placeAt, (byte)placedBlock);
            VoxelDamage.TryRemove(placeAt, out _);

            Console.WriteLine($"[Voxel] placed {placedBlock} at {placeAt}");
            return true;
        }

        var voxelType = GetWorldVoxel(connection, voxel);

        if (!VoxelTable.TryGet(voxelType, out var voxelStats) || !voxelStats.IsDiggable)
            return true;

        var (digLevel, powerTier) = usedItemCrc is uint toolCrc && ToolStatTable.TryGet(toolCrc, out var tool)
            ? (tool.DigLevel, tool.MiningPowerTier)
            : (HandDigLevel, HandMiningPowerTier);

        var strength = digLevel * swingPower / (float)SwingPowerMax;

        if (powerTier < voxelStats.MiningToughnessTier)
        {
            Console.WriteLine($"[Voxel] {voxel} type {voxelType} needs mining tier {voxelStats.MiningToughnessTier}, tool has {powerTier}");
            return true;
        }

        var hitDamage = DigDamage(strength - voxelStats.Resistance);
        var damage = VoxelDamage.AddOrUpdate(voxel, hitDamage, (_, current) => current + hitDamage);

        var damageId = DamageIdOf(voxel);

        if (damage < voxelStats.Durability)
        {
            if (SendVoxelDamage)
                connection.PlayerConnectionManager.BroadcastToAll(new UpdateVoxelDamage
            {
                DamageId = damageId,
                Amount = (uint)damage,
                EntityId = 0,
                SourceId = connection.PlayerEntity.Id,
                Position = new Vector3(voxel.X, voxel.Y, voxel.Z),
                Timestamp = (ulong)Environment.TickCount64
            });

            Console.WriteLine($"[Voxel] {voxel} type {voxelType} damage {damage}/{voxelStats.Durability} (+{hitDamage})");
            return true;
        }

        if (SendVoxelDamage)
            connection.PlayerConnectionManager.BroadcastToAll(new RemoveVoxelDamage { DamageId = damageId });
        VoxelDamage.TryRemove(voxel, out _);
        SetWorldVoxel(connection, voxel, Air);
        CollectBlock(connection, voxelType);

        Console.WriteLine($"[Voxel] removed {voxel}");
        return true;
    }

    private const int StackLimit = 64;

    private static int DamageIdOf(Vector3Int voxel) => (voxel.X << 20) | (voxel.Y << 11) | voxel.Z | 1 << 30;

    private static float DigDamage(float input)
    {
        if (input <= DigDamageLut[0].Input)
            return DigDamageLut[0].Output;

        for (var i = 1; i < DigDamageLut.Length; i++)
        {
            var (x1, y1) = DigDamageLut[i];

            if (input <= x1)
            {
                var (x0, y0) = DigDamageLut[i - 1];
                return y0 + (y1 - y0) * (input - x0) / (x1 - x0);
            }
        }

        return DigDamageLut[^1].Output;
    }

    private static uint? UsedItemCrc(PlayerConnection connection)
    {
        if (connection.LastUsedItemId is not int usedId
            || !connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return null;

        var entityId = inventory.InventoryEntityList.Contains(usedId)
            ? usedId
            : usedId >= 0 && usedId < inventory.InventoryEntityList.Count ? inventory.InventoryEntityList[usedId] : 0;

        if (entityId == 0
            || !connection.WorldManager.EntityManager.TryGetEntity(entityId, out var itemEntity)
            || !itemEntity.TryGetComponent<InventoryItemComponent>(out var item)
            || item.InventorySlotData.Name is not uint crc)
            return null;

        Console.WriteLine($"[RunActionList] used item {usedId} = {ItemTable.NameOf(ItemTable.IndexOf(crc))}");
        return crc;
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
