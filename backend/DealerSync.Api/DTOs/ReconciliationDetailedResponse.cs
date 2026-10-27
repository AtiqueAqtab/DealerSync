using DealerSync.Core.Models;

namespace DealerSync.Api.DTOs;

public record ReconciliationDetailedResponse(
    ReconciliationSummaryResponse Summary,
    List<ReconciliationMatchResponse> Matches,
    ApprovedInventoryExportResponse ApprovedInventoryExport
)
{
    public static ReconciliationDetailedResponse FromResult(
        ReconciliationResult result,
        ApprovedInventoryExportResponse approvedInventoryExport)
    {
        // Index approved updates by Shopify SKU and location.
        var approvedUpdates = result.InventoryUpdates
            .ToDictionary(update => (
                update.Sku.ToUpperInvariant(),
                update.Location.ToUpperInvariant()
            ));

        var matches = result.Matches
            .Select(match =>
            {
                var shopify = match.ShopifyItem
                              ?? throw new InvalidOperationException(
                                  "A reconciliation match must contain a Shopify item.");

                // Find an actual approved update for this product.
                approvedUpdates.TryGetValue(
                    (
                        shopify.Sku.ToUpperInvariant(),
                        shopify.Location.ToUpperInvariant()
                    ),
                    out var approvedUpdate);

                return ReconciliationMatchResponse.FromMatch(
                    match,
                    approvedUpdate);
            })
            .ToList();

        return new ReconciliationDetailedResponse(
            Summary: ReconciliationSummaryResponse.FromResult(result),
            Matches: matches,
            ApprovedInventoryExport: approvedInventoryExport
        );
    }
}
