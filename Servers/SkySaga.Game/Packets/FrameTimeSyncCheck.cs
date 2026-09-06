namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 9. DEAD IN THIS BUILD - do not send it and do not expect to receive it.
///
/// Every RPC the client knows has a stub ending in FUN_00BB3E50(direction, ordinal, ...). Walking
/// all 263 call sites, ordinal 9 appears in neither direction: the client can neither send nor
/// receive it. The name survives in the ordinal table at 0x01409528 (entry 9 of 341), so the RPC
/// was declared and then compiled out of this build.
///
/// The class is kept so the enum entry has a home and so the next person does not spend an
/// evening implementing it. Anything sent under this id is dropped by the dispatcher without a
/// word, exactly like ResourceCheck and UnlockPosition were.
/// </summary>
public class FrameTimeSyncCheck : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.FrameTimeSyncCheck);

        return bitStream;
    }
}
