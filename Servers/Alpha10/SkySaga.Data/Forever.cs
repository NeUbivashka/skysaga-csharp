namespace SkySaga.Data;

public static class Forever
{
    public static readonly DateTimeOffset Until = new(9999, 12, 31, 0, 0, 0, TimeSpan.Zero);

    public static bool Is(DateTimeOffset? time) => time >= Until;
}
