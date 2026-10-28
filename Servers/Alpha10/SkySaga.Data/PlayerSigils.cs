namespace SkySaga.Data;

public static class PlayerSigils
{
    public const uint Default = 10;

    public static readonly uint[] Starter = [2, 8, Default, 14];

    public static uint Current(int? saved) => saved is { } sigil && Starter.Contains((uint)sigil) ? (uint)sigil : Default;
}
