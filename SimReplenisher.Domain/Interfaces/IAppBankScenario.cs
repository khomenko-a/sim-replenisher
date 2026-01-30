using SimReplenisher.Domain.Entities;
using SimReplenisher.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimReplenisher.Domain.Interfaces
{
    public interface IAppBankScenario
    {
        AppType Bank { get; }
        Task ReplenishNumber(IPhoneDevice device, SimToReplenish simToReplenish);
    }
}
