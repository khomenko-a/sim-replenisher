using SimReplenisher.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SimReplenisher.Domain
{
    public class ReplenishmentOrchestrator : IReplenishmentOrchestrator
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDeviceManager _deviceManager;
        private readonly ILogger<ReplenishmentOrchestrator> _logger;

        private const int DELAY_GET_DEVICES = 10000;

        public ReplenishmentOrchestrator(IServiceScopeFactory scopeFactory, IDeviceManager deviceManager, ILogger<ReplenishmentOrchestrator> logger)
        {
            _scopeFactory = scopeFactory;
            _deviceManager = deviceManager;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<IPhoneDevice> devices;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Operation cancelled before retrieving devices.");
                    return;
                }

                var foundDevices = _deviceManager.GetConnectedDevices().ToList();

                if (foundDevices.Count > 0)
                {
                    devices = foundDevices;
                    break;
                }

                _logger.LogWarning("No connected devices found. Retrying in {Delay} ms...", DELAY_GET_DEVICES);

                try
                {
                    await Task.Delay(DELAY_GET_DEVICES, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Operation cancelled while waiting for devices.");
                    return;
                }
            }

            _logger.LogInformation("Connected devices found: {Count}", devices.Count);

            var tasks = devices.Select(device => StartLoopWorkAsync(device, cancellationToken));

            await Task.WhenAll(tasks);
        }

        private async Task StartLoopWorkAsync(IPhoneDevice device, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();

                var replenishService = scope.ServiceProvider.GetRequiredService<IReplenishService>();

                await replenishService.ExecuteReplenishment(device);

                try
                {
                    await Task.Delay(3000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Operation cancelled.");
                    return;
                }
            }
        }
    }
}
