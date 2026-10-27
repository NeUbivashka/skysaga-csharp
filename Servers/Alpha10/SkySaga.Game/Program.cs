
Console.SetOut(TextWriter.Synchronized(new TimestampedTextWriter(Console.Out,
    new StreamWriter(Path.Combine(AppContext.BaseDirectory, "server.log"), append: false) { AutoFlush = true })));

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

try
{
    SkySaga.Game.Database.GameDatabase.Initialize();
}
catch (Exception exception)
{
    Console.WriteLine($"""
        The database is not available: {exception.GetBaseException().Message}
        Press any key to continue . . .
        """);

    Console.ReadKey();

    return;
}

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<Server>();

        services.AddSingleton<IrcServer>();

        services.AddHostedService<ServerHostedService>();
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

await SkySaga.Game.Database.GameDatabase.FlushAsync();

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