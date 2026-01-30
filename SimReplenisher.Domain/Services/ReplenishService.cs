using Microsoft.Extensions.Logging;
using SimReplenisher.Domain.Entities;
using SimReplenisher.Domain.Enums;
using SimReplenisher.Domain.Exceptions;
using SimReplenisher.Domain.Interfaces;

namespace SimReplenisher.Domain.Services
{
    public class ReplenishService : IReplenishService
    {
        private readonly IDataRepository _dataRepository;
        private readonly IEnumerable<IAppBankScenario> _allScenarios;
        private readonly ILogger<ReplenishService> _logger;

        private static readonly string SCREEN_DUMP_FOLDER = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorDumps");

        public ReplenishService(IDataRepository dataRepository, IEnumerable<IAppBankScenario> allScenarios, ILogger<ReplenishService> logger)
        {
            _dataRepository = dataRepository;
            _allScenarios = allScenarios;
            _logger = logger;
        }

        public async Task ExecuteReplenishment(IPhoneDevice device)
        {
            var sim = await _dataRepository.GetNextJobAndLockAsync();

            if (sim is null)
            {
                return;
            }

            sim.PrepareForExecution();

            var scenario = _allScenarios.FirstOrDefault(s => s.Bank == sim.Bank);

            try
            {
                if (scenario is null)
                {
                    _logger.LogError("Replenishment failed due to missing scenario for bank {Bank}", sim.Bank);

                    sim.Status = SimStatus.Failure;
                    await _dataRepository.SaveChangesAsync();

                    return;
                }

                if (!Directory.Exists(SCREEN_DUMP_FOLDER))
                {
                    Directory.CreateDirectory(SCREEN_DUMP_FOLDER);
                }

                await scenario.ReplenishNumber(device, sim);

                sim.Status = SimStatus.Success;
                await _dataRepository.SaveChangesAsync();
            }
            catch (PageLoadException ex)
            {
                _logger.LogError("Error loading {Page} page.", ex.Page.ToString());

                if (ex.ScreenDump != null)
                {
                    var fileName = $"Error_{sim.SimData.PhoneNumber}_{ex.Page}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
                    var fullPath = Path.Combine(SCREEN_DUMP_FOLDER, fileName);

                    ex.ScreenDump.Save(fullPath);
                }

                sim.Status = SimStatus.Failure;
                await _dataRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error executing replenishment for {Phone}", sim.SimData.PhoneNumber);

                sim.Status = SimStatus.Failure;
                await _dataRepository.SaveChangesAsync();

                throw;
            }
        }
    }
}
