using SimReplenisher.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SimReplenisher.Domain.Entities
{
    public class SimToReplenish
    {
        public int Id { get; set; }

        [Required]
        public int SimDataId { get; set; }
        public SimStatus Status { get; set; }
        public AppType Bank { get; set; }
        public PhoneProvider? Provider { get; set; }
        public int? Amount { get; set; }
        public DateTime AddingDate { get; set; }
        public DateTime? ExecutionDate { get; set; }

        [ForeignKey(nameof(SimDataId))]
        public SimData SimData { get; set; }

        public void PrepareForExecution()
        {
            if(SimData == null)
            {
                throw new InvalidDataException("SimData is null.");
            }

            Provider = GetPhoneProvider();
            Amount = GetAmount();
        }

        private PhoneProvider GetPhoneProvider()
        {
            if (string.IsNullOrEmpty(SimData.PhoneNumber))
            {
                throw new InvalidOperationException("Phone number is null or empty.");
            }

            if (Provider != null)
            {
                return Provider.Value;
            }

            var clearNumber = SimData.PhoneNumber.Trim('+');

            return clearNumber switch
                {
                var n when n.StartsWith("38067") || n.StartsWith("38068") || n.StartsWith("38077") || n.StartsWith("38096") || n.StartsWith("38097") || n.StartsWith("38098") => PhoneProvider.Kyivstar,
                var n when n.StartsWith("38063") || n.StartsWith("38093") || n.StartsWith("38073") => PhoneProvider.Lifecell,
                var n when n.StartsWith("38050") || n.StartsWith("38066") || n.StartsWith("38095") || n.StartsWith("38099") => PhoneProvider.Vodafone,
                _ => throw new InvalidOperationException("Unknown phone provider based on the phone number prefix.")
            };
        }

        private int GetAmount()
        {
            if (Amount != null)
            {
                return Amount.Value;
            }

            return Provider switch
            {
                PhoneProvider.Kyivstar => 1,
                PhoneProvider.Lifecell => 10,
                PhoneProvider.Vodafone => 5,
                _ => throw new InvalidOperationException("Unknown phone provider."),
            };
        }
    }
}
