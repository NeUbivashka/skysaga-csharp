using System.Linq;

namespace SkySaga.Game.Packets;

public static class SetPlayerSigil
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(ClientPlayernameComponent.SigilTableCount - 1, out var sigil))
            return false;

        if (!connection.IsPlayerInitialized
            || !connection.PlayerEntity.TryGetComponent<ClientPlayernameComponent>(out var playername)
            || playername.AvailablePlayerSigilList.All(entry => entry.Index != (uint)sigil))
        {
            Console.WriteLine($"[Sigil] {connection.CharacterName} sigil {sigil} rejected");
            return true;
        }

        playername.CurrentSigil = (uint)sigil;

        Console.WriteLine($"[Sigil] {connection.CharacterName} shows sigil {sigil}");
        return true;
    }
}
