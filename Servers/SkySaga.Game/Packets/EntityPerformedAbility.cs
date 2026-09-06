namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 77. Reader: FUN_007F21E0 -> FUN_00BB3E50(1, 0x4D, ...). The client has a sender too
/// (FUN_00A77B10), so this layout serves both directions.
///
///     EntityId    32 bits, full width -> big-endian
///     AbilityId   32 bits, full width -> big-endian
///     (FUN_007DFFD0 reads nothing - it is a constructor priming defaults)
///     Origin      1 presence bit + 32 bits
///     Target      1 presence bit + 32 bits
///     Position    FUN_007D66A0: X 17 bits, Y 15 bits biased by -0x3FC0, Z 17 bits
///     Direction   FUN_007D6590: three components, ranged max 0x80 -> 8 bits each
///     Power       FUN_00CC1740: ranged, max 0x20 -> 6 bits, scaled by 0.03125 to 0.0..1.0
///     Impact      one AbilityTarget (FUN_007E8220)
///     Charge      1 presence bit + 32 bits
///     Flag        1 bit
///     Slot        FUN_007D6D80: ranged, max 8 -> 4 bits
///
/// This was the deepest of the deferred packets - three levels of nesting - and most of that
/// depth turned out to be constructors. FUN_007DFFD0 and FUN_007E0050 read nothing at all; they
/// only prime locals with the same defaults seen elsewhere (0xFFFFC040 for a biased Y, 0xFFFFFFC0
/// for direction components). Once those are set aside only two real readers remain.
///
/// Power uses the same scale AlterActivePhaseByFactor does - DAT_012C1214 is 0.03125 - so set it
/// as a real number from 0 to 1.
/// </summary>
public class EntityPerformedAbility : ISerializablePacket
{
    private const uint PowerMax = 0x20;
    private const float PowerScale = 0.03125f;
    private const uint SlotMax = 8;

    public int EntityId;
    public int AbilityId;
    public uint? Origin;
    public uint? Target;
    public Vector3 Position;

    /// <summary>Each component is clamped to -64..+64 by the client.</summary>
    public Vector3Int Direction;

    /// <summary>0.0 to 1.0, in steps of a thirty-second.</summary>
    public float Power;

    public AbilityTarget Impact = new();

    public uint? Charge;
    public bool Flag;
    public uint Slot;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EntityPerformedAbility);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)AbilityId);

        InventoryItemRef.WriteOptionalUInt32(bitStream, Origin);
        InventoryItemRef.WriteOptionalUInt32(bitStream, Target);

        AbilityTarget.WriteHorizontal(bitStream, Position.X);
        AbilityTarget.WriteVertical(bitStream, Position.Y);
        AbilityTarget.WriteHorizontal(bitStream, Position.Z);

        AbilityTarget.WriteDirection(bitStream, Direction.X);
        AbilityTarget.WriteDirection(bitStream, Direction.Y);
        AbilityTarget.WriteDirection(bitStream, Direction.Z);

        var powerUnits = (uint)Math.Clamp((int)(Power / PowerScale), 0, (int)PowerMax);
        bitStream.WriteRanged(powerUnits, PowerMax);

        Impact.Encode(bitStream);

        InventoryItemRef.WriteOptionalUInt32(bitStream, Charge);

        bitStream.Write(Flag);

        bitStream.WriteRanged(Math.Min(Slot, SlotMax), SlotMax);

        return bitStream;
    }
}
