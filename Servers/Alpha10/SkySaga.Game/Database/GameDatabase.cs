using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data;

namespace SkySaga.Game.Database;

public static class GameDatabase
{
    private static DbContextOptions<SkySagaDbContext>? _options;
    private static readonly Channel<Func<SkySagaDbContext, Task>> Writes =
        Channel.CreateUnbounded<Func<SkySagaDbContext, Task>>(new UnboundedChannelOptions { SingleReader = true });
    private static Task? _writer;

    public static void Initialize()
    {
        _options = SkySagaDatabase.Options(SkySagaDatabase.ConnectionString(Path.Combine(AppContext.BaseDirectory, "Data")));

        using (var context = Context())
            context.Database.Migrate();

        _writer = Task.Run(WriteLoop);

        Console.WriteLine("[Database] Connected, schema up to date");
    }

    public static SkySagaDbContext Context()
        => new(_options ?? throw new InvalidOperationException("The database is not initialised."));

    public static void Enqueue(Func<SkySagaDbContext, Task> write) => Writes.Writer.TryWrite(write);

    public static async Task FlushAsync()
    {
        Writes.Writer.TryComplete();

        if (_writer is not null)
            await _writer;
    }

    private static async Task WriteLoop()
    {
        await foreach (var write in Writes.Reader.ReadAllAsync())
        {
            try
            {
                await using var context = Context();
                await write(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] Write failed: {ex.GetBaseException().Message}");
            }
        }
    }
}
