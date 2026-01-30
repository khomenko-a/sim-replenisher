using SharpAdbClient;
using SimReplenisher.Domain.Interfaces;

namespace SimReplenisher.PhoneManager
{
    public class DeviceManager : IDeviceManager
    {
        private readonly IAdbClient _adbClient;

        public DeviceManager(IAdbClient adbClient)
        {
            _adbClient = adbClient;
        }

        public IEnumerable<IPhoneDevice> GetConnectedDevices()
        {
            var devices = _adbClient.GetDevices();

            foreach (var device in devices)
            {
                yield return new PhoneDevice(_adbClient, device);
            }
        }
    }
}
