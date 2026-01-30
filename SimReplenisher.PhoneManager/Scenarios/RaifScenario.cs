using Microsoft.Extensions.Logging;
using SimReplenisher.Domain.Entities;
using SimReplenisher.Domain.Enums;
using SimReplenisher.Domain.Exceptions;
using SimReplenisher.Domain.Interfaces;

namespace SimReplenisher.PhoneManager.Scenarios
{
    public class RaifScenario : IAppBankScenario
    {
        private readonly ITextRecognizerService _textRecognizerService;
        private readonly ILogger _logger;

        private Page _supposedCurrentPage;

        private static readonly string[] _mainPageMarkers =
        {
            "Усі рахунки",
            "Головна",
            "Депозити",
            "Усі платежі"
        };
        private static readonly string[] _amountPageMarkers =
        {
            "Немає комісії",
            "0.00",
            "З картки"
        };

        private const string APP_PACKAGE_NAME = "ua.raiffeisen.myraif";
        private const int PASSWORD_LENGTH = 4;
        private const int MAX_ATTEMPTS_GET_XMLDUMP = 15;
        private const int MAX_ATTEMPTS_RECOGNIZE_TEXT = 3;
        private const int MAX_ATTEMPTS_OPEN_PAGE = 2;

        private const int SHORT_DELAY = 1500;
        private const int MIDDLE_DELAY = 4000;
        private const int LONG_DELAY = 7000;

        public RaifScenario(ITextRecognizerService textRecognizerService, ILogger<RaifScenario> logger)
        {
            _textRecognizerService = textRecognizerService;
            _logger = logger;
        }

        public AppType Bank => AppType.RaiffaisenBank;

        public async Task ReplenishNumber(IPhoneDevice device, SimToReplenish simToReplenish) //add custom exceptions and logging
        {
            for (int attempt = 0; attempt < MAX_ATTEMPTS_OPEN_PAGE; attempt++)
            {
                var isMainPageOpened = await OpenAppOnMainPageAsync(device);

                if (isMainPageOpened)
                {
                    break;
                }

                await device.CloseBankApp(APP_PACKAGE_NAME);
                await Task.Delay(3000);

                if (attempt == MAX_ATTEMPTS_OPEN_PAGE - 1)
                {
                    var dump = await device.GetXmlDumpAsync();

                    throw new PageLoadException(Page.Main, dump);
                }
                //await RestartPhoneAsync(device); EMULATOR CAN'T BE RESTARTED VIA ADB
            }

            var isTopUpCellPhonePageOpened = await OpenTopUpCellPhonePageAsync(device);
            if (!isTopUpCellPhonePageOpened)
            {
                var dump = await device.GetXmlDumpAsync();

                throw new PageLoadException(Page.TopUpCellPhone, dump);
            }

            var isAmountPageOpened = await OpenAmountPageAsync(device, simToReplenish);
            if (!isAmountPageOpened)
            {
                var dump = await device.GetXmlDumpAsync();

                throw new PageLoadException(Page.AmountSelection, dump);
            }

            var isConfirmationPageOpened = await OpenConfirmationReplenishment(device, simToReplenish);
            if (!isConfirmationPageOpened)
            {
                var dump = await device.GetXmlDumpAsync();

                throw new PageLoadException(Page.Confirmation, dump);
            }
            
            var isConfirmedPageOpened = await OpenConfirmedPage(device);
            if (!isConfirmedPageOpened)
            {
                var dump = await device.GetXmlDumpAsync();

                throw new PageLoadException(Page.Success, dump);
            }
        }

        /*private async Task RestartPhoneAsync(IPhoneDevice device) EMULATOR CAN'T BE RESTARTED VIA ADB
        {
            throw new NotImplementedException();
        }*/

