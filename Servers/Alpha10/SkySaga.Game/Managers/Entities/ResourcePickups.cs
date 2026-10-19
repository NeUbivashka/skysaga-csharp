using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SkySaga.Game.Managers.Entities;

public static class ResourcePickups
{
    private const int GroundSearchDepth = 16;
    private const float RestHeight = 0.25f;
    private const float Scatter = 0.3f;
    private const float PopSpeed = 4f;
    private const float FallSpeed = 3f;
    private const float CreatureDropHeight = 0.75f;

    public static void SpawnFromVoxel(PlayerConnection connection, Vector3Int voxel, byte voxelType)
    {
        if (voxelType == WorldChunk.Air || BlockCrcMapping.GetItemCrc(voxelType) is not uint itemCrc)
            return;

        Spawn(connection, itemCrc, 1, new Vector3(voxel.X + 0.5f, voxel.Y + 0.5f, voxel.Z + 0.5f), voxel, connection.PlayerEntity.Id);
    }

    public static void SpawnDeviceDrops(PlayerConnection connection, WorldManager.PlacedDevice device)
    {
        var voxel = new Vector3Int(device.X, device.Y, device.Z);
        var start = new Vector3(device.X + 0.5f, device.Y + 0.5f, device.Z + 0.5f);
        var owner = connection.PlayerEntity.Id;

        Spawn(connection, device.ItemCrc, 1, start, voxel, owner, device.Materials);

        foreach (var stored in device.Items)
            if (stored is not null)
                Spawn(connection, stored.ItemCrc, stored.Count, start, voxel, owner, stored.Materials);
    }

    public static void SpawnCreatureLoot(PlayerConnection connection, Entity creature)
    {
        if (LootTableName(creature.Name) is not { } tableName
            || !creature.TryGetComponent<SmoothedTransformComponent>(out var transform))
            return;

        var drops = LootTable.Roll(tableName, creature.Name.Split('_')[0]);
        var position = transform.Position;
        var voxel = new Vector3Int((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y) + 1, (int)MathF.Floor(position.Z));

        Console.WriteLine($"[Loot] {creature.Name}#{creature.Id} rolled {tableName}: {(drops.Count == 0 ? "nothing" : string.Join(", ", drops.Select(drop => $"{drop.Count} x {ItemTable.NameOf(ItemTable.IndexOf(drop.ItemCrc))}")))}");

        foreach (var drop in drops)
            Spawn(connection, drop.ItemCrc, drop.Count, position + new Vector3(0, CreatureDropHeight, 0), voxel, owner: 0,
                ResolveMaterials(drop, creature.Name));
    }

