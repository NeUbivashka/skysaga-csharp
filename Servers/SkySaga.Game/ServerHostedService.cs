using System.Threading;
using System.Threading.Tasks;

namespace SkySaga.Game;

public class ServerHostedService(
    Server server,
    ILogger<ServerHostedService> logger,
    IHostApplicationLifetime lifetime) : IHostedService
{
    private readonly Server _server = server ?? throw new ArgumentNullException(nameof(server));
    private readonly ILogger<ServerHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IHostApplicationLifetime _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    private Task? _tickTask;
    private Task? _consoleTask;
    private CancellationTokenSource? _tickCancellation;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting server...");

        if (!_server.Start())
        {
            _logger.LogError("Failed to start server");
            _lifetime.StopApplication();
            return Task.CompletedTask;
        }

        _logger.LogInformation("Server started successfully");

        _tickCancellation = new CancellationTokenSource();
        _tickTask = Task.Run(() => TickLoop(_tickCancellation.Token), cancellationToken);



        _logger.LogInformation("Type 'reset' + Enter to reload the map and respawn everything (disconnect clients first).");
        _logger.LogInformation("Type 'chickens' + Enter to list every chicken's current position, spawn point, distance traveled, and AI state.");
        _consoleTask = Task.Run(() => ConsoleCommandLoop(_tickCancellation.Token), cancellationToken);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping server...");

        _tickCancellation?.Cancel();

        if (_tickTask != null)
        {
            await _tickTask;
        }

        _logger.LogInformation("Server stopped");
    }

    private void TickLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _server.Tick();
                Thread.Sleep(1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in server tick loop");
            }
        }
    }

    private void ConsoleCommandLoop(CancellationToken cancellationToken)
    {



        while (!cancellationToken.IsCancellationRequested)
        {
            var line = Console.ReadLine();

            if (line is null)
                return;

            if (string.Equals(line.Trim(), "reset", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Resetting world from Data\\WorldMap.json...");

                try
                {
                    _server.ResetWorld();
                    _logger.LogInformation("World reset. Reconnect any clients now.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "World reset failed");
                }
            }
            else if (string.Equals(line.Trim(), "chickens", StringComparison.OrdinalIgnoreCase))
            {
                _server.RequestListChickens();
            }
        }
    }
}
