namespace SkySaga.Game.Components;

public class ClientShopComponent : Component
{
    private const uint ShopListEscape = 2;
    private const uint ShopItemListEscape = 0x800;
    private const uint ItemStackListEscape = 8;

    public record ItemStack(InventoryItemRef Item, uint Quantity);

    public class ShopItem
    {
        public uint? Id;
        public required ItemStack Item;
        public List<ItemStack> Costs = [];
        public List<ItemStack> SecondList = [];
        public List<ItemStack> ThirdList = [];
        public byte Byte;
        public bool FlagA;
        public bool FlagB;
    }

    public class Shop
    {
        public List<ShopItem> Items = [];
        public ulong Timestamp;
    }

    public List<Shop> ShopList { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(ShopList), StringComparison.OrdinalIgnoreCase))
        {
            if (ShopList.Count == 0)
                return false;

            bitStream.WriteEscapedCount((uint)ShopList.Count, ShopListEscape);

            foreach (var shop in ShopList)
            {
                bitStream.WriteEscapedCount((uint)shop.Items.Count, ShopItemListEscape);

                foreach (var item in shop.Items)
                    WriteShopItem(bitStream, item);

                bitStream.WriteUInt64(shop.Timestamp);
            }

            return true;
        }

        return false;
    }

    private static void WriteShopItem(BitStream bitStream, ShopItem item)
    {
        InventoryItemRef.WriteOptionalUInt32(bitStream, item.Id);
        WriteStack(bitStream, item.Item);
        WriteStacks(bitStream, item.Costs);
        WriteStacks(bitStream, item.SecondList);
        WriteStacks(bitStream, item.ThirdList);
        bitStream.WriteBits([item.Byte], 8, true);
        bitStream.Write(item.FlagA);
        bitStream.Write(item.FlagB);
    }

    private static void WriteStacks(BitStream bitStream, List<ItemStack> stacks)
    {
        bitStream.WriteEscapedCount((uint)stacks.Count, ItemStackListEscape);

        foreach (var stack in stacks)
            WriteStack(bitStream, stack);
    }

    private static void WriteStack(BitStream bitStream, ItemStack stack)
    {
        stack.Item.Encode(bitStream);
        InventoryItemRef.WriteUInt32(bitStream, stack.Quantity);
    }
}
