
var isSocialWorld = Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "social";
var isAdventureWorld = Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "adventure"
    || Environment.GetEnvironmentVariable("SKYSAGA_ADVENTURE")?.Equals("ftue", StringComparison.OrdinalIgnoreCase) == true;
var isHomeIslandInstance = Environment.GetEnvironmentVariable("SKYSAGA_HOME_OWNER_UUID") is { Length: > 0 };
var logName = Environment.GetEnvironmentVariable("SKYSAGA_INSTANCE_LOG")
    ?? (isSocialWorld ? "server-socialhub.log" : isAdventureWorld ? "server-adventure.log" : "server.log");
Console.SetOut(TextWriter.Synchronized(new TimestampedTextWriter(Console.Out,
    new StreamWriter(Path.Combine(AppContext.BaseDirectory, logName), append: false) { AutoFlush = true })));

if (!File.Exists("RakNet.dll"))
{
    Console.WriteLine("""
        The RakNet DLL is missing.
        Press any key to continue . . .
        """);

    Console.ReadKey();

    return;
}

try
{
    _ = new RakString();
}
catch
{
    Console.WriteLine("""
        RakNet DLL issue.
        Most likely the provided DLL wasn't build with the C# wrapper file.
        Press any key to continue . . .
        """);

    Console.ReadKey();

    return;
}

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<PlayerConnectionManager>();
        services.AddSingleton<IWorldManager, WorldManager>();
        services.AddSingleton(sp => new PlayerInitializer(sp.GetRequiredService<IWorldManager>()));

        services.AddSingleton<Server>();

        services.AddSingleton<IrcServer>();

        services.AddHostedService<ServerHostedService>();
        if (!isSocialWorld && !isHomeIslandInstance)
            services.AddHostedService<IrcServerHostedService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();

        logging.SetMinimumLevel(LogLevel.Information);
    });

var host = builder.Build();

await host.RunAsync();

Console.WriteLine("""
        Server has stopped.
        Press any key to continue . . .
        """);

Console.ReadKey();

sealed class TimestampedTextWriter(TextWriter inner, TextWriter log) : TextWriter
{
    public override System.Text.Encoding Encoding => inner.Encoding;

    public override void WriteLine(string? value)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {value}";
        inner.WriteLine(line);
        log.WriteLine(line);
    }

    public override void Write(char value)
    {
        inner.Write(value);
        log.Write(value);
    }

    public override void Write(string? value)
    {
        inner.Write(value);
        log.Write(value);
    }

    public override void Flush()
    {
        inner.Flush();
        log.Flush();
    }
}
