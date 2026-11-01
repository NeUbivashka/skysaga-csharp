using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data;
using SkySaga.Data.Entities;

namespace SkySaga.Admin.Services;

public sealed record OnlinePlayer(Guid Id, string Name, string Account, Guid WorldId, string World, WorldKind? Kind, bool Premium, DateTimeOffset? Since);

public sealed record LiveEvent(long Id, DateTimeOffset Time, string Kind, Guid? CharacterId, string? Character, string? Data);

public sealed record ServiceStatus(string Name, string Address, bool Up, double? LatencyMs);

public sealed record Slice(string Label, double Value, string Color);

public sealed record TopItem(string Name, long Count);

public sealed record ServiceEndpoint(string Name, string Host, int Port, string Kind);

public sealed class LiveSnapshot
{
    public DateTimeOffset Time { get; init; }
    public double DbLatencyMs { get; init; }
    public string? Error { get; init; }
    public long Accounts { get; init; }
    public long Characters { get; init; }
    public long Worlds { get; init; }
    public long VoxelEdits { get; init; }
    public long Devices { get; init; }
    public long Photos { get; init; }
    public long Friendships { get; init; }
    public long PremiumActive { get; init; }
    public long PremiumForever { get; init; }
    public long Banned { get; init; }
    public long NewAccountsWeek { get; init; }
    public long EventsDay { get; init; }
    public long EventsHour { get; init; }
    public long PeakOnline { get; init; }
    public List<OnlinePlayer> Online { get; init; } = [];
    public List<double> OnlineHistory { get; init; } = [];
    public List<LiveEvent> RecentEvents { get; init; } = [];
    public List<Slice> AccountStatus { get; init; } = [];
    public List<Slice> Roles { get; init; } = [];
    public List<Slice> WorldKinds { get; init; } = [];
    public List<Slice> EventKinds { get; init; } = [];
    public List<Slice> OnlineByWorld { get; init; } = [];
    public List<double> ActivityPerHour { get; init; } = [];
    public List<string> HourLabels { get; init; } = [];
    public List<double> RegistrationsPerDay { get; init; } = [];
    public List<string> DayLabels { get; init; } = [];
    public List<TopItem> TopItems { get; init; } = [];
    public List<ServiceStatus> Services { get; init; } = [];
    public Dictionary<Guid, Guid> Avatars { get; init; } = [];

    public Guid? AvatarOf(Guid? characterId) => characterId is Guid id && Avatars.TryGetValue(id, out var photo) ? photo : null;
}

