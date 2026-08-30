namespace SkySaga.Game.Packets;

public class LocomotionMovementTypeChanged : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.LocomotionMovementTypeChanged);

        return bitStream;
    }
}
