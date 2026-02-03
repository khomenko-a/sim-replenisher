using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimReplenisher.Domain.Interfaces;

public class ReplenishWorker : BackgroundService
{
    private readonly IReplenishmentOrchestrator _orchestrator;
    private readonly ILogger<ReplenishWorker> _logger;

    public ReplenishWorker(IReplenishmentOrchestrator orchestrator, ILogger<ReplenishWorker> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReplenishWorker has been started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _orchestrator.StartAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Something went wrong!");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}