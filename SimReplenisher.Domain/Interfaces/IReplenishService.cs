namespace SimReplenisher.Domain.Interfaces
{
    public interface IReplenishService
    {
        Task ExecuteReplenishment(IPhoneDevice device);
    }
}
