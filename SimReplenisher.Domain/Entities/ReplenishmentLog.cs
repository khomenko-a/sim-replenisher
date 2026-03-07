using System.ComponentModel.DataAnnotations.Schema;

namespace SimReplenisher.Domain.Entities
{
    public class ReplenishmentLog
    {
        public int Id { get; set; }
        public int SimDataId { get; set; }
        public string PhoneNumber { get; set; }
        public int Amount { get; set; }
        public bool Status { get; set; }
        public DateTime AddingDate { get; set; }
        public DateTime? ExecutionDate { get; set; }
        public string? Message { get; set; }

        [ForeignKey(nameof(SimDataId))]
        public SimData SimData { get; set; }
    }
}
