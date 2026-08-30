namespace SkySaga.Game.Packets;

public class MapSpec
{

















    public uint[] Searchable = new uint[14];

    public string AdventureType = string.Empty;

    public uint Seed;

    public string FeatureName = string.Empty;

    public uint Cost;

    private const uint CostMax = 0x10;





    private static readonly uint[] SearchableCounts = { 6, 25, 145, 4, 46, 46, 46, 46, 17, 6, 6, 32, 11, 4 };







    public static MapSpec HomeIsland(uint seed)
    {
        return new MapSpec
        {
            Searchable = new uint[]
            {
                2,
                0,
                83,
                0,
                0,
                0,
                0,
                0,
                6,
                0,
                5,
                4,
                0,
                3,
            },
            Seed = seed,
            FeatureName = "Home_Island_World",
        };
    }














    public static MapSpec Ftue(uint seed)
    {
        return new MapSpec
        {
            Searchable = new uint[]
            {
                2,
                0,
                79,
                0,
                0,
                0,
                0,
                0,
                0,
                4,
                5,
                7,
                0,
                3,
            },
            Seed = seed,
            FeatureName = "FeatureGroup_Overground_FTUE_The_Old_City",




            AdventureType = "Adventure",
        };
    }

    public void Encode(BitStream bitStream)
    {
        for (var slot = 0; slot < 4; slot++)
            bitStream.WriteRangedIndex(Searchable[slot], SearchableCounts[slot]);

        bitStream.WriteString(AdventureType);
        bitStream.WriteBits(BitConverter.GetBytes(Seed), 32, true);

        for (var slot = 4; slot < 13; slot++)
            bitStream.WriteRangedIndex(Searchable[slot], SearchableCounts[slot]);

        bitStream.WriteString(FeatureName);
        bitStream.WriteRangedIndex(Searchable[13], SearchableCounts[13]);

        bitStream.WriteBits(BitConverter.GetBytes(Cost), 32 - Util.NumBitsRequiredUInt32(CostMax), true);
    }
}
