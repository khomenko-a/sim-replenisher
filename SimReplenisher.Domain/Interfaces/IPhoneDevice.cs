using System.Xml;

namespace SimReplenisher.Domain.Interfaces
{
    public interface IPhoneDevice
    {
        Task<XmlDocument?> GetXmlDumpAsync();
        Task TapAsync(int x1, int y1, int x2, int y2);
        Task ExecuteAdbShellCommandAsync(string command);
        Task InputTextAsync(string text);
        Task GoToHomeScreenAsync();
        Task OpenBankApp(string bankApp);
        Task CloseBankAppAsync(string bankApp);
        Task<byte[]> TakeScreenshotAsync();
    }
}
