using System.Linq;

namespace SkySaga.Game.Components;

public class ClientJobRankComponent : Component
{
    private static readonly bool SyncLocationSpecificQuestsReady = true;

    private static readonly bool SyncJobList = true;
    private static readonly bool SyncActiveJobChallengeList = false;
    private static readonly bool CompleteAllChallenges = Environment.GetEnvironmentVariable("SKYSAGA_COMPLETE_CHALLENGES") == "1";
    private static readonly bool SyncNumberOfJobChallengeScrollsUsed = false;

    private const int JobListMax = 64;
    private const int ActiveJobChallengeListMax = 1024;
    private const uint CompletedJobChallengeListEscape = 0x4000;

    public bool LocationSpecificQuestsReady { get; set => SetIfChanged(ref field, value); } = true;

    public List<int> JobList { get; set => SetIfChanged(ref field, value); } = [];

    public List<int> ActiveJobChallengeList { get; set => SetIfChanged(ref field, value); } = [];

    public List<uint> CompletedJobChallengeList { get; set => SetIfChanged(ref field, value); } = CompleteAllChallenges ? AllChallengeIndices() : [];

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
        {
            bitStream.WriteEscapedCount((uint)CompletedJobChallengeList.Count, CompletedJobChallengeListEscape);

            var challengeCount = GeoIndex.Count("JobChallenges");

            foreach (var index in CompletedJobChallengeList)
                bitStream.WriteRangedIndex(index, challengeCount);

            return true;
        }

        if (parameterName.Equals(nameof(NumberOfJobChallengeScrollsUsed), StringComparison.OrdinalIgnoreCase))
        {
            if (!SyncNumberOfJobChallengeScrollsUsed)
                return false;

            bitStream.Write(NumberOfJobChallengeScrollsUsed);

            return true;
        }

        return false;
    }

    private static List<uint> AllChallengeIndices()
        => [.. GeoIndex.Entries("JobChallenges").Select(entry => entry.WireIndex).Order()];

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
