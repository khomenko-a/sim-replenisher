using System.ComponentModel.DataAnnotations.Schema;

namespace SimReplenisher.Domain.Entities
{
    [Table("SimDatas")]
    public class SimData
    {
        public int Id { get; set; }

        [Column("Number")]
        public string PhoneNumber { get; set; }
    }
}
