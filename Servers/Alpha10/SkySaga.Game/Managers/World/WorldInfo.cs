using SkySaga.Data.Entities;

namespace SkySaga.Game.Managers.World;

public sealed record WorldInfo(
    Guid Id,
    WorldKind Kind,
    string Template,
    string? TemplateVersion,
    uint Seed,
    Guid? OwnerCharacterId,
    string? OwnerName,
    WorldAccess VisitAccess,
    WorldAccess BuildAccess)
{
    public bool IsPersistent => Kind == WorldKind.Island;

    public bool IsShared => Kind is WorldKind.Hub or WorldKind.Island;

    public bool CanVisit(Guid characterId) => Kind switch
    {
        WorldKind.Hub => true,
        WorldKind.Island => Allows(VisitAccess, characterId),
        _ => OwnerCharacterId == characterId
    };

    public bool CanBuild(Guid characterId) => Kind switch
    {
        WorldKind.Hub or WorldKind.CharacterCreation => false,
        WorldKind.Island => Allows(BuildAccess, characterId),
        _ => true
    };

    private bool Allows(WorldAccess access, Guid characterId)
        => characterId == OwnerCharacterId || access == WorldAccess.Everyone;
}
