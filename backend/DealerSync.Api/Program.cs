using CsvHelper;
using DealerSync.Core.Services;
using DealerSync.Infrastructure.Importers;
using DealerSync.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

// Dependency injection
builder.Services.AddTransient<LightspeedCsvImporter>();
builder.Services.AddTransient<ShopifyCsvImporter>();
builder.Services.AddTransient<SkuMatchingService>();
builder.Services.AddTransient<ReconciliationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Health check
app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        Status = "Healthy",
        Service = "DealerSync API",
        Version = "0.2.0"
    });
})
.WithName("GetHealth")
.WithTags("Health");

// Dependency injection status
app.MapGet("/api/reconciliation/status",
    (ReconciliationService service) =>
    {
        return Results.Ok(new
        {
            Status = "Ready",
            Message = "Reconciliation service resolved successfully"
        });
    })
    .WithName("GetReconciliationStatus")
    .WithTags("Reconciliation");

// Upload and reconcile inventory CSV files
app.MapPost("/api/reconciliation",
    async (
        IFormFile lightspeedFile,
        IFormFile shopifyFile,
        ReconciliationService service) =>
    {
        const long maxFileSize = 10 * 1024 * 1024; // 10 MB

        // Validate uploaded files
        if (lightspeedFile.Length == 0 ||
            shopifyFile.Length == 0)
        {
            return Results.BadRequest(new
            {
                Error = "Both CSV files must contain data."
            });
        }

        if (lightspeedFile.Length > maxFileSize ||
            shopifyFile.Length > maxFileSize)
        {
            return Results.BadRequest(new
            {
                Error = "Each CSV file must be 10 MB or smaller."
            });
        }

        if (!Path.GetExtension(lightspeedFile.FileName)
                .Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetExtension(shopifyFile.FileName)
                .Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                Error = "Only CSV files are supported."
            });
        }

        try
        {
            // Open uploaded files as streams
            using var lightspeedStream = lightspeedFile.OpenReadStream();
            using var shopifyStream = shopifyFile.OpenReadStream();

            using var lightspeedReader =
                new StreamReader(lightspeedStream);

            using var shopifyReader =
                new StreamReader(shopifyStream);

            // Run existing reconciliation logic
            var result = service.Run(
                lightspeedReader,
                shopifyReader);

            // Return summary statistics
            return Results.Ok(new
            {
                result.LightspeedItemCount,
                result.ShopifyItemCount,
                result.ExactMatches,
                result.NormalizedMatches,
                result.Unmatched,
                result.InventoryDifferences,
                result.InventoryUpdatesGenerated,
                result.NegativeQuantitiesExcluded,
                result.ShopifyCoverage
            });
        }
        catch (CsvHelperException)
        {
            return Results.BadRequest(new
            {
                Error = "The uploaded CSV files contain invalid CSV data or headers."
            });
        }
        catch (InvalidOperationException)
        {
            return Results.BadRequest(new
            {
                Error = "Inventory reconciliation failed due to conflicting data."
            });
        }
    })
    .WithName("ReconcileInventory")
    .WithTags("Reconciliation")
    .DisableAntiforgery();

app.Run();

public partial class Program { }