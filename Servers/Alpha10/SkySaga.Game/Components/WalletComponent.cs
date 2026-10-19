namespace SkySaga.Game.Components;

public class WalletComponent : Component
{
    public WalletData WalletCurrency { get; set => SetIfChanged(ref field, value); } = new();

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(WalletCurrency), StringComparison.OrdinalIgnoreCase))
        {
            WalletCurrency.Serialize(bitStream);

            return true;
        }

        return false;
    }
}