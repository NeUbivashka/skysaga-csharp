using System.Linq;

namespace SkySaga.Game.Managers.Entities;

public static class Combat
{
    private static readonly (float Input, float Output)[] PlayerVersusCreatureDamage =
        [(-30, 0), (-20, 20), (-10, 50), (0, 142), (10, 290), (20, 460), (30, 600), (40, 710), (60, 885), (80, 975), (100, 1000)];

    private static readonly int[] ArmourSlots =
    [
        (int)ClientEquippedItemComponent.Slot.Head,
        (int)ClientEquippedItemComponent.Slot.Torso,
        (int)ClientEquippedItemComponent.Slot.Legs,
        (int)ClientEquippedItemComponent.Slot.Arms,
    ];

    public static readonly TimeSpan CorpseLifetime = TimeSpan.FromSeconds(5);

    public static void ApplyMaxHealth(Entity entity)
    {
        if (entity.TryGetComponent<ClientHealthComponent>(out var health) && CombatStatTable.TryGetEntity(entity.Name, out var stats) && stats.Health > 0)
            health.MaxHP = (int)stats.Health;
    }

    public static void PlayerAttack(PlayerConnection connection, uint actionListId, int targetEntityId, uint? usedItemCrc)
    {
        var attacks = ActionListAttackTable.Get(actionListId);

        if (attacks.Count == 0)
            return;

        var entityManager = connection.WorldManager.EntityManager;

        if (!entityManager.TryGetEntity(targetEntityId, out var target)
            || target.TryGetComponent<ClientPlayerAspectsComponent>(out _)
            || !target.TryGetComponent<ClientHealthComponent>(out var health)
            || health.IsDead)
            return;

        var player = connection.PlayerEntity;
        var weaponLevel = AttackLevel(entityManager, player, usedItemCrc);
        var armourLevel = CharacterStats(target).ArmourLevel + EquipmentStats(entityManager, target).ArmourLevel;

        var damage = attacks.Sum(attack => attack.ForceZeroDamage
            ? 0
            : (int)MathF.Round(Lookup(attack.DamageLut ?? PlayerVersusCreatureDamage, weaponLevel - armourLevel)));

        health.Damage(damage);

        connection.PlayerConnectionManager.BroadcastToAll(new HitOccurred
        {
            SourceEntityId = player.Id,
            TargetEntityId = target.Id,
            Amount = (uint)damage
        });

        Console.WriteLine($"[Combat] player {player.Id} hit {target.Name}#{target.Id} with {attacks[0].EquippedAction}/{string.Join("+", attacks.Select(a => a.EntityAction))}: weapon {weaponLevel:0.##} armour {armourLevel:0.##} damage {damage}, hp {health.SyncedFullHPValues}/{health.MaxHP}");

        if (!health.IsDead)
            return;

        if (ResourcePickups.RemoveDevice(connection, target, placedOnly: false))
        {
            Console.WriteLine($"[Combat] {target.Name}#{target.Id} destroyed by player {player.Id}");
            return;
        }

        connection.PlayerConnectionManager.BroadcastToAll(new KillOccurred
        {
            SourceEntityId = player.Id,
            TargetEntityId = target.Id
        });

        Console.WriteLine($"[Combat] {target.Name}#{target.Id} killed by player {player.Id}");

        ResourcePickups.SpawnCreatureLoot(connection, target);
    }

    private static float AttackLevel(IMapEntityManager entityManager, Entity player, uint? usedItemCrc)
    {
        if (usedItemCrc is uint toolCrc && IsDiggingTool(toolCrc) && ToolStatTable.TryGet(toolCrc, out var tool))
            return tool.DigLevel;

        return CharacterStats(player).WeaponLevel + EquipmentStats(entityManager, player).WeaponLevel
            + (usedItemCrc is uint crc && CombatStatTable.TryGetItem(crc, out var weapon) ? weapon.WeaponLevel : 0);
    }

    private static readonly uint DiggingToolTag = Util.ComputeCrc32("DiggingTool");

    private static bool IsDiggingTool(uint itemCrc)
        => ItemTagTable.TryGet(itemCrc, out var tags) && tags.Contains(DiggingToolTag);

    private static CombatStats CharacterStats(Entity entity)
        => CombatStatTable.TryGetEntity(entity.Name, out var stats) ? stats : default;

    private static CombatStats EquipmentStats(IMapEntityManager entityManager, Entity entity)
    {
        if (!entity.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return default;

        float weaponLevel = 0, armourLevel = 0;

        foreach (var slot in ArmourSlots)
        {
            if (slot >= inventory.InventoryEntityList.Count
                || !entityManager.TryGetEntity(inventory.InventoryEntityList[slot], out var item)
                || !item.TryGetComponent<InventoryItemComponent>(out var itemComponent)
                || itemComponent.InventorySlotData.Name is not uint crc
                || !CombatStatTable.TryGetItem(crc, out var stats))
                continue;

            weaponLevel += stats.WeaponLevel;
            armourLevel += stats.ArmourLevel;
        }

        return new CombatStats(0, weaponLevel, armourLevel);
    }

    private static float Lookup((float Input, float Output)[] table, float input)
    {
        if (input <= table[0].Input)
            return table[0].Output;

        for (int i = 1; i < table.Length; i++)
        {
            if (input <= table[i].Input)
            {
                var (x0, y0) = table[i - 1];
                var (x1, y1) = table[i];
                return y0 + (y1 - y0) * (input - x0) / (x1 - x0);
            }
        }

        return table[^1].Output;
    }
}
