using SkySaga.Data;

namespace SkySaga.Admin.Services;

public static class Format
{
    public static string Ago(DateTimeOffset? time)
    {
        if (time is not DateTimeOffset value)
            return "never";

        var span = DateTimeOffset.UtcNow - value;

        return span.TotalSeconds < 10 ? "just now"
            : span.TotalMinutes < 1 ? $"{(int)span.TotalSeconds} s ago"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min ago"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours} h ago"
            : $"{(int)span.TotalDays} d ago";
    }

    public static string Left(DateTimeOffset until)
    {
        var span = until - DateTimeOffset.UtcNow;

        return span.TotalHours < 1 ? $"{Math.Max((int)span.TotalMinutes, 0)} min left"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours} h left"
            : span.TotalDays < 60 ? $"{(int)span.TotalDays} d {span.Hours} h left"
            : $"{(int)span.TotalDays} d left";
    }

    public static string For(TimeSpan span)
        => span.TotalMinutes < 1 ? $"{(int)span.TotalSeconds} s"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours} h {span.Minutes} min"
            : $"{(int)span.TotalDays} d {span.Hours} h";

    public static string Until(DateTimeOffset? until)
        => until is not DateTimeOffset value ? "—"
            : Forever.Is(value) ? "forever"
            : value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public static string Initial(string name) => string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();

    public static string AvatarGradient(string name)
    {
        var hash = (uint)name.Aggregate(17, (h, c) => h * 31 + c);
        var a = LiveMetrics.Palette[hash % 8];
        var b = LiveMetrics.Palette[(hash / 8 + 3) % 8];

        return $"background:linear-gradient(135deg,{a},{b})";
    }

    public static string EventColor(string kind) => kind.Split('.')[0] switch
    {
        "admin" => "#fbbf24",
        "player" => "#38bdf8",
        "combat" => "#f87171",
        "adventure" => "#34d399",
        "world" => "#22d3ee",
        _ => "#94a3b8"
    };

    public static string EventIcon(string kind) => kind switch
    {
        "player.join" => MudBlazor.Icons.Material.Filled.Login,
        "player.leave" => MudBlazor.Icons.Material.Filled.Logout,
        "player.death" => MudBlazor.Icons.Material.Filled.HeartBroken,
        "player.travel" => MudBlazor.Icons.Material.Filled.Flight,
        "combat.kill" => MudBlazor.Icons.Material.Filled.LocalFireDepartment,
        "adventure.start" => MudBlazor.Icons.Material.Filled.Explore,
        "admin.ban" => MudBlazor.Icons.Material.Filled.Gavel,
        "admin.unban" => MudBlazor.Icons.Material.Filled.LockOpen,
        "admin.premium" => MudBlazor.Icons.Material.Filled.WorkspacePremium,
        "admin.role" => MudBlazor.Icons.Material.Filled.AdminPanelSettings,
        _ => kind.StartsWith("admin.") ? MudBlazor.Icons.Material.Filled.Shield : MudBlazor.Icons.Material.Filled.Bolt
    };
}
