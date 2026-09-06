namespace SkySaga.Game.Components;

/// <summary>
/// The component GameState_ReadyToPlay waits on before it will advance without timing out.
///
/// The check, at 0x0080FD43:
///     TEST byte ptr [EAX + 0xbc], 0x1
///     JZ   ->skip
///     CALL 0x00718D00                  ; advance to state 9
/// where EAX = GetComponent(playerEntity, hash("JobRankComponent")).
///
/// Missing it costs the whole transition: ClientReadyToPlay -> EnteredGameplay has measured
/// 10.07-10.09 s in every run, which is the timer, not work. Leaving on the timer skips the
/// transition setup, so the transition texture is never captured, ModeGlobal+0xFC4 stays
/// 0x0/format=3, the loading screen never shows, and the screen stays black while audio and game
/// logic keep running.
///
/// The bindings below are the fifteen from the definitions extracted from this client build. They
/// match, name for name and in order, the field list registered by the client's own reflection
/// code at FUN_00ACC370 - which is what makes it clear those definitions are the right ones.
///
/// Only LocationSpecificQuestsReady is enabled. It is a plain bool named exactly like a readiness
/// flag, it did not exist at all in the older definitions, and it is the natural candidate for the
/// bit being tested. Everything else stays off: one change at a time, since a wrong width shifts
/// the whole rest of the stream and breaks things that currently work.
///
/// Widths for the four whose element types are known, decoded from the client's mangled RTTI
/// (MSVC encodes template integers as $0 plus hex digits A-P, terminated by @):
///     JobList                          List&lt;JobEntry, $0EA@ = 64&gt;              ->  7 bits
///     ActiveJobChallengeList           List&lt;ActiveJobChallengeEntry, $0EAA@&gt;   -> 11 bits
///     CompletedJobChallengeList        List&lt;CRCValueBase&lt;I&gt;, $0EAAA@&gt;          -> 15 bits
///     NumberOfJobChallengeScrollsUsed  plain ".I", unbounded                   -> 32 bits
/// The rest are not established yet and must not be guessed.
/// </summary>
public class ClientJobRankComponent : Component
{
    // LocationSpecificQuestsReady went out and did not move the bit - SyncDataBits changed from
    // 1942 to 1943 exactly, so the client received it, and the timeout still won. Left on because
    // it costs one bit and is plausibly needed anyway.
    private static readonly bool SyncLocationSpecificQuestsReady = true;

    // JobList is now on, and this is the actual hypothesis being tested.
    //
    // The component resolves at runtime (key 34 -> slot 31, vptr 0x012CD4D0, which is
    // ClientJobRankComponent from FUN_00840DF0 - it calls the base ctor FUN_00BB45C0 and then
    // overwrites the vptr, which is why searching for the base vtable 0x0133A950 found nothing).
    //
    // Its +0xBC reads 0xFE: bits 1 through 7 set, bit 0 clear. Nothing in the binary clears bit 0
    // - no AND against +0xBC touches it - so it was simply never set. That reads like a
    // "parameter received" bitmap, and the first binding in the definition order is joblist.
    //
    // Expected on the wire: SyncDataBits 1943 -> 1950 (a 7-bit count for List<JobEntry, 64>).
    // Expected in the tool: +0xBC becomes 0xFF and bit0 reads SET.

    private static readonly bool SyncJobList = true;
    private static readonly bool SyncActiveJobChallengeList = false;
    private static readonly bool SyncCompletedJobChallengeList = false;
    private static readonly bool SyncNumberOfJobChallengeScrollsUsed = false;

    private const int JobListMax = 64;
    private const int ActiveJobChallengeListMax = 1024;
    private const int CompletedJobChallengeListMax = 16384;

    /// <summary>The readiness flag the state machine is most likely waiting on.</summary>
    public bool LocationSpecificQuestsReady { get; set => SetIfChanged(ref field, value); } = true;

    public List<int> JobList { get; set => SetIfChanged(ref field, value); } = [];

    public List<int> ActiveJobChallengeList { get; set => SetIfChanged(ref field, value); } = [];

    public List<int> CompletedJobChallengeList { get; set => SetIfChanged(ref field, value); } = [];

    public int NumberOfJobChallengeScrollsUsed { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(LocationSpecificQuestsReady), StringComparison.OrdinalIgnoreCase))
        {
            if (!SyncLocationSpecificQuestsReady)
                return false;

            bitStream.Write(LocationSpecificQuestsReady);

            return true;
        }

        if (parameterName.Equals(nameof(JobList), StringComparison.OrdinalIgnoreCase))
            return SyncJobList && WriteList(bitStream, JobList, JobListMax);

        if (parameterName.Equals(nameof(ActiveJobChallengeList), StringComparison.OrdinalIgnoreCase))
            return SyncActiveJobChallengeList && WriteList(bitStream, ActiveJobChallengeList, ActiveJobChallengeListMax);

        if (parameterName.Equals(nameof(CompletedJobChallengeList), StringComparison.OrdinalIgnoreCase))
            return SyncCompletedJobChallengeList && WriteList(bitStream, CompletedJobChallengeList, CompletedJobChallengeListMax);

        if (parameterName.Equals(nameof(NumberOfJobChallengeScrollsUsed), StringComparison.OrdinalIgnoreCase))
        {
            if (!SyncNumberOfJobChallengeScrollsUsed)
                return false;

            bitStream.Write(NumberOfJobChallengeScrollsUsed);

            return true;
        }

        // NormalJobChallengeRefreshData, MasterJobChallengeRefreshData,
        // CurrentRefreshMasterJobChallengeExpiryTime, ActiveCommunityQuestList,
        // AvailableCommunityQuestList, CommunityQuestExpiryTime, JobChallengeCompletionCountList,
        // ActiveRewardTrackList, QuestsForEntityList and ActiveProgressionTreesList are declared in
        // the definitions but their widths are not known yet, so they are left unwritten.
        return false;
    }

    /// <summary>
    /// The list shape InventoryComponent already uses successfully: the count in a field sized for
    /// the type's maximum, then the elements. An empty list is just a zero count.
    ///
    /// Util.NumBitsRequiredUInt32 returns the LEADING ZERO count, so 32 minus it is the real width.
    /// </summary>
    private static bool WriteList(BitStream bitStream, List<int> values, int maxCount)
    {
        var width = 32 - Util.NumBitsRequiredUInt32((uint)maxCount);

        if (values.Count < maxCount)
        {
            bitStream.WriteBits(BitConverter.GetBytes(values.Count), width, true);
        }
        else
        {
            bitStream.WriteBits(BitConverter.GetBytes(maxCount), width, true);

            bitStream.Write1();
            bitStream.Write(values.Count);
        }

        foreach (var value in values)
            bitStream.Write(value);

        return true;
    }
}
