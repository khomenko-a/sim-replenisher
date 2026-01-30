using Microsoft.EntityFrameworkCore;
using SimReplenisher.Domain.Entities;
using SimReplenisher.Domain.Enums;

namespace SimReplenisher.DataManager
{
    public class SimDbContext : DbContext
    {
        public DbSet<SimToReplenish> ReplenishmentRequests { get; set; }
        public DbSet<SimData> SimDatas { get; set; }

        public SimDbContext(DbContextOptions<SimDbContext> dbContextOptions) : base(dbContextOptions) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SimToReplenish>()
                .Property(s => s.Status)
                .HasDefaultValue(SimStatus.New);

            modelBuilder.Entity<SimToReplenish>()
                .Property(s => s.Bank)
                .HasDefaultValue(AppType.RaiffaisenBank);
        }
    }
}
