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

            try
            {
                sim.PrepareForExecution();
            }
            catch (InvalidOperationException)
            {
                sim.Status = SimStatus.UnknownProvider;
                await _dataRepository.LogAsync(sim, "Sim has unknown provider.");
                await _dataRepository.SaveChangesAsync();
                return;
            }

            var scenario = _allScenarios.FirstOrDefault(s => s.Bank == sim.Bank);

            using (_logger.BeginScope("Number: {Number}", sim.SimData.PhoneNumber))
            {
                try
                {
                    if (scenario is null)
                    {
                        _logger.LogError("Replenishment failed due to missing scenario for bank {Bank}", sim.Bank);

                        sim.Status = SimStatus.Failure;
                        await _dataRepository.LogAsync(sim, "Current bank is not supported.");
                        await _dataRepository.SaveChangesAsync();

                        return;
                    }

                    await scenario.ReplenishNumber(device, sim);

                    sim.Status = SimStatus.Success;
                    await _dataRepository.LogAsync(sim, "Replenishment is successfull.");
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

                    switch (ex.Page)
                    {
                        case Page.TechnicalProblem:
                            sim.Status = SimStatus.Failure;
                            await _dataRepository.LogAsync(sim, "Technical problem page loaded. Probably sim is dead.");
                            await _dataRepository.SaveChangesAsync();
                            break;

                        case Page.Blocked:
                            sim.Status = SimStatus.Failure;
                            await _dataRepository.LogAsync(sim, "The bank card is blocked");
                            await _dataRepository.SaveChangesAsync();
                            throw new PageLoadException("The bank card is blocked. No need to continue replenishment.", Page.Blocked, null);

                        case Page.NotEnoughFunds:
                            sim.Status = SimStatus.Failure;
                            await _dataRepository.LogAsync(sim, "Not enough funds.");
                            await _dataRepository.SaveChangesAsync();
                            throw new PageLoadException("Not enough funds.", Page.NotEnoughFunds, null);

                        case Page.Unknown:
                            _logger.LogWarning("Unknown page loaded. This may be a glitch of the emulator. Setting status to new for retry.");
                            sim.Status = SimStatus.New;
                            await _dataRepository.LogAsync(sim, "Unknown page loaded. This may be a glitch of the emulator. Setting status to new for retry.");
                            await _dataRepository.SaveChangesAsync();
                            break;

                        default:
                            sim.Status = SimStatus.Failure;
                            await _dataRepository.LogAsync(sim, $"Unexpected page loaded: {ex.Page}.");
                            await _dataRepository.SaveChangesAsync();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error executing replenishment for {Phone}", sim.SimData.PhoneNumber);

                    sim.Status = SimStatus.Failure;
                    await _dataRepository.LogAsync(sim, $"Error executing replenishment: {ex.Message}");
                    await _dataRepository.SaveChangesAsync();
                }
            }
        }
    }
}
