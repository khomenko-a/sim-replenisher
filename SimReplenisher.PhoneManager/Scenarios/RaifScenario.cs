using Microsoft.Extensions.Logging;
using SimReplenisher.Domain.Entities;
using SimReplenisher.Domain.Enums;
using SimReplenisher.Domain.Exceptions;
using SimReplenisher.Domain.Interfaces;
using System.Xml;

namespace SimReplenisher.PhoneManager.Scenarios
{
    public class RaifScenario : IAppBankScenario
    {
        private readonly ITextRecognizerService _textRecognizerService;
        private readonly ILogger _logger;

        private static readonly string[] MAIN_PAGE_MARKERS =
        {
            "Усі рахунки",
            "Головна",
            "Депозити",
            "Усі платежі"
        };
        private static readonly string[] AMOUNT_PAGE_MARKERS =
        {
            "Немає комісії",
            "0,00",
            "З картки"
        };

        private const string PASSWORD_PAGE_MARKER = "//node[@text=\"Введіть пароль\"]";
        private const string TOP_UP_CELL_PHONE_PAGE_MARKER = "//node[contains(@text, 'Отримувач')]";
        private const string CONFIRMATION_PAGE_MARKER = "//node[contains(@text, 'Оплатити')]";
        private const string SUCCESS_PAGE_MARKER = "//node[@text='Платіж прийнято']";
        private const string HOMESCREEN_PAGE_MARKER = "//node[@resource-id='com.ldmnq.launcher3:id/workspace']";
        private const string AMOUNT_PAGE_MARKER = "//node[@resource-id='uds_amount_input_amount']";
        private const string PROCESSING_PAGE_MARKER = "//node[@text='Обробка платежу']";

        private const string APP_PACKAGE_NAME = "ua.raiffeisen.myraif";
        private const int PASSWORD_LENGTH = 4;
        private const int MAX_ATTEMPTS_OPEN_PAGE = 2;
        private const int MAX_ATTEMPTS_IDENTIFY_PAGE = 5;
        private const int MAX_ATTEMMPTS_GET_INCASE_OF_EMULATOR_LAG = 2;
        private const int MAX_ATTEMPTS_RETURN_TO_MAIN_SCREEN = 10;

        private const int SHORT_DELAY = 2000;
        private const int MIDDLE_DELAY = 4000;
        private const int LONG_DELAY = 7000;

        public RaifScenario(ITextRecognizerService textRecognizerService, ILogger<RaifScenario> logger)
        {
            _textRecognizerService = textRecognizerService;
            _logger = logger;
        }

        public AppType Bank => AppType.RaiffaisenBank;

        public async Task ReplenishNumber(IPhoneDevice device, SimToReplenish simToReplenish)
        {
            int totalAttempts = 0;

            var previousPage = Page.Default;
            var currentPage = Page.Default;
            int stuckOnPageCount = 0;

            while (totalAttempts < MAX_ATTEMPTS_IDENTIFY_PAGE)
            {
                currentPage = await DetectCurrentPageAsync(device, previousPage, currentPage);

                if (currentPage == Page.Unknown)
                {
                    _logger.LogWarning("Could not identify the current page. Retrying...");
                    totalAttempts++;
                    await Task.Delay(MIDDLE_DELAY);

                    if (totalAttempts == MAX_ATTEMPTS_IDENTIFY_PAGE)
                    {
                        var xml = await device.GetXmlDumpAsync();
                        await device.CloseBankAppAsync(APP_PACKAGE_NAME);
                        await Task.Delay(SHORT_DELAY);
                        throw new PageLoadException(currentPage, xml);
                    }

                    continue;
                }

                _logger.LogInformation("Current page detected: {CurrentPage}", currentPage);

                if (currentPage == previousPage)
                {
                    if (stuckOnPageCount == MAX_ATTEMPTS_OPEN_PAGE)
                    {
                        var xml = await device.GetXmlDumpAsync();
                        await ReturnToMainPageAsync(device);
                        throw new PageLoadException(currentPage + 1, xml);
                    }

                    if (currentPage != Page.Processing)
                    {
                        stuckOnPageCount++;
                        await Task.Delay(LONG_DELAY);
                    }
                }
                else
                {
                    stuckOnPageCount = 0;
                    totalAttempts = 0;
                }

                previousPage = currentPage;

                switch (currentPage)
                {
                    case Page.HomeScreen:
                        await OpenAppOnMainPageAsync(device);
                        await Task.Delay(LONG_DELAY);
                        break;

                    case Page.PasswordInput:
                        await UnlockBankApp(device);
                        await Task.Delay(LONG_DELAY);
                        break;

                    case Page.Main:
                        await OpenTopUpCellPhonePageAsync(device);
                        await Task.Delay(MIDDLE_DELAY);
                        break;

                    case Page.TopUpCellPhone:
                        await OpenAmountPageAsync(device, simToReplenish);
                        await Task.Delay(MIDDLE_DELAY);
                        break;

                    case Page.AmountSelection:
                        await OpenConfirmationReplenishmentPageAsync(device, simToReplenish);
                        await Task.Delay(LONG_DELAY);
                        break;

                    case Page.Confirmation:
                        await OpenConfirmedPageAsync(device);
                        await Task.Delay(LONG_DELAY);
                        break;

                    case Page.Processing:
                        _logger.LogInformation("Processing payment, waiting...");
                        await Task.Delay(MIDDLE_DELAY);
                        break;

                    case Page.Success:
                        await FinishReplenishmentAsync(device);
                        await Task.Delay(SHORT_DELAY);
                        return;
                }
            }
        }

