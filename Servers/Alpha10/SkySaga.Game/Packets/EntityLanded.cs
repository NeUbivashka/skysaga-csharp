namespace SkySaga.Game.Packets;

public static class EntityLanded
{
    private const uint HeightMax = 0xEA0;
    private const float HeightScale = 1f / 16;
    private const float SafeFallHeight = 5f;
    private const float DamagePerVoxel = 66f;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(HeightMax, out var fallStart)) return false;
        if (!bitStream.ReadInt32(HeightMax, out var landing)) return false;

        var fallHeight = (fallStart - landing) * HeightScale;
        var damage = (int)((fallHeight - SafeFallHeight) * DamagePerVoxel);

        connection.Ai.Hear(connection.PlayerEntity, "Land");

        Console.WriteLine($"[EntityLanded] fell {fallHeight:0.##} voxels, damage {Math.Max(damage, 0)}");

        if (damage > 0 && connection.PlayerEntity.TryGetComponent<ClientHealthComponent>(out var health))
            health.Damage(damage);

        return true;
    }
}
