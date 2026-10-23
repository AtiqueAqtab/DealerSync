using CsvHelper;
using DealerSync.Api.DTOs;
using DealerSync.Core.Models;
using DealerSync.Infrastructure.Services;
using DealerSync.Infrastructure.Persistence.Repositories;
using DealerSync.Infrastructure.Exporters;
using System.Text;

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
        group.MapPost("/", async (
                IFormFile lightspeedFile,
                IFormFile shopifyFile,
                ReconciliationService service,
                ReconciliationHistoryRepository historyRepository,
                SkuMappingRepository skuMappingRepository,
                ReconciliationDiagnosticsCsvExporter diagnosticsExporter,
                ShopifyInventoryCsvExporter shopifyExporter,
                bool detailed = false,
                string? format = null) =>
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

                    // Retrieve all supplier mappings, including unverified mappings.
                    var savedMappings =
                        await skuMappingRepository.GetAllAsync();

                    // Convert database entities into Core models.
                    var allMappings = savedMappings
                        .Select(mapping => new SkuMappingInfo(
                            mapping.LightspeedPartNumber,
                            mapping.ShopifySku,
                            mapping.IsVerified))
                        .ToList();

                    // Only verified mappings are trusted inventory contributors.
                    var verifiedMappings = allMappings
                        .Where(mapping => mapping.IsVerified)
                        .Select(mapping => new VerifiedSkuMapping(
                            mapping.LightspeedPartNumber,
                            mapping.ShopifySku))
                        .ToList();

                    // Run reconciliation with both trusted and pending mappings.
                    var result = service.Run(
                        lightspeedReader,
                        shopifyReader,
                        verifiedMappings,
                        allMappings);

                    // Save successful reconciliation results
                    await historyRepository.SaveAsync(result);

                    // Return downloadable CSV diagnostics
                    if (string.Equals(
                            format,
                            "csv",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        using var writer = new StringWriter();

                        diagnosticsExporter.Export(
                            writer,
                            result.Matches,
                            result.InventoryUpdates);

                        var csvBytes = Encoding.UTF8.GetBytes(writer.ToString());

                        return Results.File(
                            fileContents: csvBytes,
                            contentType: "text/csv; charset=utf-8",
                            fileDownloadName: "dealersync_diagnostics.csv");
                    }

                    // Return detailed JSON when requested
                    if (detailed)
                    {
                        var detailedResponse =
                            ReconciliationDetailedResponse.FromResult(result,
                                ApprovedInventoryExportResponse.FromResult(result, shopifyExporter));

                        return Results.Ok(detailedResponse);
                    }

                    // Default: return summary JSON
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
                catch (FormatException ex)
                {
                    return Results.BadRequest(new
                    {
                        Error = ex.Message
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

        /// Retrieve the 50 most recent reconciliation runs
        group.MapGet("/history",
                async (ReconciliationHistoryRepository repository) =>
                {
                    var runs = await repository.GetRecentAsync();

                    return Results.Ok(runs);
                })
            .WithName("GetReconciliationHistory");

        // Retrieve a specific reconciliation run by ID
        group.MapGet("/history/{id:guid}",
                async (
                    Guid id,
                    ReconciliationHistoryRepository repository) =>
                {
                    var run = await repository.GetByIdAsync(id);

                    if (run == null)
                    {
                        return Results.NotFound(new
                        {
                            Error = "Reconciliation run not found."
                        });
                    }

                    return Results.Ok(run);
                })
            .WithName("GetReconciliationRunById");
    }
}
