using System.Linq;

namespace SkySaga.Game.Packets;

/// <summary>
/// Handles ordinal 29 (RPCRunActionList) from the client.
///
/// The old version read the payload, printed it, and returned. That is the whole bug this file
/// is about: the client asks the server to run an action list, the server answers with nothing,
/// and the action list never runs anywhere.
///
/// Why that matters for the stuck FTUE, from GeoData.json. The only adventure in the game with
/// IsFTUE:true is FTUE_The_Old_City (Index 78), and its cutscenes carry their consequences in
/// OnCompleteActions.AlwaysActions. Eva_Leads_Player (AnimatedSequences Index 7) ends with:
///
///     OldCity_Gladiator_1_HideNode
///     OldCity_GladiatorSpawner_1_Despawn
///     OldCity_GladiatorSpawner_2_Spawn
///     Trigger_Eva_Leads_Player_Part2_OnEntities
///     Unlock_OldCity_Tutorial_Move          <-- movement input
///
/// and Rescue_Dilemma_Choice_Explorer (Index 9) ends with Unlock_OldCity_Tutorial_Attack,
/// Unlock_OldCity_Reach_ExplorerLeader and SetCheckpoint_OldCity_LeftWing. Nothing else in the
/// data unlocks input. If the action lists never run, movement and attack stay locked and only
/// ESC responds - which is exactly the reported symptom.
///
/// Sequence 7 also declares FadeSettings { InitialState: "Black", FadeAtEnd: true } with
/// UseCutsceneUI and ShowBars, so the fullscreen veil belongs to the cutscene, not to the
/// loading screen. That is consistent with the census finding of fade widgets sitting Inactive
/// with no anigraph: put up, never animated out.
///
/// WHY ECHOING IS SAFE HERE, WHEN IT CRASHED FOR 271
///
/// Echoing 268's payload into ordinal 271 produced STATUS_FLOAT_MULTIPLE_TRAPS, because those
/// are two different packet shapes - 271's handler reads a float out of bits that were written
/// by 268's writer. That failure does not apply here. These bits were produced by the client's
/// own ordinal-29 writer, so the client's own ordinal-29 reader is symmetric with them by
/// construction. Same ordinal, same layout.
///
/// OBSERVED LAYOUT (four captures, one session)
///
///     00 00 04 3c | 00 00 00 04 | 94 a4 2f 7e 7c 67 88 b7 | 28 more bytes
///     00 00 04 b2 | 00 00 00 01 | 94 a4 2f 7e 7c 67 88 b7 | ...
///     00 00 04 bc | 00 00 00 04 | 94 a4 2f 7e 7c 67 88 b7 | ...
///     00 00 04 c8 | 00 00 00 04 | 94 a4 2f 7e 7c 67 88 b7 | ...
///
/// 352 bits after the one-byte id. First u32 big-endian varies per call (1084, 1202, 1212,
/// 1224) and is presumably the action-list id. Second u32 is an entity id: 4 is the player, 1 is
/// HUB_NPC_Gladiator_OldCity - which is the entity Eva_Leads_Player names in its
/// AdditionalPropData, so the pairing is consistent. The eight bytes after it were identical in
/// all four captures. The remaining 28 bytes are not decoded and are not touched here.
///
/// The fields above are only logged. Nothing is parsed out to build the reply, so a wrong guess
/// about their meaning cannot corrupt what goes on the wire.
///
/// NOT VERIFIED: that ordinal 29 is handled in the server-to-client direction at all. It has
/// only ever been observed client-to-server. This is the experiment. Change one flag at a time,
/// and Packets/RunActionList.cs stays unused on purpose - it serialises a string, which does not
/// match anything above, and would be unparseable if it were ever sent.
/// </summary>
public static class RunActionListReceived
{
    /// <summary>
    /// Send the action list straight back to the client that asked for it.
    ///
    /// OFF. The experiment ran and the answer is no: ordinal 29 is not handled inbound.
    ///
    /// Every receive stub in the client ends with FUN_00BB3E50(1, ordinal, bytes, name), and
    /// every send stub with the same call and a leading 0. Walking all 263 call sites, ordinal 29
    /// appears only as SEND, at stub 0x00A51A10 - there is no receive side. So the echo has been
    /// going out and being dropped by the dispatcher, exactly like ResourceCheck (10) and
    /// UnlockPosition (157) were. That matches the observed behaviour: the echo was on for many
    /// runs and the client's reply never changed by a single bit.
    ///
    /// Turning this off is not the experiment - it removes a packet that provably does nothing.
    /// The experiment is StartAnimationSequence below.
    ///
    /// Not const: a const bool folds the branch away and trips CS0162.
    /// </summary>
    private static readonly bool EchoToRequester = false;

