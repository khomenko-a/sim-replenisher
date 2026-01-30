namespace SimReplenisher.Domain.Interfaces
{
    public interface ITextRecognizerService
    {
        Task<string> RecognizeTextAsync(byte[] imageData);
        Task<bool> ContainsTextAsync(byte[] imageData, string text);
    }
}
