namespace SkySaga.Game.Packets;

public static class SetPlayerState
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(CharacterStateComponent.CharacterStateMax, out var state)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientCharacterStateComponent>(out var characterState))
            characterState.CharacterStateId = (byte)state;

        return true;
    }
}
