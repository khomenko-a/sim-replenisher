using SharpAdbClient;
using SimReplenisher.Domain.Interfaces;
using System.Diagnostics;
using System.Text;
using System.Xml;

namespace SimReplenisher.PhoneManager
{
    public class PhoneDevice : IPhoneDevice
    {
        private readonly IAdbClient _adbClient;
        private readonly DeviceData _deviceData;

        public PhoneDevice(IAdbClient adbClient, DeviceData deviceData)
        {
            _adbClient = adbClient;
            _deviceData = deviceData;
        }

        public async Task ExecuteAdbShellCommandAsync(string command)
        {
            var receiver = new ConsoleOutputReceiver();
            await _adbClient.ExecuteRemoteCommandAsync(command, _deviceData, receiver, Encoding.UTF8, CancellationToken.None);
        }

        public async Task<XmlDocument?> GetXmlDumpAsync()
        {
            var receiver = new ConsoleOutputReceiver();

            await _adbClient.ExecuteRemoteCommandAsync("uiautomator dump /sdcard/window_dump.xml", _deviceData, receiver, Encoding.UTF8, CancellationToken.None);

            var dumpOutput = receiver.ToString();

            if (dumpOutput.Contains("ERROR") || !dumpOutput.Contains("dumped to"))
            {
                return null;
            }

            var readReceiver = new ConsoleOutputReceiver();
            await _adbClient.ExecuteRemoteCommandAsync("cat /sdcard/window_dump.xml", _deviceData, readReceiver, Encoding.UTF8, CancellationToken.None);

            var xmlDump = readReceiver.ToString().Trim();

            if (string.IsNullOrEmpty(xmlDump) || !xmlDump.StartsWith("<"))
            {
                return null;
            }

            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlDump);

                return xmlDoc;
            }
            catch (XmlException)
            {
                return null;
            }
        }

        public async Task InputTextAsync(string text)
        {
            await ExecuteAdbShellCommandAsync($"input text {text}");
        }

        public async Task GoToHomeScreenAsync()
        {
            await ExecuteAdbShellCommandAsync("input keyevent 3");
        }

        public async Task OpenBankApp(string bankApp)
        {
            await ExecuteAdbShellCommandAsync($"monkey -p {bankApp} -c android.intent.category.LAUNCHER 1");
        }

        public async Task CloseBankApp(string bankApp)
        {
            await ExecuteAdbShellCommandAsync($"am force-stop {bankApp}");
        }

        public async Task TapAsync(int x1, int y1, int x2, int y2)
        {
            var rnd = new Random();
            var x = rnd.Next(x1, x2);
            var y = rnd.Next(y1, y2);

            await ExecuteAdbShellCommandAsync($"input tap {x} {y}");
        }

        public async Task<byte[]> TakeScreenshotAsync()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = "exec-out screencap -p",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.Environment["ANDROID_SERIAL"] = _deviceData.Serial;

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                using (var ms = new MemoryStream())
                {
                    await process.StandardOutput.BaseStream.CopyToAsync(ms);
                    await process.WaitForExitAsync();
                    return ms.ToArray();
                }
            }
        }
    }
}
