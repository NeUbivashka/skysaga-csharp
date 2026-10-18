using System.Linq;

namespace SkySaga.Game.Packets.Common;

public class WalletData : ISerializableType
{
    private const uint CurrencyListEscape = 8;
    private const uint SmallValueMax = 0x3FF;

    public List<CurrencyData> CurrencyList = [];

    public class CurrencyData : ISerializableType
    {
        public uint? NameHash;
        public int Value;

        public void Serialize(BitStream bitStream)
        {
            bitStream.WriteRangedIndex(NameHash is uint hash ? ItemTable.IndexOf(hash) : 0u, InventoryItemRef.ItemTableCount);

            if ((uint)Value <= SmallValueMax)
            {
                bitStream.Write0();
                bitStream.WriteRanged((uint)Value, SmallValueMax);
            }
            else
            {
                bitStream.Write1();
                bitStream.WriteBits(BitConverter.GetBytes(Value), 32, true);
            }
        }
    }

    public void Serialize(BitStream bitStream)
    {
        var known = CurrencyList.Where(currency => currency.NameHash is uint hash && ItemTable.IndexOf(hash) != 0).ToList();

        bitStream.WriteEscapedCount((uint)known.Count, CurrencyListEscape);

        foreach (var currency in known)
            currency.Serialize(bitStream);
    }
}
