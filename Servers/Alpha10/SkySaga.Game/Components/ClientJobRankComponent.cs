using System.Linq;

namespace SkySaga.Game.Components;

public record JobProgress(string Job, byte Rank, uint Experience, uint PrestigeRank)
{
    public List<string> NormalAvailable { get; init; } = [];
}

public record ActiveJobChallenge(string Name, uint Progress);

public record QuestOffer(int EntityId, List<string> Quests);

public class ClientJobRankComponent : Component
{
    private const uint JobListEscape = 64;
    private const uint JobRankMax = 200;
    private const uint AvailableJobChallengeListEscape = 10;
    private const uint ActiveJobChallengeListEscape = 0x400;
    private const uint CompletedJobChallengeListEscape = 0x4000;
    private const uint QuestsForEntityListEscape = 8;
    private const uint QuestsEscape = 10;

    public bool LocationSpecificQuestsReady { get; set => SetIfChanged(ref field, value); } = true;

    public List<JobProgress> JobList { get; set => SetIfChanged(ref field, value); } = [];

    public List<ActiveJobChallenge> ActiveJobChallengeList { get; set => SetIfChanged(ref field, value); } = [];

    public List<string> CompletedJobChallengeList { get; set => SetIfChanged(ref field, value); } = [];

    public List<QuestOffer> QuestsForEntityList { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(LocationSpecificQuestsReady), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(LocationSpecificQuestsReady);
            return true;
        }

        if (parameterName.Equals(nameof(JobList), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)JobList.Count, JobListEscape);

            foreach (var job in JobList)
            {
                WriteCrc(bitStream, job.Job);
                bitStream.WriteRanged(Math.Min(job.Rank, JobRankMax), JobRankMax);
                bitStream.WriteFullWidthUInt32(job.Experience);
                WriteCrcList(bitStream, job.NormalAvailable, AvailableJobChallengeListEscape);
                WriteCrcList(bitStream, [], AvailableJobChallengeListEscape);
                bitStream.WriteFullWidthUInt32(job.PrestigeRank);
            }

            return true;
        }

        if (parameterName.Equals(nameof(ActiveJobChallengeList), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)ActiveJobChallengeList.Count, ActiveJobChallengeListEscape);

            foreach (var challenge in ActiveJobChallengeList)
            {
                WriteCrc(bitStream, challenge.Name);
                bitStream.WriteFullWidthUInt32(challenge.Progress);
                bitStream.Write0();
                bitStream.WriteUInt64(0);
                bitStream.WriteUInt64(0);
                bitStream.WriteUInt64(0);
                bitStream.Write0();
                bitStream.Write0();
            }

            return true;
        }

        if (parameterName.Equals(nameof(QuestsForEntityList), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)QuestsForEntityList.Count, QuestsForEntityListEscape);

            foreach (var offer in QuestsForEntityList)
            {
                bitStream.WriteFullWidthUInt32((uint)offer.EntityId);
                bitStream.WriteEscapedCount((uint)offer.Quests.Count, QuestsEscape);

                foreach (var quest in offer.Quests)
                {
                    WriteCrc(bitStream, quest);
                    bitStream.WriteClientUuid(null);
                    bitStream.Write1();
                }
            }

            return true;
        }

        if (parameterName.Equals(nameof(CompletedJobChallengeList), StringComparison.OrdinalIgnoreCase))
        {
            WriteCrcList(bitStream, CompletedJobChallengeList, CompletedJobChallengeListEscape, sorted: true);
            return true;
        }

        return false;
    }

    private static void WriteCrc(BitStream bitStream, string name)
    {
        bitStream.Write1();
        bitStream.WriteFullWidthUInt32(Util.ComputeCrc32(name));
    }

    private static void WriteCrcList(BitStream bitStream, IReadOnlyCollection<string> names, uint escape, bool sorted = false)
    {
        bitStream.WriteEscapedCount((uint)names.Count, escape);

        var crcs = names.Select(name => Util.ComputeCrc32(name));

        foreach (var crc in sorted ? crcs.Order() : crcs)
        {
            bitStream.Write1();
            bitStream.WriteFullWidthUInt32(crc);
        }
    }

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
}
