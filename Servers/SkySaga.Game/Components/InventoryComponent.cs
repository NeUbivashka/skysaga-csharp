namespace SkySaga.Game.Components;

public class InventoryComponent : Component
{
    public byte MaxInventorySlots { get; set => SetIfChanged(ref field, value); }
    public uint? InventoryLoadOut { get; set => SetIfChanged(ref field, value); }
    public bool TakeOnly { get; set => SetIfChanged(ref field, value); }

    private const int InventoryEntityListDefaultCount = 45;
    public List<int> InventoryEntityList { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(MaxInventorySlots), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits([MaxInventorySlots], 8 - Util.NumBitsRequiredByte(36), true);

            return true;
        }
        else if (parameterName.Equals(nameof(InventoryLoadOut), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteOptional(InventoryLoadOut, (value) =>
            {
                bitStream.Write((int)value);
            });

            return true;
        }
        else if (parameterName.Equals(nameof(TakeOnly), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(TakeOnly);

            return true;
        }
        else if (parameterName.Equals(nameof(InventoryEntityList), StringComparison.OrdinalIgnoreCase))
        {

            if (InventoryEntityList.Count < InventoryEntityListDefaultCount)
            {
                bitStream.WriteBits(BitConverter.GetBytes(InventoryEntityList.Count), 32 - Util.NumBitsRequiredUInt32(InventoryEntityListDefaultCount), true);
            }
            else
            {
                bitStream.WriteBits(BitConverter.GetBytes(InventoryEntityListDefaultCount), 32 - Util.NumBitsRequiredUInt32(InventoryEntityListDefaultCount), true);

                bitStream.Write1();
                bitStream.Write(InventoryEntityList.Count);
            }

            foreach (var inventoryEntity in InventoryEntityList)
                bitStream.Write(inventoryEntity);

            return true;
        }

        return false;
    }
}