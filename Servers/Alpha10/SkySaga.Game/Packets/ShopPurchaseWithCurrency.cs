using System.Linq;

namespace SkySaga.Game.Packets;

public static class ShopPurchaseWithCurrency
{
    private const uint ShopIndexMax = 1;
    private const uint ItemIndexMax = 0x7FF;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var shopOwnerId)) return false;
        if (!bitStream.ReadInt32(ShopIndexMax, out var shopIndex)) return false;
        if (!bitStream.ReadInt32(ItemIndexMax, out var itemIndex)) return false;
        var flag = bitStream.ReadBit();
        if (!bitStream.ReadFullWidthUInt32(out var quantity)) return false;

        Console.WriteLine($"[Shop] purchase owner {shopOwnerId} shop {shopIndex} item {itemIndex} flag {flag} quantity {quantity}");

        if (!connection.WorldManager.EntityManager.TryGetEntity((int)shopOwnerId, out var owner)
            || !owner.TryGetComponent<ClientShopComponent>(out var shops)
            || shopIndex >= shops.ShopList.Count
            || itemIndex >= shops.ShopList[shopIndex].Items.Count
            || !connection.PlayerEntity.TryGetComponent<ClientWalletComponent>(out var wallet)
            || !connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return true;

        var item = shops.ShopList[shopIndex].Items[itemIndex];
        var count = Math.Max(1, quantity);
        var currencies = wallet.WalletCurrency.CurrencyList.Select(entry => new WalletData.CurrencyData { NameHash = entry.NameHash, Value = entry.Value }).ToList();

        foreach (var cost in item.Costs)
        {
            var currencyCrc = Util.ComputeCrc32(ItemTable.NameOf(cost.Item.Kind));
            var balance = currencies.FirstOrDefault(entry => entry.NameHash == currencyCrc);
            var price = (int)(cost.Quantity * count);

            if (balance is null || balance.Value < price)
            {
                Console.WriteLine($"[Shop] not enough {ItemTable.NameOf(cost.Item.Kind)}: need {price}, have {balance?.Value ?? 0}");
                return true;
            }

            balance.Value -= price;
        }

        var itemName = ItemTable.NameOf(item.Item.Item.Kind);

        if (connection.PlayerInitializer.GiveItem(inventory, itemName, (int)(item.Item.Quantity * count)) is not { } given)
        {
            Console.WriteLine("[Shop] inventory full");
            return true;
        }

        wallet.WalletCurrency = new WalletData { CurrencyList = currencies };

        connection.PlayerConnectionManager.BroadcastToAll(new EntityAdd
        {
            Id = given.Id,
            NameHash = Util.ComputeCrc32(given.Name),
            SyncData = given.GetSyncData(newEntity: true)
        });

        Console.WriteLine($"[Shop] bought {item.Item.Quantity * count} x {itemName}");
        return true;
    }
}
