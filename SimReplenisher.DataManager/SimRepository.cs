using Microsoft.EntityFrameworkCore;
using SimReplenisher.Domain.Entities;
using SimReplenisher.Domain.Enums;
using SimReplenisher.Domain.Interfaces;
using System.Data;

namespace SimReplenisher.DataManager
{
    public class SimRepository : IDataRepository
    {
        private readonly SimDbContext _context;

        public SimRepository(SimDbContext simDbContext)
        {
            _context = simDbContext;
        }

        public async Task SaveChangesAsync() 
        {
            await _context.SaveChangesAsync();
        }

        public async Task<SimToReplenish?> GetNextJobAndLockAsync()
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);

                try
                {
                    var job = await _context.ReplenishmentRequests
                        .Where(s => s.Status == SimStatus.New || s.Status == SimStatus.Priority)
                        .Include(s => s.SimData)
                        .OrderByDescending(s => s.Status)
                        .ThenBy(s => s.Id)
                        .FirstOrDefaultAsync();

                    if (job != null)
                    {
                        job.Status = SimStatus.Processing;

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }

                    return job;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task LogAsync(SimToReplenish sim, string message)
        {
            var log = new ReplenishmentLog
            {
                SimDataId = sim.SimDataId,
                PhoneNumber = sim.SimData.PhoneNumber,
                Amount = sim.Amount.Value,
                Status = sim.Status == SimStatus.Success,
                AddingDate = sim.AddingDate,
                ExecutionDate = DateTime.UtcNow.AddHours(2),
                Message = message
            };

            _context.RaifLogs.Add(log);
        }
    }
}
