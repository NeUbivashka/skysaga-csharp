namespace SkySaga.Game.Components;

public class PlayerAspectsComponent : Component
{
    public bool CanEditMap { get; set => SetIfChanged(ref field, value); }
    public bool CanDamageEntities { get; set => SetIfChanged(ref field, value); }
    public bool CanDamagePlayers { get; set => SetIfChanged(ref field, value); }
    public bool CanCreateDevices { get; set => SetIfChanged(ref field, value); }
    public bool CanDamageDevices { get; set => SetIfChanged(ref field, value); }
    public bool IsSpectator { get; set => SetIfChanged(ref field, value); }
    public bool IsTeleporting { get; set => SetIfChanged(ref field, value); }
    public bool IsDebugPlayer { get; set => SetIfChanged(ref field, value); }
    public string? Tags { get; set => SetIfChanged(ref field, value); }
    public int AccountLevel { get; set => SetIfChanged(ref field, value); }

    /// <summary>
    /// "islocal" (clientplayeraspectscomponent binding 17). Not stored: it is true exactly when the
    /// sync data is going to the entity's owner, false for every other client.
    ///
    /// The client keeps it in the player entity's flags (+0x81 bit 0x08). Every local-player path
    /// gates on that bit, the portal fade among them: FUN_0086B9C0 / FUN_0086BD40 return
    /// early without it. Without the bit, the white PortalFade that TeleportBegins leaves on screen
    /// is never faded out (PortalFadeEnterBiome / CancelFade on ui_portalfade, screen 0x42).
    /// That was the white screen. See docs/client/loading-screen.md.
    /// </summary>
    public bool IsLocal => SyncingForOwner;

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(IsLocal), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(IsLocal);

            return true;
        }
        else if (parameterName.Equals(nameof(CanEditMap), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(CanEditMap);

            return true;
        }
        else if (parameterName.Equals(nameof(CanDamageEntities), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(CanDamageEntities);

            return true;
        }
        else if (parameterName.Equals(nameof(CanDamagePlayers), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(CanDamagePlayers);

            return true;
        }
        else if (parameterName.Equals(nameof(CanCreateDevices), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(CanCreateDevices);

            return true;
        }
        else if (parameterName.Equals(nameof(CanDamageDevices), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(CanDamageDevices);

            return true;
        }
        else if (parameterName.Equals(nameof(IsSpectator), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(IsSpectator);

            return true;
        }
        else if (parameterName.Equals(nameof(IsTeleporting), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(IsTeleporting);

            return true;
        }
        else if (parameterName.Equals(nameof(IsDebugPlayer), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(IsDebugPlayer);

            return true;
        }
        else if (parameterName.Equals(nameof(Tags), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteString(Tags);

            return true;
        }
        else if (parameterName.Equals(nameof(AccountLevel), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(AccountLevel), 32 - Util.NumBitsRequiredUInt32(3), true);

            return true;
        }

        return false;
    }
}