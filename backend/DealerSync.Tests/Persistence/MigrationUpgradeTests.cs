using DealerSync.Infrastructure.Persistence;
using DealerSync.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DealerSync.Tests.Persistence;

public class MigrationUpgradeTests
{
    // Verified against tag v0.2.0 (3fdebe75): these four migrations are unchanged in v0.3.
    private const string V02 = "20261013024015_AddNormalizedUpdatesExcluded";
    private const string MultiSupplier = "20261017023848_AllowMultipleSuppliersPerShopifySku";

    [Fact]
    public async Task PopulatedV02Database_UpgradesWithoutLosingMappingsOrIntegrity()
    {
        var database = "dealersync_upgrade_" + Guid.NewGuid().ToString("N");
        await using var postgres = new PostgreSqlBuilder("postgres:17")
            .WithDatabase(database)
            .WithUsername("upgrade_test")
            .WithPassword(Guid.NewGuid().ToString("N"))
            .WithLabel("com.dealersync.upgrade-test", database)
            .WithTmpfsMount("/var/lib/postgresql/data")
            .WithCreateParameterModifier(parameters =>
            {
                var bindings = parameters.HostConfig?.PortBindings
                    ?? throw new InvalidOperationException("Test database port bindings are missing.");
                foreach (var binding in bindings.Values.SelectMany(value => value))
                    binding.HostIP = "127.0.0.1";
            })
            .Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<DealerSyncDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using (var context = new DealerSyncDbContext(options))
        {
            Assert.Equal(database, await context.Database.SqlQueryRaw<string>(
                "SELECT current_database() AS \"Value\"").SingleAsync());
            await context.GetService<IMigrator>().MigrateAsync(V02);
            Assert.Equal(V02, (await context.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(new[] {MultiSupplier}, await context.Database.GetPendingMigrationsAsync());
        }

        var timestamp = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var originals = new[]
        {
            new SkuMapping {Id = Guid.NewGuid(), LightspeedPartNumber = "V02-Part-A", ShopifySku = "V02-Jacket",
                IsVerified = true, CreatedAt = timestamp, UpdatedAt = timestamp.AddDays(1)},
            new SkuMapping {Id = Guid.NewGuid(), LightspeedPartNumber = "V02-Part-B", ShopifySku = "V02-Helmet",
                IsVerified = false, CreatedAt = timestamp.AddHours(1), UpdatedAt = timestamp.AddDays(2)}
        };
        await using (var context = new DealerSyncDbContext(options))
        {
            context.SkuMappings.AddRange(originals);
            await context.SaveChangesAsync();
        }
        // The old schema really has the unique Shopify constraint; no hand-written schema substitutes.
        await AssertRejected(options, new SkuMapping {LightspeedPartNumber = "OLD-OTHER", ShopifySku = "v02-jacket"},
            PostgresErrorCodes.UniqueViolation);

        await using (var context = new DealerSyncDbContext(options))
        {
            await context.Database.MigrateAsync(); // Same EF migration mechanism as `dotnet ef database update`.
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            Assert.Equal(MultiSupplier, (await context.Database.GetAppliedMigrationsAsync()).Last());
            var restored = await context.SkuMappings.AsNoTracking().ToListAsync();
            Assert.Equal(originals.Length, restored.Count);
            foreach (var original in originals)
            {
                var saved = Assert.Single(restored, mapping => mapping.Id == original.Id);
                Assert.Equal(original.LightspeedPartNumber, saved.LightspeedPartNumber);
                Assert.Equal(original.ShopifySku, saved.ShopifySku);
                Assert.Equal(original.IsVerified, saved.IsVerified);
                Assert.Equal(original.CreatedAt, saved.CreatedAt);
                Assert.Equal(original.UpdatedAt, saved.UpdatedAt);
            }
            context.SkuMappings.Add(new SkuMapping {LightspeedPartNumber = "NEW-SUPPLIER", ShopifySku = "v02-jacket"});
            await context.SaveChangesAsync();
            Assert.Equal(2, await context.SkuMappings.CountAsync(mapping => mapping.ShopifySku == "V02-Jacket"));
        }
        await AssertRejected(options, new SkuMapping {LightspeedPartNumber = "v02-part-a", ShopifySku = "OTHER-SKU"},
            PostgresErrorCodes.UniqueViolation);
        await AssertRejected(options, new SkuMapping {Id = originals[0].Id, LightspeedPartNumber = "OTHER-PART", ShopifySku = "OTHER-SKU"},
            PostgresErrorCodes.UniqueViolation);
        await AssertRejected(options, new SkuMapping {LightspeedPartNumber = null!, ShopifySku = "OTHER-SKU"},
            PostgresErrorCodes.NotNullViolation);
        await AssertRejected(options, new SkuMapping {LightspeedPartNumber = "OTHER-PART", ShopifySku = null!},
            PostgresErrorCodes.NotNullViolation);
        await using (var context = new DealerSyncDbContext(options))
            Assert.Equal(3, await context.SkuMappings.CountAsync());
        // await using removes only this test's container/tmpfs, including on assertion failure.
    }

    private static async Task AssertRejected(DbContextOptions<DealerSyncDbContext> options, SkuMapping mapping, string sqlState)
    {
        await using var context = new DealerSyncDbContext(options);
        context.SkuMappings.Add(mapping);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(sqlState, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
}