        private async Task<bool> OpenAppOnMainPageAsync(IPhoneDevice device) //add logging
        {
            var xmLDump = await device.GetXmlDumpAsync();

            if (xmLDump != null)
            {
                _logger.LogInformation("Going back to the home screen");
                await device.GoToHomeScreenAsync();

                await Task.Delay(SHORT_DELAY);

                _logger.LogInformation("Opening bank app");
                await device.OpenBankApp(APP_PACKAGE_NAME);

                await Task.Delay(LONG_DELAY);

                for (int attempt = 0; attempt < MAX_ATTEMPTS_GET_XMLDUMP; attempt++)
                {
                    xmLDump = await device.GetXmlDumpAsync();
                    var appNode = xmLDump?.SelectSingleNode("//node[@text=\"Введіть пароль\"]");

                    if (appNode != null)
                    {
                        await UnlockBankApp(device);

                        await Task.Delay(MIDDLE_DELAY);

                        break;
                    }

                    _logger.LogInformation("Didn't find password page. Trying again");
                    await Task.Delay(MIDDLE_DELAY);
                }
            }

            await Task.Delay(5000);

            for (int attempt = 0; attempt < MAX_ATTEMPTS_RECOGNIZE_TEXT; attempt++)
            {
                _logger.LogInformation("Using CV to get text on the screen.");
                var textOnScreen = await TakeScreenShotAndRecognizeText(device);

                var res = _mainPageMarkers.Any(marker =>
                textOnScreen.Contains(marker, StringComparison.OrdinalIgnoreCase));

                if (res)
                {
                    _logger.LogInformation("Main page is open");
                    return res;
                }

                await Task.Delay(2000);
            }

            _logger.LogWarning("Couldnt open main page");

            return false;
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

        private async Task<bool> OpenTopUpCellPhonePageAsync(IPhoneDevice device)
        {
            _logger.LogInformation("Clicking Top up cell phone");
            await device.TapAsync(620, 1270, 720, 1330); // tap Top up cell phone
            await Task.Delay(MIDDLE_DELAY);

            var xmLDump = await device.GetXmlDumpAsync();
            var appNode = xmLDump?.SelectSingleNode("//node[@text='Поповнити мобільний']");

            if (xmLDump is null)
            {
                await Task.Delay(SHORT_DELAY);

                xmLDump = await device.GetXmlDumpAsync();
                appNode = xmLDump?.SelectSingleNode("//node[@text='Поповнити мобільний']");
            }

            if (appNode != null)
            {
                _logger.LogInformation("TopUp cellphone page is open");
                return true;
            }

            _logger.LogWarning("Couldnt open TopUp cellphone page");

            return false;
        }

        private async Task<bool> OpenAmountPageAsync(IPhoneDevice device, SimToReplenish simToReplenish)
        {
            _logger.LogInformation("Clicking phone number field");
            await device.TapAsync(48, 438, 1032, 630); // tap phone number field

            await Task.Delay(MIDDLE_DELAY);

            await device.InputTextAsync(simToReplenish.SimData.PhoneNumber);

            await Task.Delay(SHORT_DELAY);

            _logger.LogInformation("Clicking Top Up button");
            await device.TapAsync(48, 1728, 1032, 1872); // tap Top Up

            await Task.Delay(MIDDLE_DELAY);

            var xmLDump = await device.GetXmlDumpAsync();

            if (xmLDump != null)
            {
                return false;
            }

            for (int attempt = 0; attempt < MAX_ATTEMPTS_RECOGNIZE_TEXT; attempt++)
            {
                _logger.LogInformation("Using CV to get text on the screen.");
                var textOnScreen = await TakeScreenShotAndRecognizeText(device);

                var res = _amountPageMarkers.Any(marker =>
                textOnScreen.Contains(marker, StringComparison.OrdinalIgnoreCase));

                if (res)
                {
                    _logger.LogInformation("Amount selection page is open");
                    return res;
                }

                await Task.Delay(SHORT_DELAY);
            }

            _logger.LogWarning("Couldnt open Amount selection page");

            return false;
        }

        private async Task<bool> OpenConfirmationReplenishment(IPhoneDevice device, SimToReplenish simToReplenish)
        {
            await device.InputTextAsync(simToReplenish.Amount.ToString());

            await Task.Delay(SHORT_DELAY);

            _logger.LogInformation("Clicking Continue button");
            await device.TapAsync(48, 1728, 1032, 1872); // tap Continue

            await Task.Delay(MIDDLE_DELAY);

            var xmLDump = await device.GetXmlDumpAsync();

            if (xmLDump is null) // change to cycle WHERE xml shuold be 100% w/ MAX_ATTEMPTS_GET_XMLDUMP !!!!!!!!!!!!!!!!!!!!!!
            {
                await Task.Delay(SHORT_DELAY);

                xmLDump = await device.GetXmlDumpAsync();
            }

            var appNode = xmLDump?.SelectSingleNode("//node[@resource-id='uds_amount_input_amount']");

            var errorNode = xmLDump?.SelectSingleNode("//node[@resource-id='uds_alert_message']");

            if (appNode != null || errorNode != null)
            {
                await Task.Delay(MIDDLE_DELAY);

                for (int i = 0; i < 2; i++) // go back to the main page (just double click go back button)
                {
                    await device.TapAsync(84, 156, 90, 170);
                    _logger.LogInformation("{Iteration} click back", i + 1);
                    await Task.Delay(MIDDLE_DELAY);
                }

                return false;
            }

            await Task.Delay(MIDDLE_DELAY);

            xmLDump = await device.GetXmlDumpAsync();
            appNode = xmLDump?.SelectSingleNode("//node[contains(@text, 'Оплатити')]");

            if (appNode == null)
            {
                _logger.LogWarning("Couldnt open Confirmation page");
                return false;
            }

            _logger.LogInformation("Confirmation page is open");

            return true;
        }

        private async Task<bool> OpenConfirmedPage(IPhoneDevice device)
        {
            _logger.LogInformation("Clicking Pay button");
            await device.TapAsync(48, 1728, 1032, 1872); // tap Pay
            await Task.Delay(LONG_DELAY);

            for (int attempt = 0; attempt < MAX_ATTEMPTS_GET_XMLDUMP; attempt++)
            {
                var xmLDump = await device.GetXmlDumpAsync();
                var appNode = xmLDump?.SelectSingleNode("//node[@text='Платіж прийнято']");

                if (appNode != null)
                {
                    _logger.LogInformation("DONE!");
                    await device.TapAsync(48, 1728, 1032, 1872); // tap Done

                    await Task.Delay(MIDDLE_DELAY);

                    return true;
                }

                _logger.LogInformation("Payment still processing.");
                await Task.Delay(SHORT_DELAY);
            }

            _logger.LogWarning("Couldnt confirm payment");

            return false;
        }

        private async Task<string> TakeScreenShotAndRecognizeText(IPhoneDevice device)
        {
            var screenShot = await device.TakeScreenshotAsync();

            return await _textRecognizerService.RecognizeTextAsync(screenShot);
        }
    }
}
