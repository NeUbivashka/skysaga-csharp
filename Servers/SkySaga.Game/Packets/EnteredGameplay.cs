using System.Linq;

namespace SkySaga.Game.Packets;

public static class EnteredGameplay
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();
        var remainingBits = (uint)(totalBits - 8);
        var buffer = new byte[(remainingBits + 7) / 8];
        var readOk = remainingBits == 0 || bitStream.ReadBits(buffer, remainingBits, alignBitsToRight: true);
        var hex = string.Join(" ", buffer.Select(b => b.ToString("x2")));

        Console.WriteLine($"[EnteredGameplay] Received. readOk={readOk} raw: {hex}");

        const string videoName = "GCIntro_StoryTextVideo";

        // The client selects the video by its index in its own Videos list, not by name and not
        // by hash. This used to send a CRC32 of the name, of which the client read the top two
        // bits as the index - so which video started was down to two bits of a hash.
        //
        // GeoData's Videos list has three entries and indices are 1-based, so GCIntro_
        // StoryTextVideo is 3. It is the looping one (SkySaga_IntroStoryBackground_Loop with
        // INTRO_STORY_TEXT over it) and its on-complete transition points at CGIntro_Video1.
        var playVideo = new PlayFullScreenVideo
        {
            VideoIndex = PlayFullScreenVideo.GCIntroStoryTextVideo
        };

        // Only read back by FullScreenVideoCompleted's log line, so it stays accurate about which
        // video the client is acknowledging.
        connection.CurrentVideoFileName = videoName;

        // TeleportBegins IS a teleport, not just a picture hint - see the note on _teleportSent.
        var teleporting = SendTeleport && !_teleportSent;

        if (teleporting)
        {
            _teleportSent = true;

            SendTeleportBegins(connection);

            // No video on the connection that is about to teleport. EnteredGameplay arrives twice
            // - once before the teleport and once on the connection that comes back after it -
            // and sending the video both times overlapped two playbacks, which is what the doubled
            // audio was. The first one would be torn down by the teleport seconds later anyway,
            // so the connection that stays is the one that should get it.
            Console.WriteLine("[EnteredGameplay] Teleporting - holding PlayFullScreenVideo until "
                + "the client comes back.");

            return true;
        }

        SendIsLocal(connection);

        // Debug switch: SKYSAGA_SKIP_INTRO=1 goes straight to the world. The client reads Esc through
        // raw input, so a posted key can't skip the video when testing unattended. Nothing on the
        // server depends on FullScreenVideoCompleted.
        if (Environment.GetEnvironmentVariable("SKYSAGA_SKIP_INTRO") == "1")
        {
            Console.WriteLine("[EnteredGameplay] SKYSAGA_SKIP_INTRO=1 - not sending PlayFullScreenVideo.");
            return true;
        }

        var playVideoStream = playVideo.Serialize();

        connection.Send(playVideoStream);

        Console.WriteLine($"[EnteredGameplay] Sent PlayFullScreenVideo('{videoName}', index="
            + $"{playVideo.VideoIndex}, {playVideoStream.GetNumberOfBytesUsed()} bytes, "
            + $"{playVideoStream.GetNumberOfBitsUsed()} bits used).");

        return true;
    }

    /// <summary>
    /// Sent once per server run, not once per connection.
    ///
    /// Confirmed working: after this packet the client's ModeGlobal+0xFC4 went from
    /// 0x0/format=3 to "2x79 format=0 VALID", ui_loading_screen's tick gate at +0x194 cleared,
    /// and the game-state flags at +0x38 went 0x11 -> 0x17, which is the first time the
    /// (flags &amp; 5) == 5 condition has ever been met. The 2 and 79 are Searchable[0] and
    /// Searchable[2] from MapSpec, so the client parsed the body exactly as a map descriptor.
    ///
    /// But ordinal 35 is a real teleport, not a hint: the client disconnects and reconnects to
    /// the destination. Sending it on every EnteredGameplay therefore loops - connect, enter
    /// gameplay, teleport, reconnect, repeat - which is what the reconnect storm was.
    ///
    /// A static flag is crude and only right for single-client testing. For more than one player
    /// this belongs on the connection or the save, keyed by whether that player has already been
    /// placed in the world.
    /// </summary>
    /// <summary>
    /// Master switch for TeleportBegins.
    ///
    /// OFF (2026-10-11). The white screen was never the loading screen: it was ui_portalfade, lifted
    /// by Player.islocal (see SendIsLocal). Without the teleport, ui_loading_screen (screen 0x45)
    /// closes by itself, and the reconnect it forced is gone. The history below is kept for reference.
    ///
    /// (Old note) ON. This is not an optional extra - it is the only way the loading screen ever leaves.
    ///
    /// Measured with unstick while the screen was white:
    ///
    ///     fsm=4  tickGate(+0x194)=1  flag(+0x160)=0
    ///     image 0x0 format=3 data=0x00000000 INVALID
    ///     state@+0xD04 flags=0x11
    ///
    /// ui_loading_screen's tick skips its whole state machine while +0x194 is set, and only
    /// clears it once FUN_00C0CD80(ModeGlobal + 0xFC4) passes. That object is a MapSpec, not an
    /// image - destructor FUN_007B85B0 -> FUN_00C0CD20, constructor FUN_00C0CC30, 0x60 bytes -
    /// and it was reading exactly what its default constructor leaves, meaning nothing had ever
    /// written to it.
    ///
    /// There is exactly one writer in the binary, the TeleportBegins handler:
    ///
    ///     void FUN_00964BA0(int this, ..., param_3) {
    ///         *(byte *)(this + 0x38) |= 2;
    ///         ModeGlobal.FC4 = param_3;   // FUN_007176F0 is MapSpec::operator=(this=dest)
    ///     }
    ///
    /// (Corrected: FUN_00964010 used to be listed here as the writer. At 0x00964259 it puts a
    /// temp in ECX and pushes +0xFC4 as the source, so it copies the spec OUT into slot 0x45.
    /// Every +0xFC4 access is classified in docs/client/loading-screen.md.)
    ///
    /// flags read 0x11, so bit 1 (0x02) was missing. The handler raises it and stores the spec.
    /// Without ordinal 35 the bit never sets, the condition never holds, +0xFC4 stays default and
    /// the panel never lifts. The earlier run that reached flags 0x17 and a VALID image was this
    /// path firing correctly, not a side effect.
    ///
    /// The reconnect is the protocol, not damage. A teleport moves the client to another world -
    /// in the live game, another server - so it disconnects and comes back. The server has to
    /// accept the second connection as the same player arriving, rather than starting over.
    /// _teleportSent keeps the second one from teleporting again; whether that is enough depends
    /// on what the second connection actually does, which is what this run is for.
    /// </summary>
    private static readonly bool SendTeleport = false;

    /// <summary>
    /// Append a 32-bit float after the MapDefinition-shaped body.
    ///
    /// Why: the body alone is 90 bytes and the client evidently reads past it. Reading past the
    /// end of a BitStream yields zeros, and a zero landed in a duration field - FUN_00D45E60
    /// divides by the float at +0x38, and its other caller (FUN_00D459B0) explicitly guards with
    /// `if ((float)param_1[0xe] &lt;= 0.0) return;`, so that field is normally positive. The
    /// unguarded caller is what crashed with STATUS_FLOAT_MULTIPLE_TRAPS at 0x00D45EDC.
    ///
    /// So the payload continues past where we stop, and at least one of the missing fields is a
    /// duration. This is the cheapest probe for that. If it does not help, vary
    /// TrailingFloatValue or turn SendTeleport off and we go read the real tail out of the client.
    /// </summary>
    private static readonly bool AppendTrailingFloat = false;

    private const float TrailingFloatValue = 1.0f;

    private static bool _teleportSent;

    /// <summary>
    /// Tell the owner its player entity is the local one: EntitySync carrying only "islocal" = true.
    ///
    /// This is what lifts the white screen. The client keeps islocal as bit 0x08 of the player
    /// entity's flags byte (+0x81), and every local-player path gates on it. The portal fade does
    /// too: FUN_0086B9C0 (entity events) and FUN_0086BD40 (tick) return early without it. With
    /// the bit clear, the PortalFade that TeleportBegins leaves on ui_portalfade (screen 0x42) is
    /// never faded out.
    ///
    /// It goes out as a parameter change, not in the initial EntityAdd, for two reasons:
    ///  - the change itself is the trigger: FUN_0086B8A0 (on-parameters-changed) handles binding 17
    ///    by firing entity event 0xB and, with the bit now set, requesting PortalFadeEnterBiome
    ///    (+0x91 = 1, +0x92 = 0), which fades the white out;
    ///  - in the initial add it crashed the client in READY_TO_PLAY (0x00D45EDC, see WorldManager).
    ///
    /// Owner-only, because islocal must stay false for every other client.
    ///
    /// Timing: sent on the next server tick after EnteredGameplay. The old observations ("a copy in
    /// the same tick was ignored", "accepted in some runs", "cleared by itself ~4 s later") were all
    /// one bug: Entity.GetSyncData lacked the escape bit before the parameter-block length, so the
    /// client read the bool from the bits after the packet's end. Fixed there; traced with
    /// Tools/trace/trace_islocal.py (2026-10-12: first copy applied, later copies are no-ops).
    ///
    /// The send itself happens in PlayerConnection.Tick, on the server thread - see the note on
    /// PlayerConnection.IsLocalDueUtc for the race that made it unreliable from a Task.Delay.
    /// See docs/client/loading-screen.md.
    /// </summary>
    private static void SendIsLocal(PlayerConnection connection)
    {
        // Immediately, plus one safety copy a second later (a no-op if the first applied: the client
        // only reacts to a change of the value).
        var now = DateTime.UtcNow;
        connection.IsLocalDueUtc.Enqueue(now);
        connection.IsLocalDueUtc.Enqueue(now + TimeSpan.FromSeconds(1));
    }

    private static void SendTeleportBegins(PlayerConnection connection)
    {
        // TeleportBegins (ordinal 35) carries the destination map descriptor. Its handler fills
        // ModeGlobal+0xFC4, the struct ui_loading_screen's tick is blocked on - without it the
        // loading screen never appears and its background picture never changes.
        //
        // It has to go out HERE, not at ClientConnected. The handler is registered by
        // FUN_00964C00, called from FUN_009644F0, which is the gameplay state's Enter. Sending it
        // at connect time - which is what the first attempt did - means the client has no handler
        // for ordinal 35 yet and drops the packet silently. That matches what happened: 90 bytes
        // went out, the client neither crashed nor reacted.
        //
        // Timing from the client's own loading-section byte (ModeGlobal+0xD34):
        //     FRONTEND -> READY_TO_PLAY (13) -> [10.2 s timeout] -> TELEPORTING (9) -> stuck
        // TELEPORTING is exactly the section this packet belongs to.
        var definition = connection.WorldManager.Definition;

        // One field. FUN_00964EE0 reads a MapSpec and stops; the six extra fields this used to
        // carry were never read by anyone.
        var teleportBegins = new TeleportBegins
        {
            Spec = definition.Spec
        };

        var teleportStream = teleportBegins.Serialize();

        // OFF. TeleportBegins is one MapSpec and nothing else - FUN_00964EE0 reads the spec via
        // FUN_007E9810 and stops. This float was added when MapSpec.Encode was still missing
        // nonSearchable, masteryLevels and version: the client overran the end of the spec, a zero
        // reached a divisor in FUN_00D45E60 and it trapped at 0x00D45EDC. The spec is complete
        // now, so there is nothing left to overrun.
        if (AppendTrailingFloat)
            teleportStream.WriteBits(BitConverter.GetBytes(TrailingFloatValue), 32, true);

        Console.WriteLine($"[EnteredGameplay] Sending TeleportBegins (ordinal 35), "
            + $"{teleportStream.GetNumberOfBytesUsed()} bytes"
            + (AppendTrailingFloat ? $", trailing float {TrailingFloatValue}." : "."));

        connection.Send(teleportStream);
    }

}