    private static readonly Dictionary<string, string> SpeciesMeats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sheep"] = "Lamb_Meat",
        ["Boar"] = "Pig_Meat",
        ["Skitterling"] = "Beetle_Meat",
        ["Scarab"] = "Beetle_Meat",
        ["FennecFox"] = "Fox_Meat",
        ["Penguin"] = "Chicken_Meat",
    };

    private static uint?[] ResolveMaterials(LootDrop drop, string entityName)
    {
        var defaults = MaterialTable.TryGet(drop.ItemCrc, out var materials) ? materials : [];
        var result = new uint?[drop.Materials.Length];

        for (var i = 0; i < drop.Materials.Length; i++)
        {
            result[i] = drop.Materials[i] switch
            {
                null => null,
                LootTable.EntityMaterial => i < defaults.Length && defaults[i] is { } material && LootTable.IsMeat(material) && MeatMaterialOf(entityName) is { } meat
                    ? Util.ComputeCrc32(meat)
                    : null,
                var specific => Util.ComputeCrc32(specific),
            };
        }

        return result;
    }

    private static string? MeatMaterialOf(string entityName)
    {
        var tokens = entityName.Split('_');

        foreach (var token in tokens)
        {
            if (SpeciesMeats.TryGetValue(token, out var meat))
                return meat;
        }

        for (var length = tokens.Length; length > 0; length--)
        {
            for (var start = 0; start + length <= tokens.Length; start++)
            {
                var candidate = string.Join('_', tokens[start..(start + length)]) + "_Meat";

                if (LootTable.IsMeat(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static string? LootTableName(string entityName)
        => entityName.Split('_')
            .Select(token => $"Base_{token}_LootTable")
            .FirstOrDefault(LootTable.Exists);

    private static void Spawn(PlayerConnection connection, uint itemCrc, int count, Vector3 start, Vector3Int groundFrom, int owner, uint?[]? materials = null)
    {
        if (ItemTable.IndexOf(itemCrc) == 0)
            return;

        var entityManager = connection.WorldManager.EntityManager;

        if (connection.PlayerInitializer.CreateInventoryItem(itemCrc, count, materialOverrides: materials) is not { } item)
            return;

        if (!entityManager.TryCreateEntity("Pickup", out var pickup)
            || !pickup.TryGetComponent<ClientResourcePickupComponent>(out var component))
        {
            entityManager.RemoveEntity(item.Id);
            return;
        }

        var target = new Vector3(
            start.X + RandomOffset(),
            GroundBelow(connection, groundFrom) + RestHeight,
            start.Z + RandomOffset());

        component.InventoryItemEntity = item.Id;
        component.OwningEntity = owner;
        component.StartPosition = start;
        component.StartVelocity = new Vector3(RandomOffset(), PopSpeed, RandomOffset());
        component.TargetPosition = target;
        component.TargetVelocity = new Vector3(0, -FallSpeed, 0);

        if (pickup.TryGetComponent<TransformComponent>(out var transform))
            transform.Position = target;

        Broadcast(connection, item);
        Broadcast(connection, pickup);

        Console.WriteLine($"[Pickup] {pickup.Id} {count} x {ItemTable.NameOf(ItemTable.IndexOf(itemCrc))} item {item.Id} at {start} owner {owner}");
    }

    public static void Assign(PlayerConnection connection, int pickupId)
    {
        if (!TryGetPickup(connection, pickupId, out _, out var component))
            return;

        if (component.OwningEntity == 0)
            component.OwningEntity = connection.PlayerEntity.Id;

        Console.WriteLine($"[Pickup] {pickupId} assigned to {component.OwningEntity} (requested by {connection.PlayerEntity.Id})");
    }

    public static void Finalize(PlayerConnection connection, int pickupId)
    {
        if (!TryGetPickup(connection, pickupId, out var pickup, out var component))
            return;

        if (component.OwningEntity != connection.PlayerEntity.Id)
        {
            Console.WriteLine($"[Pickup] {pickupId} finalize by {connection.PlayerEntity.Id} rejected, owner {component.OwningEntity}");
            return;
        }

        var entityManager = connection.WorldManager.EntityManager;

        if (!entityManager.TryGetEntity(component.InventoryItemEntity, out var item)
            || !item.TryGetComponent<InventoryItemComponent>(out var itemComponent)
            || !connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
        {
            Remove(connection, pickup.Id);
            return;
        }

        var slotData = itemComponent.InventorySlotData;

        if (TryFindStack(entityManager, inventory, slotData.Name, MaterialsOf(item), slotData.Count, out var stack))
        {
            stack.InventorySlotData = stack.InventorySlotData.WithCount(stack.InventorySlotData.Count + slotData.Count);
            Remove(connection, item.Id);
            Console.WriteLine($"[Pickup] {pickupId} stacked, now {stack.InventorySlotData.Count}");
        }
        else
        {
            var free = inventory.InventoryEntityList.IndexOf(0, (int)ClientEquippedItemComponent.SlotCount);

            if (free < 0)
            {
                component.OwningEntity = 0;
                Console.WriteLine($"[Pickup] {pickupId} not collected, inventory full");
                return;
            }

            var list = inventory.InventoryEntityList.ToList();
            list[free] = item.Id;
            inventory.InventoryEntityList = list;
            Console.WriteLine($"[Pickup] {pickupId} placed in slot {free}");
        }

        Remove(connection, pickup.Id);
    }

    internal static uint?[] MaterialsOf(Entity item)
        => item.TryGetComponent<MaterialCompositionComponent>(out var composition) ? composition.MaterialList : [];

    internal static bool TryFindStack(IMapEntityManager entityManager, ClientInventoryComponent inventory, uint? name, uint?[] materials, int count, [NotNullWhen(true)] out InventoryItemComponent? stack)
    {
        foreach (var entityId in inventory.InventoryEntityList)
        {
            if (entityId != 0
                && entityManager.TryGetEntity(entityId, out var entity)
                && entity.TryGetComponent<InventoryItemComponent>(out var candidate)
                && candidate.InventorySlotData.Name == name
                && MaterialsOf(entity).SequenceEqual(materials)
                && candidate.InventorySlotData.Count + count <= StackLimitTable.Get(name))
            {
                stack = candidate;
                return true;
            }
        }

        stack = null;
        return false;
    }

    private static bool TryGetPickup(PlayerConnection connection, int pickupId, [NotNullWhen(true)] out Entity? pickup, [NotNullWhen(true)] out ClientResourcePickupComponent? component)
    {
        component = null;

        if (connection.WorldManager.EntityManager.TryGetEntity(pickupId, out pickup)
            && pickup.TryGetComponent(out component))
            return true;

        Console.WriteLine($"[Pickup] unknown pickup {pickupId}");
        return false;
    }

    private static float GroundBelow(PlayerConnection connection, Vector3Int voxel)
    {
        for (var y = voxel.Y - 1; y >= Math.Max(0, voxel.Y - GroundSearchDepth); y--)
        {
            if (RunActionListReceived.GetWorldVoxel(connection, new Vector3Int(voxel.X, y, voxel.Z)) != WorldChunk.Air)
                return y + 1;
        }

        return voxel.Y;
    }

    private static float RandomOffset() => (Random.Shared.NextSingle() * 2 - 1) * Scatter;

    private static void Remove(PlayerConnection connection, int entityId)
    {
        connection.WorldManager.EntityManager.RemoveEntity(entityId);
        connection.PlayerConnectionManager.BroadcastToAll(new EntityRemoved { Id = entityId });
    }

    private static void Broadcast(PlayerConnection connection, Entity entity)
        => connection.PlayerConnectionManager.BroadcastToAll(new EntityAdd
        {
            Id = entity.Id,
            NameHash = Util.ComputeCrc32(entity.Name),
            SyncData = entity.GetSyncData(newEntity: true)
        });
}
