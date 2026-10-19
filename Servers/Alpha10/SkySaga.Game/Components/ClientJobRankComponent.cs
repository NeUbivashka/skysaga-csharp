using System.Linq;

namespace SkySaga.Game.Components;

public record JobProgress(string Job, byte Rank, uint Experience, uint PrestigeRank);

public class ClientJobRankComponent : Component
{
    private static readonly bool SyncLocationSpecificQuestsReady = true;

    private static readonly bool SyncJobList = true;
    private static readonly bool SyncActiveJobChallengeList = false;
    private static readonly bool CompleteAllChallenges = Environment.GetEnvironmentVariable("SKYSAGA_COMPLETE_CHALLENGES") == "1";
    private static readonly bool SyncNumberOfJobChallengeScrollsUsed = false;

    private const uint JobListEscape = 64;
    private const uint JobRankMax = 200;
    private const uint AvailableJobChallengeListEscape = 10;
    private const int ActiveJobChallengeListMax = 1024;
    private const uint CompletedJobChallengeListEscape = 0x4000;

    public bool LocationSpecificQuestsReady { get; set => SetIfChanged(ref field, value); } = true;

    public List<JobProgress> JobList { get; set => SetIfChanged(ref field, value); } = [];

    public List<int> ActiveJobChallengeList { get; set => SetIfChanged(ref field, value); } = [];

    public List<uint> CompletedJobChallengeList { get; set => SetIfChanged(ref field, value); } = CompleteAllChallenges ? AllChallengeCrcs() : [];

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
        {
            if (!SyncJobList)
                return false;

            bitStream.WriteEscapedCount((uint)JobList.Count, JobListEscape);

            foreach (var job in JobList)
            {
                bitStream.Write1();
                bitStream.WriteFullWidthUInt32(Util.ComputeCrc32(job.Job));
                bitStream.WriteRanged(Math.Min(job.Rank, JobRankMax), JobRankMax);
                bitStream.WriteFullWidthUInt32(job.Experience);
                bitStream.WriteEscapedCount(0, AvailableJobChallengeListEscape);
                bitStream.WriteEscapedCount(0, AvailableJobChallengeListEscape);
                bitStream.WriteFullWidthUInt32(job.PrestigeRank);
            }

            return true;
        }

        if (parameterName.Equals(nameof(ActiveJobChallengeList), StringComparison.OrdinalIgnoreCase))
            return SyncActiveJobChallengeList && WriteList(bitStream, ActiveJobChallengeList, ActiveJobChallengeListMax);

        if (parameterName.Equals(nameof(CompletedJobChallengeList), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)CompletedJobChallengeList.Count, CompletedJobChallengeListEscape);

            foreach (var crc in CompletedJobChallengeList)
            {
                bitStream.Write1();
                bitStream.WriteFullWidthUInt32(crc);
            }

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

    private static List<uint> AllChallengeCrcs() => [.. JobChallengeTable.All.Select(name => Util.ComputeCrc32(name)).Order()];

    public (JobProgress Before, JobProgress After)? AddExperience(string jobName, uint amount)
    {
        if (!JobTable.TryGet(jobName, out var job))
            return null;

        var index = JobList.FindIndex(entry => entry.Job.Equals(job.Name, StringComparison.OrdinalIgnoreCase));
        var before = index >= 0 ? JobList[index] : new JobProgress(job.Name, 0, 0, 0);
        var rank = (int)before.Rank;
        var experience = (long)before.Experience + amount;

        while (rank < job.RankExperience.Length - 1 && experience >= job.RankExperience[rank])
            experience -= job.RankExperience[rank++];

        if (rank == job.RankExperience.Length - 1)
            experience = Math.Min(experience, job.RankExperience[rank]);

        var after = before with { Rank = (byte)rank, Experience = (uint)experience };
        List<JobProgress> list = [.. JobList];

        if (index >= 0)
            list[index] = after;
        else
            list.Add(after);

        JobList = list;
        return (before, after);
    }

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
