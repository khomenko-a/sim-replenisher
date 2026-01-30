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
            using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                var job = _context.ReplenishmentRequests
                    .Where(s => s.Status == SimStatus.New)
                    .Include(s => s.SimData)
                    .OrderBy(s => s.Id)
                    .FirstOrDefault();

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
        }
    }
}
