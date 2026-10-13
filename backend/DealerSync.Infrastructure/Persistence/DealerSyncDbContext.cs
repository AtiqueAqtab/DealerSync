using DealerSync.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DealerSync.Infrastructure.Persistence;

public class DealerSyncDbContext : DbContext
{
    public DealerSyncDbContext(
        DbContextOptions<DealerSyncDbContext> options)
        : base(options)
    {
    }

    public DbSet<SkuMapping> SkuMappings => Set<SkuMapping>();

    public DbSet<ReconciliationRun> ReconciliationRuns =>
        Set<ReconciliationRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SkuMapping>(entity =>
        {
            entity.HasKey(mapping => mapping.Id);

            entity.Property(mapping => mapping.LightspeedPartNumber)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(mapping => mapping.ShopifySku)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(mapping => mapping.LightspeedPartNumber);

            entity.HasIndex(mapping => mapping.ShopifySku);
        });

        modelBuilder.Entity<ReconciliationRun>(entity =>
        {
            entity.HasKey(run => run.Id);
        });
    }
}