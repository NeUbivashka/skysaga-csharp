namespace SkySaga.Game.Packets;

public class EntityEvent : ISerializablePacket
{
    public const uint None = 0xD0;

    public static readonly IReadOnlyDictionary<string, uint> KnownEvents = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
    {
        ["Attack"] = 0x03,
        ["Jump"] = 0x04,
        ["Hit"] = 0x05,
        ["Dead"] = 0x07,
        ["Spawn"] = 0x23,
        ["Teleport"] = 0x25,
        ["Block"] = 0x31,
        ["JobChallengeComplete"] = 0x42,
        ["PVPFlagCaptured"] = 0x50,
        ["Dig"] = 0x5E,
        ["PlaceVoxel"] = 0x5F,
        ["FeatureUnlocked"] = 0x61,
        ["JobRankChanged"] = 0x62,
        ["EntityEnteredProximity"] = 0x73,
        ["Landed"] = 0x7B,
        ["ActiveJobChallengesChanged"] = 0x87,
        ["EntityTagsChanged"] = 0x94,
        ["EntityLeftProximity"] = 0xAF,
        ["LogicStateAnyInputsOn"] = 0xBF,
    };

    public uint EventKind;

    public int EntityId;

    public uint? InstigatorId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityEvent);

        bitStream.WriteRanged(Math.Min(EventKind, None), None);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        InventoryItemRef.WriteOptionalUInt32(bitStream, InstigatorId);

        return bitStream;
    }
}
