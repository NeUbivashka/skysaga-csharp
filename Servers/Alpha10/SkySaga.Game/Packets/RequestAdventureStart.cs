namespace SkySaga.Game.Packets;

public class RequestAdventureStart : ISerializablePacket
{
    internal static readonly Guid OldCityWorldUuid = new("b26e7d65-991d-4d6e-9a84-49d7f4ac0397");
    internal static readonly Guid OldCityServerUuid = new("d6bf148a-6532-4c72-87ba-270dc803811c");
    internal static readonly Guid ForestMasteryWorldUuid = new("2e4ee754-9dbe-4f81-a0f5-332c94964342");
    internal static readonly Guid ForestMasteryServerUuid = new("a277d3e2-71f3-42a9-b5d4-1d5e57f44d49");
    internal static readonly Guid DesertMasteryWorldUuid = new("c614438e-25c1-455e-88c3-5e3aae375238");
    internal static readonly Guid DesertMasteryServerUuid = new("e9d3d013-e11d-4e91-bd25-c32e9d567c9a");
    internal static readonly Guid JungleMasteryWorldUuid = new("87187948-3120-4d17-b8d0-c59f32e7a934");
    internal static readonly Guid JungleMasteryServerUuid = new("c7ec56eb-9e73-4088-ae9d-a1a55d756bda");

    internal sealed record AdventureDestination(
        string EnvironmentName,
        string DisplayName,
        string PortVariable,
        Guid WorldUuid,
        Guid ServerUuid,
        Func<MapSpec> CreateSpec);

    private static readonly AdventureDestination OldCity = new(
        "ftue", "The Old City", "SKYSAGA_FTUE_PORT", OldCityWorldUuid, OldCityServerUuid,
        () => MapSpec.Ftue(seed: 1337));

    private static readonly AdventureDestination ForestMastery = new(
        "mastery-forest", "Forest Mastery Test", "SKYSAGA_MASTERY_FOREST_PORT",
        ForestMasteryWorldUuid, ForestMasteryServerUuid,
        () => MapSpec.MasteryAdventure(seed: 1338, MapSpec.AdventureBiome.Forest));

    private static readonly AdventureDestination DesertMastery = new(
        "mastery-desert", "Desert Mastery Test", "SKYSAGA_MASTERY_DESERT_PORT",
        DesertMasteryWorldUuid, DesertMasteryServerUuid,
        () => MapSpec.MasteryAdventure(seed: 1339, MapSpec.AdventureBiome.Desert));

    private static readonly AdventureDestination JungleMastery = new(
        "mastery-jungle", "Jungle Mastery Test", "SKYSAGA_MASTERY_JUNGLE_PORT",
        JungleMasteryWorldUuid, JungleMasteryServerUuid,
        () => MapSpec.MasteryAdventure(seed: 1340, MapSpec.AdventureBiome.Jungle));

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (WorldTransfer.CurrentWorldKind != "home")
        {
            Console.WriteLine($"[RequestAdventureStart] Ignored request from {connection.Identity.Name} in {WorldTransfer.CurrentWorldKind}.");
            return true;
        }

        if (!MapSpec.TryReadAdventureWireIndex(bitStream, out var adventureWireIndex))
        {
            Console.WriteLine($"[RequestAdventureStart] Could not read the selected adventure from {connection.Identity.Name}; transfer refused.");
            return true;
        }

        var destination = adventureWireIndex switch
        {
            79 => OldCity,
            31 => ForestMastery,
            21 => DesertMastery,
            41 => JungleMastery,
            _ => null,
        };

        if (destination is null)
        {
            Console.WriteLine($"[RequestAdventureStart] {connection.Identity.Name} requested unknown adventure wire index {adventureWireIndex}; transfer refused.");
            return true;
        }

        Console.WriteLine($"[RequestAdventureStart] {connection.Identity.Name} selected {destination.DisplayName} (wire index {adventureWireIndex}).");
        return WorldTransfer.Transfer(connection, (uint)connection.PlayerEntity.Id,
            destination.PortVariable, destination.WorldUuid, destination.ServerUuid,
            destination.CreateSpec(), expectedSourceWorld: "home");
    }

    internal static AdventureDestination? CurrentDestination()
        => Environment.GetEnvironmentVariable("SKYSAGA_ADVENTURE")?.ToLowerInvariant() switch
        {
            "ftue" => OldCity,
            "mastery-forest" => ForestMastery,
            "mastery-desert" => DesertMastery,
            "mastery-jungle" => JungleMastery,
            _ => null,
        };

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestAdventureStart);

        return bitStream;
    }
}