public sealed class LiveMetrics(IDbContextFactory<SkySagaDbContext> dbFactory, GameNames names, IConfiguration configuration, ILogger<LiveMetrics> logger)
    : BackgroundService
{
    public static readonly string[] Palette = ["#38bdf8", "#a78bfa", "#34d399", "#fbbf24", "#f472b6", "#f87171", "#22d3ee", "#a3e635", "#fb923c", "#94a3b8"];

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    private const int HistoryLength = 150;
    private const int ServiceCheckEvery = 15;

    private static readonly byte[] RakNetMagic = Convert.FromHexString("00FFFF00FEFEFEFEFDFDFDFD12345678");

    private readonly List<double> _history = [];
    private List<ServiceStatus> _services = [];
    private long _peak;

    public LiveSnapshot Snapshot { get; private set; } = new() { Time = DateTimeOffset.UtcNow };

    public event Action? Changed;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tick = 0;
        using var timer = new PeriodicTimer(Interval);

        do
        {
            if (tick++ % ServiceCheckEvery == 0)
                _services = await CheckServicesAsync(stoppingToken);

            try
            {
                Snapshot = await BuildAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Live metrics failed");
                Snapshot = new LiveSnapshot { Time = DateTimeOffset.UtcNow, Error = exception.GetBaseException().Message, Services = _services, OnlineHistory = [.. _history] };
            }

            Changed?.Invoke();
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<LiveSnapshot> BuildAsync(CancellationToken token)
    {
        await using var db = await dbFactory.CreateDbContextAsync(token);
        var now = DateTimeOffset.UtcNow;
        var day = now.AddHours(-24);
        var watch = Stopwatch.StartNew();
        var accounts = await db.Accounts.LongCountAsync(token);
        var latency = watch.Elapsed.TotalMilliseconds;

        var online = await db.Characters.AsNoTracking().Where(c => c.OnlineWorldId != null)
            .Select(c => new { c.Id, c.Name, Account = c.Account!.Username, WorldId = c.OnlineWorldId!.Value, c.Account.PremiumUntil })
            .ToListAsync(token);
        var onlineIds = online.Select(o => o.Id).ToList();
        var worldIds = online.Select(o => o.WorldId).Distinct().ToList();
        var worlds = await db.Worlds.AsNoTracking().Where(w => worldIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Kind, Owner = db.Characters.Where(c => c.Id == w.OwnerCharacterId).Select(c => c.Name).FirstOrDefault() })
            .ToDictionaryAsync(w => w.Id, token);
        var joined = await db.GameEvents.AsNoTracking().Where(e => e.Kind == "player.join" && e.CharacterId != null && onlineIds.Contains(e.CharacterId.Value))
            .GroupBy(e => e.CharacterId!.Value).Select(g => new { Id = g.Key, Time = g.Max(e => e.Time) })
            .ToDictionaryAsync(g => g.Id, g => g.Time, token);

        var onlinePlayers = online.Select(o =>
        {
            var world = worlds.GetValueOrDefault(o.WorldId);
            var title = world is null ? "Instance" : world.Kind switch
            {
                WorldKind.Hub => "Hub",
                WorldKind.Island => world.Owner is { } owner ? $"{owner}'s island" : "Island",
                _ => world.Kind.ToString()
            };

            return new OnlinePlayer(o.Id, o.Name, o.Account, o.WorldId, title, world?.Kind, o.PremiumUntil > now,
                joined.TryGetValue(o.Id, out var since) ? since : null);
        }).OrderBy(p => p.World).ThenBy(p => p.Name).ToList();

        _history.Add(onlinePlayers.Count);
        if (_history.Count > HistoryLength)
            _history.RemoveRange(0, _history.Count - HistoryLength);
        _peak = Math.Max(_peak, onlinePlayers.Count);

        var characterNames = await db.Characters.AsNoTracking().Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, token);
        var recent = await db.GameEvents.AsNoTracking().OrderByDescending(e => e.Id).Take(40).ToListAsync(token);
        var avatars = await db.Photos.AsNoTracking().Where(p => p.IsAvatar && p.UploadedAt != null)
            .OrderBy(p => p.UploadedAt).Select(p => new { p.OwnerCharacterId, p.Id }).ToListAsync(token);

        var states = await db.Accounts.AsNoTracking()
            .Select(a => new { a.BannedUntil, a.LockedUntil, HasPassword = a.PasswordHash != null, a.PremiumUntil }).ToListAsync(token);
        var status = states.Select(a => a.BannedUntil > now ? "Banned" : a.LockedUntil > now ? "Locked" : !a.HasPassword ? "No password" : a.PremiumUntil > now ? "VIP" : "Active")
            .GroupBy(s => s).Select(g => new { g.Key, Count = g.LongCount() });
        var roles = await db.Accounts.AsNoTracking().GroupBy(a => a.Role).Select(g => new { g.Key, Count = g.LongCount() }).ToListAsync(token);
        var kinds = await db.Worlds.AsNoTracking().GroupBy(w => w.Kind).Select(g => new { g.Key, Count = g.LongCount() }).ToListAsync(token);
        var eventKinds = await db.GameEvents.AsNoTracking().Where(e => e.Time >= day).GroupBy(e => e.Kind)
            .Select(g => new { g.Key, Count = g.LongCount() }).OrderByDescending(g => g.Count).ToListAsync(token);

        var perHour = await db.GameEvents.AsNoTracking().Where(e => e.Time >= day)
            .GroupBy(e => new { e.Time.Year, e.Time.Month, e.Time.Day, e.Time.Hour })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
            .ToListAsync(token);
        var hours = Enumerable.Range(0, 24).Select(i => now.AddHours(i - 23)).ToList();

        var firstDay = new DateTimeOffset(now.UtcDateTime.Date.AddDays(-13), TimeSpan.Zero);
        var created = await db.Accounts.AsNoTracking().Where(a => a.CreatedAt >= firstDay).Select(a => a.CreatedAt).ToListAsync(token);
        var days = Enumerable.Range(0, 14).Select(i => firstDay.AddDays(i)).ToList();

        var items = await db.InventoryItems.AsNoTracking().GroupBy(i => i.NameHash)
            .Select(g => new { NameHash = g.Key, Count = g.Sum(i => (long)i.Count) })
            .OrderByDescending(g => g.Count).Take(8).ToListAsync(token);

        return new LiveSnapshot
        {
            Time = now,
            DbLatencyMs = latency,
            Accounts = accounts,
            Characters = await db.Characters.LongCountAsync(token),
            Worlds = await db.Worlds.LongCountAsync(token),
            VoxelEdits = await db.VoxelEdits.LongCountAsync(token),
            Devices = await db.PlacedDevices.LongCountAsync(token),
            Photos = await db.Photos.LongCountAsync(token),
            Friendships = await db.Friendships.LongCountAsync(token) / 2,
            PremiumActive = await db.Accounts.LongCountAsync(a => a.PremiumUntil > now, token),
            PremiumForever = await db.Accounts.LongCountAsync(a => a.PremiumUntil >= Forever.Until, token),
            Banned = await db.Accounts.LongCountAsync(a => a.BannedUntil > now, token),
            NewAccountsWeek = await db.Accounts.LongCountAsync(a => a.CreatedAt >= now.AddDays(-7), token),
            EventsDay = await db.GameEvents.LongCountAsync(e => e.Time >= day, token),
            EventsHour = await db.GameEvents.LongCountAsync(e => e.Time >= now.AddHours(-1), token),
            PeakOnline = _peak,
            Online = onlinePlayers,
            Avatars = avatars.GroupBy(a => a.OwnerCharacterId).ToDictionary(g => g.Key, g => g.Last().Id),
            OnlineHistory = [.. _history],
            RecentEvents = [.. recent.Select(e => new LiveEvent(e.Id, e.Time, e.Kind, e.CharacterId,
                e.CharacterId is Guid id && characterNames.TryGetValue(id, out var name) ? name : null, e.Data))],
            AccountStatus = Slices(status.Select(s => (s.Key, s.Count)), ["Active", "VIP", "Banned", "Locked", "No password"],
                ["#34d399", "#fbbf24", "#f87171", "#fb923c", "#94a3b8"]),
            Roles = Slices(roles.Select(r => (r.Key.ToString(), r.Count)), Enum.GetNames<AccountRole>(), ["#38bdf8", "#a78bfa", "#f472b6"]),
            WorldKinds = Slices(kinds.Select(k => (k.Key.ToString(), k.Count)), Enum.GetNames<WorldKind>(), ["#94a3b8", "#22d3ee", "#a78bfa", "#34d399", "#fbbf24"]),
            EventKinds = [.. eventKinds.Select((k, i) => new Slice(k.Key, k.Count, Palette[i % Palette.Length]))],
            OnlineByWorld = [.. onlinePlayers.GroupBy(p => p.World).OrderByDescending(g => g.Count())
                .Select((g, i) => new Slice(g.Key, g.Count(), Palette[i % Palette.Length]))],
            ActivityPerHour = [.. hours.Select(h => (double)(perHour.FirstOrDefault(p => p.Year == h.Year && p.Month == h.Month && p.Day == h.Day && p.Hour == h.Hour)?.Count ?? 0))],
            HourLabels = [.. hours.Select(h => h.ToLocalTime().ToString("HH:00"))],
            RegistrationsPerDay = [.. days.Select(d => (double)created.Count(c => c.UtcDateTime.Date == d.UtcDateTime.Date))],
            DayLabels = [.. days.Select(d => d.ToString("dd.MM"))],
            TopItems = [.. items.Select(i => new TopItem(names.Item(i.NameHash), i.Count))],
            Services = _services
        };
    }

    private static List<Slice> Slices(IEnumerable<(string Label, long Count)> counts, string[] order, string[] colors)
    {
        var map = counts.ToDictionary(c => c.Label, c => c.Count);

        return [.. order.Select((label, i) => new Slice(label, map.GetValueOrDefault(label), colors[i % colors.Length]))];
    }

    private async Task<List<ServiceStatus>> CheckServicesAsync(CancellationToken token)
    {
        var endpoints = configuration.GetSection("Services").Get<List<ServiceEndpoint>>() ?? [];

        return [.. await Task.WhenAll(endpoints.Select(endpoint => CheckAsync(endpoint, token)))];
    }

    private static async Task<ServiceStatus> CheckAsync(ServiceEndpoint endpoint, CancellationToken token)
    {
        var address = $"{endpoint.Host}:{endpoint.Port}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        var watch = Stopwatch.StartNew();

        try
        {
            if (endpoint.Kind.Equals("raknet", StringComparison.OrdinalIgnoreCase))
            {
                using var udp = new UdpClient();
                var ping = new byte[1 + 8 + RakNetMagic.Length + 8];
                ping[0] = 0x01;
                BitConverter.TryWriteBytes(ping.AsSpan(1, 8), System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(Environment.TickCount64));
                RakNetMagic.CopyTo(ping, 9);

                await udp.SendAsync(ping, endpoint.Host, endpoint.Port, timeout.Token);
                var reply = await udp.ReceiveAsync(timeout.Token);

                return new ServiceStatus(endpoint.Name, address, reply.Buffer is [0x1C, ..], watch.Elapsed.TotalMilliseconds);
            }

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token);

            if (endpoint.Kind.Equals("irc", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(tcp.GetStream(), leaveOpen: true);
                var greeting = await reader.ReadLineAsync(timeout.Token);
                tcp.Client.Shutdown(SocketShutdown.Send);

                return new ServiceStatus(endpoint.Name, address, greeting is not null, watch.Elapsed.TotalMilliseconds);
            }

            return new ServiceStatus(endpoint.Name, address, true, watch.Elapsed.TotalMilliseconds);
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            return new ServiceStatus(endpoint.Name, address, false, null);
        }
    }
}
