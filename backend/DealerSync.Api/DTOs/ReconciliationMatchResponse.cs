using DealerSync.Core.Enums;
using DealerSync.Core.Models;
using DealerSync.Core.Services;

namespace DealerSync.Api.DTOs;

public record ReconciliationMatchResponse(
    string ShopifySku,
    string ShopifyTitle,
    string Location,
    string? LightspeedPartNumber,
    string MatchType,
    int CurrentQuantity,
    int? ProposedQuantity,
    bool HasInventoryDifference,
    string DiagnosticStatus,
    bool RequiresReview
)
{
    public static ReconciliationMatchResponse FromMatch(
        InventoryMatch match)
    {
        var shopify = match.ShopifyItem
                      ?? throw new InvalidOperationException(
                          "A reconciliation match must contain a Shopify item.");

        var lightspeed = match.LightspeedItem;

        // Unmatched items require manual review.
        // Negative source quantities must not be exported.
        var diagnosticService = new InventoryDiagnosticService();

        var status = diagnosticService.GetStatus(match);
        var requiresReview = diagnosticService.RequiresReview(match);

        return new ReconciliationMatchResponse(
            ShopifySku: shopify.Sku,
            ShopifyTitle: shopify.Title,
            Location: shopify.Location,
            LightspeedPartNumber: lightspeed?.PartNumber,
            MatchType: match.MatchType.ToString(),
            CurrentQuantity: shopify.CurrentQuantity,
            ProposedQuantity:
            lightspeed != null && lightspeed.Quantity >= 0
                ? lightspeed.Quantity
                : null,
            HasInventoryDifference: match.HasInventoryDifference,
            DiagnosticStatus: status.ToString(),
            RequiresReview: requiresReview
        );
    }
}