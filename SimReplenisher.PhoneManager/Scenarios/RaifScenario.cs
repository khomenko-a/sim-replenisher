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

        private static readonly Dictionary<Page, string> PAGES_XML_MARKERS = new()
        {
            { Page.HomeScreen, HOMESCREEN_PAGE_MARKER },
            { Page.PasswordInput, PASSWORD_PAGE_MARKER },
            { Page.Success, SUCCESS_PAGE_MARKER },
            { Page.AmountSelection, AMOUNT_PAGE_MARKER },
            { Page.TopUpCellPhone, TOP_UP_CELL_PHONE_PAGE_MARKER },
            { Page.Confirmation, CONFIRMATION_PAGE_MARKER },
            { Page.Processing, PROCESSING_PAGE_MARKER },
            { Page.PasswordReset, CHANGE_CURRENT_PASS_MARKER },
            { Page.TechnicalProblem, ERROR_MESSAGE_MARKER },
            { Page.UnableShowCards, UNABLE_SHOW_CARDS_MARKER }
        };

        private const string PASSWORD_PAGE_MARKER = "//node[@text=\"Введіть пароль\"]";
        private const string TOP_UP_CELL_PHONE_PAGE_MARKER = "//node[contains(@text, 'Отримувач')]";
        private const string CONFIRMATION_PAGE_MARKER = "//node[contains(@text, 'Оплатити')]";
        private const string SUCCESS_PAGE_MARKER = "//node[@text='Платіж прийнято']";
        private const string HOMESCREEN_PAGE_MARKER = "//node[@resource-id='com.ldmnq.launcher3:id/workspace']";
        private const string AMOUNT_PAGE_MARKER = "//node[@resource-id='uds_amount_input_amount']";
        private const string PROCESSING_PAGE_MARKER = "//node[@text='Обробка платежу']";
        private const string CHANGE_CURRENT_PASS_MARKER = "//node[@text='Змінити поточний пароль?']";
        private const string ERROR_MESSAGE_MARKER = "//node[@resource-id='uds_alert_message' and @text='Технічна помилка. Будь ласка, спробуйте пізніше.']";
        private const string UNABLE_SHOW_CARDS_MARKER = "//node[@text='Неможливо показати рахунки']";

        private const string APP_PACKAGE_NAME = "ua.raiffeisen.myraif";
        private const int PASSWORD_LENGTH = 4;
        private const int MAX_ATTEMPTS_OPEN_PAGE = 2;
        private const int MAX_ATTEMPTS_IDENTIFY_PAGE = 5;
        private const int MAX_ATTEMMPTS_GET_INCASE_OF_EMULATOR_LAG = 2;
        private const int MAX_ATTEMPTS_RETURN_TO_MAIN_SCREEN = 10;
        private const int AMOUNT_OF_RETRIES_FOR_TECHNICAL_ISSUE = 2;

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
            int technicalIssueAttempts = 0;

            var previousPage = Page.Default;
            int stuckOnPageCount = 0;

            while (totalAttempts < MAX_ATTEMPTS_IDENTIFY_PAGE)
            {
                if (technicalIssueAttempts >= AMOUNT_OF_RETRIES_FOR_TECHNICAL_ISSUE)
                {
                    _logger.LogWarning("Exceeded maximum attempts for technical issues. Aborting replenishment. {Number} is dead or bank is glitching", simToReplenish.SimData.PhoneNumber);
                    throw new PageLoadException(Page.TechnicalProblem, null);
                }

                var currentPage = await DetectCurrentPageAsync(device, previousPage);

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

                var delay = SHORT_DELAY;

                switch (currentPage)
                {
                    case Page.HomeScreen:
                        await OpenAppOnMainPageAsync(device);
                        delay = LONG_DELAY;
                        break;

                    case Page.PasswordInput:
                        await UnlockBankApp(device);
                        delay = LONG_DELAY;
                        break;

                    case Page.Main:
                        await OpenTopUpCellPhonePageAsync(device);
                        delay = MIDDLE_DELAY;
                        break;

                    case Page.TopUpCellPhone:
                        await OpenAmountPageAsync(device, simToReplenish);
                        delay = MIDDLE_DELAY;
                        break;

                    case Page.AmountSelection:
                        await OpenConfirmationReplenishmentPageAsync(device, simToReplenish);
                        delay = LONG_DELAY;
                        break;

                    case Page.Confirmation:
                        await OpenConfirmedPageAsync(device);
                        delay = LONG_DELAY;
                        break;

                    case Page.Processing:
                        _logger.LogInformation("Processing payment, waiting...");
                        delay = MIDDLE_DELAY;
                        break;

                    case Page.PasswordReset:
                        _logger.LogWarning("The app is glitching. Reset password window is open. Closing...");
                        await CloseResetPasswordWindowAsync(device);
                        break;

                    case Page.TechnicalProblem:
                        _logger.LogWarning("There is a technical problem. Maybe the number is dead.");
                        technicalIssueAttempts++;
                        break;

                    case Page.UnableShowCards:
                        _logger.LogCritical("The app is not working!");
                        await RestartApp(device);
                        break;

                    case Page.Success:
                        await FinishReplenishmentAsync(device);
                        return;
                }

                await Task.Delay(delay + 2000);
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

        private async Task<Page> DetectCurrentPageAsync(IPhoneDevice device, Page previousPage = Page.Unknown)
        {
            var isProperPageForCV = 
                   previousPage == Page.TopUpCellPhone
                || previousPage == Page.PasswordInput
                || previousPage == Page.HomeScreen
                || previousPage == Page.Default;

            for (int attempt = 0; attempt < MAX_ATTEMMPTS_GET_INCASE_OF_EMULATOR_LAG; attempt++) // in case of emulator lag try to recognize text a few times
            {
                var xml = await device.GetXmlDumpAsync();
                if (xml != null)
                {
                    foreach (var (page, xpath) in PAGES_XML_MARKERS)
                    {
                        if (xml.SelectSingleNode(xpath) != null)
                        {
                            return page;
                        }
                    }
                }

                if (isProperPageForCV)
                {
                    break;
                }
            }

            for (int attempt = 0; attempt < MAX_ATTEMMPTS_GET_INCASE_OF_EMULATOR_LAG; attempt++) // in case of emulator lag try to recognize text a few times
            {
                if (isProperPageForCV)
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
            await device.OpenBankAppAsync(APP_PACKAGE_NAME);

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

        private async Task CloseResetPasswordWindowAsync(IPhoneDevice device)
        {
            await device.TapAsync(300, 1130, 400, 1200);
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

        private async Task RestartApp(IPhoneDevice device)
        {
            await device.CloseBankAppAsync(APP_PACKAGE_NAME);
            await Task.Delay(MIDDLE_DELAY);
            await device.OpenBankAppAsync(APP_PACKAGE_NAME);
        }

        private async Task<string> TakeScreenShotAndRecognizeText(IPhoneDevice device)
        {
            var screenShot = await device.TakeScreenshotAsync();

            return await _textRecognizerService.RecognizeTextAsync(screenShot);
        }
    }
}
