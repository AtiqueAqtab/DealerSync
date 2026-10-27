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

        // Enable PostgreSQL case-insensitive text
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.Entity<SkuMapping>(entity =>
        {
            entity.HasKey(mapping => mapping.Id);

            entity.Property(mapping => mapping.LightspeedPartNumber)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("citext");

            entity.Property(mapping => mapping.ShopifySku)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("citext");

            // Each Lightspeed part number maps to one Shopify SKU.
            entity.HasIndex(mapping => mapping.LightspeedPartNumber)
                .IsUnique();

            // Multiple Lightspeed part numbers may map to one Shopify SKU.
            entity.HasIndex(mapping => mapping.ShopifySku);
        });

        modelBuilder.Entity<ReconciliationRun>(entity =>
        {
            entity.HasKey(run => run.Id);
        });
    }
}