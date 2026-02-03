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

            modelBuilder.Entity<SimToReplenish>()
                .Property(s => s.AddingDate)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .ValueGeneratedOnAdd();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries<SimToReplenish>()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Property(p => p.Status).IsModified)
                {
                    if (entry.Entity.Status == SimStatus.Success || entry.Entity.Status == SimStatus.Failure)
                    {
                        entry.Entity.ExecutionDate = DateTime.UtcNow.AddHours(2);
                    }
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
