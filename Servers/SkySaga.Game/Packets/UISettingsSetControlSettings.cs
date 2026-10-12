namespace SkySaga.Game.Packets;

/// <summary>
/// RPCUISettingsSetControlSettings, ordinal 22, client to server (send stub FUN_009C3460): one bit.
/// Stored on ClientSettingsComponent (controlsettings binding). What the bit controls is not known
/// yet (the JSON printer's field names are in the packed part of the binary).
/// </summary>
public static class UISettingsSetControlSettings
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(1, out var flag)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings))
            settings.ControlFlag = flag != 0;

        Console.WriteLine($"[UISettings] control flag={flag}");

        return true;
    }
}
