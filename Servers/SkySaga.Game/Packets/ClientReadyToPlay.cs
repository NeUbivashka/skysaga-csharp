namespace SkySaga.Game.Packets;

public static class ClientReadyToPlay
{
    /// <summary>
    /// Send TeleportBegins from here rather than from EnteredGameplay.
    ///
    /// OFF. This is the change that caused the white screen, and the note below predicted it
    /// exactly - "if it does not exist yet the packet is simply dropped [...] and the image
    /// staying INVALID in the tool will say so". It said so in every run since, and we read it as
    /// a side detail.
    ///
    /// The chain, now read out of the client instead of guessed. ui_loading_screen's tick
    /// (FUN_008F6EE0, slot 5 of vtable 0x012E8BFC) branches on +0x194 BEFORE its state machine:
    ///
    ///     if (this[0x194] == 0) { switch (this[0x17C]) { case 0,1,3,4: ... } }
    ///     else if (FUN_00C0CD80(ModeGlobal + 0xFC4)) {
    ///         FUN_008F8780(ModeGlobal + 0xFC4);
    ///         this[0x17C] = 4;
    ///         this[0x194] = 0;            // it clears the flag itself, when done
    ///     }
    ///
    /// So +0x194 is a request, not a block, and while it is set the switch does not run at all.
    /// The condition is a plain image-validity test:
    ///
    ///     FUN_00C0CD80(img) = img[0x2E] != 0 && img[0x30] != 0 && img[0x0C] != 3
    ///                          (width)          (height)          (format)
    ///
    /// ModeGlobal+0xFC4 read 'width 0, height 0, format 3' in every run with the teleport sent
    /// from here - all three fail, the flag never clears, the state machine never advances, the
    /// loading screen never leaves. It draws a blank fullscreen panel over the world and over
    /// fullscreen video, under the menu and under the cutscene fade. That is the white screen.
    ///
    /// It also explains the two dead ends: +0x160 is set by case 4, and the loading-screen tips
    /// (with their /_suggest request) are case 3 - neither can run while the switch is skipped.
    ///
    /// Sending TeleportBegins from EnteredGameplay is the only configuration ever observed to
    /// make that image valid ("2x79 format=0"). Hence this goes off and EnteredGameplay goes on.
    ///
    /// Known cost: ordinal 35 after gameplay entry is a real teleport, so expect the client to
    /// disconnect and reconnect - a second ID_NEW_INCOMING_CONNECTION with a fresh guid. That is
    /// a separate problem from the white screen and is worth paying to confirm the diagnosis. The
    /// proper fix is to fill +0xFC4 without a teleport, which means reading FUN_008F8780.
    /// </summary>
    private static readonly bool SendTeleportHere = false;

    /// <summary>
    /// One shot per server run. Without this the teleport fires on every connection, and since a
    /// teleport causes a reconnect, the client loops: connect, ready-to-play, teleport, reconnect,
    /// repeat - never staying long enough to play the intro videos. That is what "no videos, like
    /// we went straight into the game" was.
    ///
    /// Same caveat as the copy in EnteredGameplay: a static is only right while one client is
    /// being tested. For real use this belongs on the connection or the save, keyed by whether
    /// that player has already been placed in the world.
    /// </summary>
    private static bool _teleportSent;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        Debug.WriteLine("", nameof(ClientReadyToPlay));

        // UnlockPosition (ordinal 157) used to be sent here, between SetClientEntity and
        // PlayerSpawned. It is a client-to-server packet: the client has no receive stub for 157,
        // only a sender (FUN_007E5770, which opens an outgoing RPC via FUN_00BC56B0(0x9D, ...)
        // with an empty body). Every copy the server sent was dropped by the dispatcher.
        connection.Send(new SetClientEntity { EntityId = connection.PlayerEntity.Id });

        // PlayerSpawned carries the spawn transform. It used to go out empty, which left the
        // client on its own primed defaults - position (0, -0x3FC0, 0) and both angles at
        // -0x3200 - no matter where the server had actually placed the player.
        var playerSpawned = new PlayerSpawned
        {
            EntityId = connection.PlayerEntity.Id
        };

        if (connection.PlayerEntity.TryGetComponent<SmoothedTransformComponent>(out var transform))
        {
            playerSpawned.Position = transform.Position;
            playerSpawned.YawDegrees = transform.YawDegrees;
        }

        connection.Send(playerSpawned);

        Console.WriteLine($"[ClientReadyToPlay] Sent SetClientLocalPlayerEntity(EntityId={connection.PlayerEntity.Id}), "
            + $"PlayerSpawned(Position={playerSpawned.Position}, Yaw={playerSpawned.YawDegrees}, "
            + $"Pitch={playerSpawned.PitchDegrees}).");

        // TeleportBegins here instead of at EnteredGameplay.
        //
        // Sending it from EnteredGameplay is proven to work: ModeGlobal+0xFC4 went from
        // 0x0/format=3 to "2x79 format=0 VALID", ui_loading_screen's +0x194 gate cleared, and the
        // game-state flags reached 0x17 for the first time. But it lands after the client is
        // already in gameplay, so the client teleports properly - disconnects, reconnects - and we
        // pay an extra round trip for a world it was already in.
        //
        // ClientReadyToPlay is ~10 s earlier and before gameplay entry, so if the ordinal-35
        // handler already exists at this point the initial load becomes the teleport and there is
        // no second connection. If it does not exist yet the packet is simply dropped, exactly as
        // it was when we tried ClientConnected - harmless, and the image staying INVALID in the
        // tool will say so.
        if (SendTeleportHere && !_teleportSent)
        {
            _teleportSent = true;

            var definition = connection.WorldManager.Definition;

            // One field. FUN_00964EE0 reads a MapSpec via FUN_007E9810 and stops - the six extra
            // fields this used to carry were never read, and neither was the trailing float.
            //
            // That float was not arbitrary: it stopped a real STATUS_FLOAT_MULTIPLE_TRAPS at
            // 0x00D45EDC. But the cause was MapSpec.Encode being short of nonSearchable,
            // masteryLevels and version at the time, so the client overran the end of the spec and
            // a zero reached a divisor in FUN_00D45E60. The spec is complete now; there is nothing
            // left to overrun, and the workaround goes with the bug.
            var teleportBegins = new TeleportBegins
            {
                Spec = definition.Spec
            };

            var teleportStream = teleportBegins.Serialize();

            Console.WriteLine($"[ClientReadyToPlay] Sending TeleportBegins (ordinal 35), "
                + $"{teleportStream.GetNumberOfBytesUsed()} bytes.");

            connection.Send(teleportStream);
        }

        var entityAdd = new EntityAdd
        {
            Id = connection.PlayerEntity.Id,
            NameHash = Util.ComputeCrc32(connection.PlayerEntity.Name),
            SyncData = connection.PlayerEntity.GetSyncData(newEntity: true)
        };

        connection.PlayerConnectionManager.BroadcastToAllExcept(connection.Guid.g, entityAdd);

        return true;
    }
}
