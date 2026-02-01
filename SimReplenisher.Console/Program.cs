using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpAdbClient;
using Microsoft.Extensions.Logging;

using SimReplenisher.DataManager;
using SimReplenisher.Domain;
using SimReplenisher.Domain.Interfaces;
using SimReplenisher.Domain.Services;
using SimReplenisher.PhoneManager;
using SimReplenisher.PhoneManager.Scenarios;
using SimReplenisher.TextOnPictureManager;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        builder.Services.AddDbContext<SimDbContext>(options =>
            options.UseMySql(
                connectionString, 
                new MySqlServerVersion(new Version(5, 7, 42)),
                mySqlOptions =>
                {
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                }));

        builder.Logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = false;
            options.SingleLine = true;
            options.TimestampFormat = "[hh:mm:ss] ";
        });

        builder.Services.AddSingleton<IDeviceManager, DeviceManager>();
        builder.Services.AddSingleton<IAdbClient, AdbClient>();
        builder.Services.AddSingleton<IReplenishmentOrchestrator, ReplenishmentOrchestrator>();

        builder.Services.AddScoped<IReplenishService, ReplenishService>();

        builder.Services.AddScoped<IDataRepository, SimRepository>();

        builder.Services.AddScoped<IAppBankScenario, RaifScenario>();

        builder.Services.AddScoped<ITextRecognizerService, TextRecognizerServiceTesseract>();

        builder.Services.AddHostedService<ReplenishWorker>();

        var app = builder.Build();

        app.Run();
    }
}