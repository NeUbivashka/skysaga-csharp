namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 111, server to client. Reader: FUN_007EA750 -> FUN_00BB3E50(1, 0x6F, ...).
///
///     DialogIndex   ranged, max 0x7D -> 7 bits
///     EntityId      1 presence bit + 32 bits
///     TargetId      1 presence bit + 32 bits
///     Flag          1 bit
///
/// This is the packet that puts dialogue and narrative text on screen, and the server has never
/// sent one - the class was a bare stub until now. The client answers with NarrativeCompleted
/// (105) when the player finishes reading, which we already handle and have seen fire.
///
/// One caveat that the layout cannot settle: DialogIndex is a seven-bit index into a list of
/// dialogues held inside the client, and that list is not in GeoData.json - nothing there has a
/// matching shape. So the field is correct on the wire, but which number means which line of text
/// is unknown, and sending an arbitrary index will show an arbitrary dialogue or none at all.
/// </summary>
public class ActionShowDialog : ISerializablePacket
{
    private const uint DialogIndexMax = 0x7D;

    /// <summary>Index into the client's own dialogue list - see the note on the class.</summary>
    public uint DialogIndex;

    public uint? EntityId;

    public uint? TargetId;

    public bool Flag;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ActionShowDialog);

        bitStream.WriteRanged(Math.Min(DialogIndex, DialogIndexMax), DialogIndexMax);

        InventoryItemRef.WriteOptionalUInt32(bitStream, EntityId);
        InventoryItemRef.WriteOptionalUInt32(bitStream, TargetId);

        bitStream.Write(Flag);

        return bitStream;
    }
}
