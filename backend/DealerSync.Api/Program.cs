using DealerSync.Api.Endpoints;
using DealerSync.Core.Services;
using DealerSync.Infrastructure.Importers;
using DealerSync.Infrastructure.Exporters;
using DealerSync.Infrastructure.Persistence;
using DealerSync.Infrastructure.Persistence.Repositories;
using DealerSync.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Configure PostgreSQL database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Database connection string 'DefaultConnection' is missing.");

builder.Services.AddDbContext<DealerSyncDbContext>(options =>
    options.UseNpgsql(connectionString));

// OpenAPI documentation
builder.Services.AddOpenApi();

// Register dependencies
builder.Services.AddTransient<LightspeedCsvImporter>();
builder.Services.AddTransient<ShopifyCsvImporter>();
builder.Services.AddTransient<SkuMatchingService>();
builder.Services.AddTransient<ReconciliationService>();
builder.Services.AddScoped<ReconciliationHistoryRepository>();
builder.Services.AddScoped<SkuMappingRepository>();
builder.Services.AddTransient<ReconciliationDiagnosticsCsvExporter>();
builder.Services.AddTransient<ShopifyInventoryCsvExporter>();

var app = builder.Build();

// Configure HTTP pipeline
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseHttpsRedirection();

// Database outages must never expose driver details or produce partial approvals.
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception error) when (!context.Response.HasStarted &&
        (error is NpgsqlException || error is DbUpdateException {InnerException: NpgsqlException} ||
         error is InvalidOperationException {InnerException: NpgsqlException}))
    {
        app.Logger.LogWarning("Database operation failed; no result was returned.");
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Database unavailable. Check the local database connection and migrations, then try again."
        });
    }
});

// Register API endpoints
app.MapHealthEndpoints();
app.MapReconciliationEndpoints();
app.MapSkuMappingEndpoints();

app.Run();

// Expose Program for integration testing
public partial class Program
{
}
