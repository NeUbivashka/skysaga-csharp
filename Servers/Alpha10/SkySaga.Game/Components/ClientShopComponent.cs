namespace SkySaga.Game.Components;

public class ClientShopComponent : Component
{
    private const uint ShopListEscape = 2;
    private const uint ShopItemListEscape = 0x800;

    public List<ulong> ShopList { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(ShopList), StringComparison.OrdinalIgnoreCase))
        {
            if (ShopList.Count == 0)
                return false;

            bitStream.WriteEscapedCount((uint)ShopList.Count, ShopListEscape);

            foreach (var shopId in ShopList)
            {
                bitStream.WriteEscapedCount(0, ShopItemListEscape);
                bitStream.WriteUInt64(shopId);
            }

            return true;
        }

        return false;
    }
}
