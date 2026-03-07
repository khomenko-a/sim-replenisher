
using SimReplenisher.Domain.Entities;

namespace SimReplenisher.Domain.Interfaces
{
    public interface IDataRepository
    {
        Task<SimToReplenish?> GetNextJobAndLockAsync();
        Task SaveChangesAsync();
        Task LogAsync(SimToReplenish sim, string message);
    }
}
