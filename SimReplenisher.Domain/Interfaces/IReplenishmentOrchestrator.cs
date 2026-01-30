namespace SimReplenisher.Domain.Interfaces
{
    public interface IReplenishmentOrchestrator
    {
        Task StartAsync(CancellationToken cancellationToken);
    }
}
