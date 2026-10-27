using DealerSync.Core.Models;
using DealerSync.Infrastructure.Exporters;

namespace DealerSync.Api.DTOs;

public record ApprovedInventoryExportResponse(string? Csv, string? Error, int UpdateCount)
{
    public static ApprovedInventoryExportResponse FromResult(
        ReconciliationResult result, ShopifyInventoryCsvExporter exporter)
    {
        if (result.InventoryUpdates.Count == 0) return new(null, null, 0);

        try
        {
            var csv = exporter.ExportToString(result.InventoryUpdates,
                result.Matches.Select(match => match.ShopifyItem
                    ?? throw new InvalidOperationException("A Shopify source record is missing.")));
            return new(csv, null, result.InventoryUpdates.Count);
        }
        catch (InvalidOperationException exception)
        {
            // Preserve diagnostics and approvals, but never advertise a partial export.
            return new(null, $"Shopify export blocked: {exception.Message}", result.InventoryUpdates.Count);
        }
    }
}
