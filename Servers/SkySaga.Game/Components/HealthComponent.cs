namespace SkySaga.Game.Components;

public class HealthComponent : Component
{
    public int WholeHearts { get; set => SetIfChanged(ref field, value); }
    public int HalfHearts { get; set => SetIfChanged(ref field, value); }
    public int InitialHP { get; set => SetIfChanged(ref field, value); }
    public bool Immortal { get; set => SetIfChanged(ref field, value); }
    public int CorpseStatus { get; set => SetIfChanged(ref field, value); }
    public int LastDamageSourceID { get; set => SetIfChanged(ref field, value); }
    public ItemSpec LastDamageSourceWeaponItemSpec { get; set => SetIfChanged(ref field, value);     } = new();

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(WholeHearts), StringComparison.OrdinalIgnoreCase))
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