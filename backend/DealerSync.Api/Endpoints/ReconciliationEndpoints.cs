using CsvHelper;
using DealerSync.Api.DTOs;
using DealerSync.Infrastructure.Services;

namespace DealerSync.Api.Endpoints;

public static class ReconciliationEndpoints
{
    public static void MapReconciliationEndpoints(
        this WebApplication app)
    {
        var group = app.MapGroup("/api/reconciliation")
            .WithTags("Reconciliation");

        // Check whether the reconciliation service is available
        group.MapGet("/status",
            (ReconciliationService service) =>
            {
                return Results.Ok(new
                {
                    Status = "Ready",
                    Message = "Reconciliation service resolved successfully"
                });
            })
            .WithName("GetReconciliationStatus");

        // Upload inventory files and run reconciliation
        group.MapPost("/",
            (
                IFormFile lightspeedFile,
                IFormFile shopifyFile,
                ReconciliationService service) =>
            {
                const long maxFileSize = 10 * 1024 * 1024;

                // Validate that both files contain data
                if (lightspeedFile.Length == 0 ||
                    shopifyFile.Length == 0)
                {
                    return Results.BadRequest(new
                    {
                        Error = "Both CSV files must contain data."
                    });
                }

                // Validate file sizes
                if (lightspeedFile.Length > maxFileSize ||
                    shopifyFile.Length > maxFileSize)
                {
                    return Results.BadRequest(new
                    {
                        Error = "Each CSV file must be 10 MB or smaller."
                    });
                }

                // Validate file extensions
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
                    using var lightspeedStream =
                        lightspeedFile.OpenReadStream();

                    using var shopifyStream =
                        shopifyFile.OpenReadStream();

                    using var lightspeedReader =
                        new StreamReader(lightspeedStream);

                    using var shopifyReader =
                        new StreamReader(shopifyStream);

                    // Reuse existing reconciliation logic
                    var result = service.Run(
                        lightspeedReader,
                        shopifyReader);

                    // Convert the result to an API response DTO
                    var response =
                        ReconciliationSummaryResponse.FromResult(result);

                    return Results.Ok(response);
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
            .DisableAntiforgery();
    }
}