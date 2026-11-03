namespace SkySaga.Game.Components;

public class HealthComponent : Component
{
    private const uint HpMax = 0x7FFF;
    private const uint DamageReasonMax = 9;

    public const uint DamageReasonUnspecified = 0;
    public const uint DamageReasonAttack = 1;
    public const uint DamageReasonFell = 2;
    public const uint DamageReasonDrowned = 3;
    public const uint DamageReasonTemperatureHot = 5;
    public const uint DamageReasonDebug = 6;

    public int MaxHP { get; set; } = 1000;

    public int? SyncedFullHPValues { get; set => SetIfChanged(ref field, value); }

    private const double RegenPauseSeconds = 7.5;
    private const double RegenPerSecond = 12.5;
    private const double RegenSyncSeconds = 1;

    private DateTime _lastDamageUtc;
    private DateTime _lastRegenUtc;
    private double _regenCarry;

    public bool IsDead { get; set => SetIfChanged(ref field, value); }

    public uint LastKilledByDamageReason { get; set => SetIfChanged(ref field, value); }

    public int LastKilledById { get; set => SetIfChanged(ref field, value); }

    public void Damage(int amount, uint reason = DamageReasonUnspecified, int killerId = 0)
    {
        if (IsDead)
            return;

        SyncedFullHPValues = Math.Max((SyncedFullHPValues ?? MaxHP) - amount, 0);
        _lastDamageUtc = DateTime.UtcNow;
        _regenCarry = 0;

        if (SyncedFullHPValues == 0)
        {
            LastKilledByDamageReason = reason;
            LastKilledById = killerId;
            IsDead = true;
            DiedUtc = _lastDamageUtc;
        }
    }

    public DateTime DiedUtc { get; private set; }

    public void Revive()
    {
        SyncedFullHPValues = MaxHP;
        LastKilledByDamageReason = DamageReasonUnspecified;
        LastKilledById = 0;
        IsDead = false;
    }

    public void Heal(int amount)
    {
        if (IsDead)
            return;

        SyncedFullHPValues = Math.Min((SyncedFullHPValues ?? MaxHP) + amount, MaxHP);
    }

    public void Regenerate(DateTime now)
    {
        if (IsDead || SyncedFullHPValues is not { } hp || hp >= MaxHP)
            return;

        if ((now - _lastDamageUtc).TotalSeconds < RegenPauseSeconds || (now - _lastRegenUtc).TotalSeconds < RegenSyncSeconds)
            return;

        var elapsed = Math.Min((now - _lastRegenUtc).TotalSeconds, (now - _lastDamageUtc).TotalSeconds - RegenPauseSeconds);
        _lastRegenUtc = now;
        _regenCarry += elapsed * RegenPerSecond;

        var gain = (int)_regenCarry;
        _regenCarry -= gain;
        SyncedFullHPValues = Math.Min(hp + gain, MaxHP);
    }

    public int WholeHearts { get; set => SetIfChanged(ref field, value); }
    public int HalfHearts { get; set => SetIfChanged(ref field, value); }
    public int InitialHP { get; set => SetIfChanged(ref field, value); }
    public bool Immortal { get; set => SetIfChanged(ref field, value); }
    public int CorpseStatus { get; set => SetIfChanged(ref field, value); }
    public int LastDamageSourceID { get; set => SetIfChanged(ref field, value); }
    public ItemSpec LastDamageSourceWeaponItemSpec { get; set => SetIfChanged(ref field, value);     } = new();

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(SyncedFullHPValues), StringComparison.OrdinalIgnoreCase))
        {
            if (SyncedFullHPValues is not { } hp)
                return false;

            bitStream.WriteRanged((uint)Math.Clamp(hp, 0, (int)HpMax), HpMax);
            bitStream.Write0();

            return true;
        }
        else if (parameterName.Equals(nameof(LastKilledById), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteFullWidthUInt32((uint)LastKilledById);

            return true;
        }
        else if (parameterName.Equals(nameof(LastKilledByDamageReason), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(LastKilledByDamageReason, DamageReasonMax);

            return true;
        }
        else if (parameterName.Equals(nameof(IsDead), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(IsDead);

            return true;
        }
        else if (parameterName.Equals(nameof(WholeHearts), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(WholeHearts), 32 - Util.NumBitsRequiredUInt32(0x200), true);

            return true;
        }
        else if (parameterName.Equals(nameof(HalfHearts), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(HalfHearts), 32 - Util.NumBitsRequiredUInt32(0x200), true);

            return true;
        }
        else if (parameterName.Equals(nameof(InitialHP), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(InitialHP), 32 - Util.NumBitsRequiredUInt32(0x200), true);

            return true;
        }
        else if (parameterName.Equals(nameof(Immortal), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(Immortal);

            return true;
        }
        else if (parameterName.Equals(nameof(CorpseStatus), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(CorpseStatus), 32 - Util.NumBitsRequiredUInt32(4), true);

            return true;
        }
        else if (parameterName.Equals(nameof(LastDamageSourceID), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(LastDamageSourceID);

            return true;
        }
        else if (parameterName.Equals(nameof(LastDamageSourceWeaponItemSpec), StringComparison.OrdinalIgnoreCase))
        {
            LastDamageSourceWeaponItemSpec.Serialize(bitStream);

            return true;
        }

        return false;
    }
}