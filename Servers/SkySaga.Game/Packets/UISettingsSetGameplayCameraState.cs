namespace SkySaga.Game.Packets;

/// <summary>
/// RPCUISettingsSetGameplayCameraState, ordinal 17, client to server (send stub FUN_009C37D0):
///
///   flag    1 bit
///   state   ranged max 1 (1 bit)
///
/// Stored on ClientSettingsComponent (currentgameplaycamerastate binding, read FUN_00C25E30).
/// </summary>
public static class UISettingsSetGameplayCameraState
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(1, out var flag)) return false;
        if (!bitStream.ReadInt32(ClientSettingsComponent.CameraStateMax, out var state)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings))
        {
            settings.CameraFlag = flag != 0;
            settings.CameraState = (uint)state;
        }

        Console.WriteLine($"[UISettings] camera flag={flag} state={state}");

        return true;
    }
}
