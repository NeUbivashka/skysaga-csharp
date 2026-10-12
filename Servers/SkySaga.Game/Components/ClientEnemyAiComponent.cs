namespace SkySaga.Game.Components;

public class ClientEnemyAiComponent : Component
{
    public string AiCapabilities { get; set => SetIfChanged(ref field, value); } = string.Empty;
    public string EnemyType { get; set => SetIfChanged(ref field, value); } = string.Empty;
    public string EnemySubtype { get; set => SetIfChanged(ref field, value); } = string.Empty;
    public int MaxRangedAttackDistance { get; set => SetIfChanged(ref field, value); }
    public string Mood { get; set => SetIfChanged(ref field, value); } = string.Empty;
    public string RelationshipTable { get; set => SetIfChanged(ref field, value); } = string.Empty;
    public Vector3 SpawnPosition { get; set => SetIfChanged(ref field, value); }
    public string StateName { get; set => SetIfChanged(ref field, value); } = string.Empty;
    public int TargetEntityId { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(AiCapabilities), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteString(AiCapabilities);

            return true;
        }
        else if (parameterName.Equals(nameof(EnemyType), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteString(EnemyType);

            return true;
        }
        else if (parameterName.Equals(nameof(EnemySubtype), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteString(EnemySubtype);

            return true;
        }
        else if (parameterName.Equals(nameof(MaxRangedAttackDistance), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(MaxRangedAttackDistance);

            return true;
        }
        else if (parameterName.Equals(nameof(Mood), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteString(Mood);

            return true;
        }
        else if (parameterName.Equals(nameof(RelationshipTable), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteString(RelationshipTable);

            return true;
        }
        else if (parameterName.Equals(nameof(SpawnPosition), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes((int)(SpawnPosition.X * 64f)), 32 - Util.NumBitsRequiredUInt32(0x10000u), true);
            bitStream.WriteBits(BitConverter.GetBytes((int)(SpawnPosition.Y * 64f)), 32 - Util.NumBitsRequiredUInt32(0x10000u), true);
            bitStream.WriteBits(BitConverter.GetBytes((int)(SpawnPosition.Z * 64f)), 32 - Util.NumBitsRequiredUInt32(0x10000u), true);

            return true;
        }
        else if (parameterName.Equals(nameof(StateName), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteString(StateName);

            return true;
        }
        else if (parameterName.Equals(nameof(TargetEntityId), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(TargetEntityId);

            return true;
        }

        return false;
    }
}
