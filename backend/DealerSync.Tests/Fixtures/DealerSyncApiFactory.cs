using DealerSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace DealerSync.Tests.Fixtures;

public class DealerSyncApiFactory :
    WebApplicationFactory<Program>,
    IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17")
            .WithDatabase("dealersync_test")
            .WithUsername("test")
            .WithPassword("test_password")
            .Build();

    public async Task InitializeAsync()
    {
        // Start an isolated PostgreSQL container
        await _postgres.StartAsync();

        // Create the API's service provider
        using var scope = Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<DealerSyncDbContext>();

        // Apply EF Core migrations to the test database
        await context.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the development database configuration
            services.RemoveAll<
                DbContextOptions<DealerSyncDbContext>>();

            // Register the temporary PostgreSQL database
            services.AddDbContext<DealerSyncDbContext>(
                options =>
                {
                    options.UseNpgsql(
                        _postgres.GetConnectionString());
                });
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Dispose of the test server
        Dispose();

        // Stop and remove the temporary database container
        await _postgres.DisposeAsync();
    }
}