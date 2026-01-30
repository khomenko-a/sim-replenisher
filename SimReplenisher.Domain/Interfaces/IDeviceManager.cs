using System;
using System.Collections.Generic;
using System.Text;

namespace SimReplenisher.Domain.Interfaces
{
    public interface IDeviceManager
    {
        IEnumerable<IPhoneDevice> GetConnectedDevices();
    }
}