    /// <summary>
    /// Start the animation sequence ourselves, without waiting for the client to ask.
    ///
    /// OFF. The experiment ran, it worked, and it was the wrong thing to do.
    ///
    /// GCIntro_StoryTextVideo is meant to loop until the player presses ESC. Ending it is the
    /// player's job, not the server's - so there is no packet missing here, and sending 272 early
    /// just forges the keypress. The client obeyed it and fast-forwarded the whole intro:
    ///
    ///     17:00:24.921  FullScreenVideoCompleted   raw c0   (GCIntro_StoryTextVideo)
    ///     17:00:24.922  NarrativeCompleted         49 7c c0 00 00 00 10
    ///     17:00:24.923  NarrativeCompleted         4a 7c c0 00 00 00 10
    ///     17:00:29.073  FullScreenVideoCompleted   raw 40
    ///     17:00:29.074  RequestStartAnimationSequence  00 00 00 13 38
    ///
    /// Two narratives completing one millisecond apart is text being cancelled, not read, which
    /// is why the voice-over stopped playing. Note the payload byte differs between the two video
    /// completions - c0 against 40, one bit - so the first one is probably flagged as interrupted.
    ///
    /// Three things it did establish, and they hold regardless:
    ///
    ///   1. The client is not deadlocked. Given a nudge it leaves the loop and drives the rest of
    ///      the chain on its own - videos, narratives, then its own 268.
    ///   2. The 268 payload shape read off the earlier captures is correct. The client's own
    ///      request came back as 00 00 00 13 38, byte for byte what we had synthesised.
    ///   3. Sequence index 7 is right.
    ///
    /// So whatever keeps the screen white, it is not a missing "stop the video" packet. Do not
    /// turn this back on to chase the white screen - it will only skip the intro again.
    ///
    /// Not const: a const bool folds the branch away and trips CS0162.
    /// </summary>
    private static readonly bool StartAnimationSequence = false;

    /// <summary>
    /// Sequence index, in the top five bits of the trailing byte. 0x38 is index 7, which is
    /// Eva_Leads_Player - the sequence whose OnCompleteActions unlock movement.
    /// </summary>
    private const byte SequenceIndexByte = 0x38;

    /// <summary>
    /// Also forward to every other connection. Off - with one client under test it changes
    /// nothing, and turning it on at the same time as the echo would make it impossible to tell
    /// which one moved the client. Turn this on only once the echo is settled and a second
    /// client is actually connected.
    /// </summary>
    private static readonly bool BroadcastToOthers = false;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();

        // Ordinal 29 is below 121, so the id occupied a single byte on the wire - unlike the
        // 268 family, which needs 16 bits for the 0xFF escape plus one byte.
        var remainingBits = (uint)Math.Max(0, totalBits - 8);
        var payload = new byte[(remainingBits + 7) / 8];

        var readOk = remainingBits == 0 || bitStream.ReadBits(payload, remainingBits, alignBitsToRight: true);

        var hex = string.Join(" ", payload.Select(b => b.ToString("x2")));

        var actionListId = payload.Length >= 4
            ? (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3]
            : -1;

        var entityId = payload.Length >= 8
            ? (payload[4] << 24) | (payload[5] << 16) | (payload[6] << 8) | payload[7]
            : -1;

        var expectedId = connection.PlayerEntity.Id;
        var idNote = entityId == expectedId
            ? "the player entity"
            : $"NOT the player entity ({expectedId})";

        Console.WriteLine($"[RunActionList] Received (ordinal 29). totalBits={totalBits} "
            + $"payloadBits={remainingBits} readOk={readOk} raw: {hex}");
        Console.WriteLine($"[RunActionList] ActionListId={actionListId} EntityId={entityId} ({idNote}).");

        if (!readOk)
        {
            // Echoing bits we failed to read would put garbage on the wire. Drop instead, and
            // say so, rather than sending something the client has to survive.
            Console.WriteLine("[RunActionList] Payload read FAILED - not echoing.");

            return true;
        }

        if (EchoToRequester)
        {
            connection.Send(new RawPayloadPacket
            {
                PacketId = PacketId.RunActionList,
                Payload = payload,
                PayloadBits = remainingBits
            });

            Console.WriteLine($"[RunActionList] Echoed ordinal 29 back to the requester, "
                + $"{remainingBits} payload bits.");
        }

        if (StartAnimationSequence && entityId > 0)
        {
            var sequencePayload = new byte[]
            {
                (byte)(entityId >> 24),
                (byte)(entityId >> 16),
                (byte)(entityId >> 8),
                (byte)entityId,
                SequenceIndexByte
            };

            connection.Send(new RawPayloadPacket
            {
                PacketId = PacketId.AnimationSequenceStarting,
                Payload = sequencePayload,
                PayloadBits = (uint)sequencePayload.Length * 8
            });

            Console.WriteLine($"[RunActionList] Sent AnimationSequenceStarting (ordinal 272) "
                + $"unprompted for EntityId={entityId}, sequence index {SequenceIndexByte >> 3}, "
                + $"payload {string.Join(" ", sequencePayload.Select(b => b.ToString("x2")))}.");
        }

        if (BroadcastToOthers)
        {
            connection.PlayerConnectionManager.BroadcastToAllExcept(connection.Guid.g, new RawPayloadPacket
            {
                PacketId = PacketId.RunActionList,
                Payload = payload,
                PayloadBits = remainingBits
            });

            Console.WriteLine("[RunActionList] Forwarded ordinal 29 to the other connections.");
        }

        return true;
    }
}