        private async Task ReturnToMainPageAsync(IPhoneDevice device)
        {
            _logger.LogWarning("Returning to main page");
            var currentPage = await DetectCurrentPageAsync(device);
            int attempts = 0;

            while (currentPage != Page.Main && attempts < MAX_ATTEMPTS_RETURN_TO_MAIN_SCREEN)
            {
                await device.ExecuteAdbShellCommandAsync("input keyevent 4");
                await Task.Delay(SHORT_DELAY);
                currentPage = await DetectCurrentPageAsync(device, currentPage);
                attempts++;
            }

            if (currentPage != Page.Main)
            {
                await device.CloseBankAppAsync(APP_PACKAGE_NAME);
            }
        }

        private async Task<Page> DetectCurrentPageAsync(IPhoneDevice device, Page previousPage = Page.Unknown, Page currentPage = Page.Default)
        {
            var xml = await device.GetXmlDumpAsync();
            if (xml != null)
            {
                if (xml.SelectSingleNode(HOMESCREEN_PAGE_MARKER) != null) return Page.HomeScreen;
                if (xml.SelectSingleNode(PASSWORD_PAGE_MARKER) != null) return Page.PasswordInput;
                if (xml.SelectSingleNode(SUCCESS_PAGE_MARKER) != null) return Page.Success;
                if (xml.SelectSingleNode(AMOUNT_PAGE_MARKER) != null) return Page.AmountSelection;
                if (xml.SelectSingleNode(TOP_UP_CELL_PHONE_PAGE_MARKER) != null) return Page.TopUpCellPhone;
                if (xml.SelectSingleNode(CONFIRMATION_PAGE_MARKER) != null) return Page.Confirmation;
                if (xml.SelectSingleNode(PROCESSING_PAGE_MARKER) != null) return Page.Processing;
            }

            for (int attempt = 0; attempt < MAX_ATTEMMPTS_GET_INCASE_OF_EMULATOR_LAG; attempt++) // in case of emulator lag try to recognize text a few times
            {
                if (previousPage == Page.TopUpCellPhone
                || previousPage == Page.PasswordInput
                || previousPage == Page.HomeScreen
                || previousPage == Page.Default
                || currentPage == Page.Main
                || currentPage == Page.AmountSelection)
                {
                    var text = await TakeScreenShotAndRecognizeText(device);

                    if (MAIN_PAGE_MARKERS.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase)))
                        return Page.Main;

                    if (AMOUNT_PAGE_MARKERS.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase)))
                        return Page.AmountSelection;
                }
                await Task.Delay(SHORT_DELAY);
            }

            return Page.Unknown;
        }

        private async Task OpenAppOnMainPageAsync(IPhoneDevice device) //add logging
        {
            _logger.LogInformation("Opening bank app");
            await device.OpenBankApp(APP_PACKAGE_NAME);

            await Task.Delay(LONG_DELAY);

            for (int i = 0; i < MAX_ATTEMPTS_IDENTIFY_PAGE; i++)
            {
                var page = await DetectCurrentPageAsync(device);

                switch (page)
                {
                    case Page.Main:
                        return;

                    case Page.PasswordInput:
                        await UnlockBankApp(device);
                        await Task.Delay(LONG_DELAY);
                        return;

                    default:
                        _logger.LogInformation("Current page is {Page}. Trying again...", page);
                        await Task.Delay(MIDDLE_DELAY);
                        break;
                }
            }
        }

        private async Task UnlockBankApp(IPhoneDevice device)
        {
            _logger.LogInformation("Entering password");
            for (int i = 0; i < PASSWORD_LENGTH; i++)
            {
                await device.TapAsync(240, 900, 350, 1000); // enter password
                await Task.Delay(SHORT_DELAY);
            }
        }

        private async Task OpenTopUpCellPhonePageAsync(IPhoneDevice device)
        {
            _logger.LogInformation("Clicking Top up cell phone");
            await device.TapAsync(620, 1270, 720, 1330); // tap Top up cell phone
        }

        private async Task OpenAmountPageAsync(IPhoneDevice device, SimToReplenish simToReplenish)
        {
            _logger.LogInformation("Clicking phone number field");
            await device.TapAsync(48, 438, 1032, 630); // tap phone number field

            await Task.Delay(SHORT_DELAY);

            await device.InputTextAsync(simToReplenish.SimData.PhoneNumber);

            await Task.Delay(MIDDLE_DELAY);

            _logger.LogInformation("Clicking Top Up button");
            await device.TapAsync(100, 1790, 1000, 1850); // tap Top Up
        }

        private async Task OpenConfirmationReplenishmentPageAsync(IPhoneDevice device, SimToReplenish simToReplenish)
        {
            await device.InputTextAsync(simToReplenish.Amount.ToString());

            await Task.Delay(SHORT_DELAY);

            _logger.LogInformation("Clicking Continue button");
            await device.TapAsync(100, 1790, 1000, 1850); // tap Continue
        }

        private async Task OpenConfirmedPageAsync(IPhoneDevice device)
        {
            _logger.LogInformation("Clicking Pay button");
            await device.TapAsync(100, 1790, 1000, 1850); // tap Pay
        }

        private async Task FinishReplenishmentAsync(IPhoneDevice device)
        {
            _logger.LogInformation("Replenishment successful");
            await device.TapAsync(100, 1790, 1000, 1850);
        }

        private async Task<string> TakeScreenShotAndRecognizeText(IPhoneDevice device)
        {
            var screenShot = await device.TakeScreenshotAsync();

            return await _textRecognizerService.RecognizeTextAsync(screenShot);
        }
    }
}
