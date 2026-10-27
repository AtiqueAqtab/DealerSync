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
    bool RequiresReview,
    int? CombinedQuantity,
    List<InventoryContributorResponse> Contributors,
    List<string> ReviewReasons
)
{
    public static ReconciliationMatchResponse FromMatch(
        InventoryMatch match,
        InventoryUpdate? approvedUpdate = null)
    {
        var shopify = match.ShopifyItem
            ?? throw new InvalidOperationException(
                "A reconciliation match must contain a Shopify item.");

        var diagnosticService = new InventoryDiagnosticService();
        var status = diagnosticService.GetStatus(match);

        var contributors = match.Contributors
            .Select(contributor => new InventoryContributorResponse(
                contributor.LightspeedItem.PartNumber,
                contributor.LightspeedItem.Quantity,
                contributor.MatchType.ToString()))
            .ToList();

        return new ReconciliationMatchResponse(
            ShopifySku: shopify.Sku,
            ShopifyTitle: shopify.Title,
            Location: shopify.Location,
            LightspeedPartNumber: match.LightspeedItem?.PartNumber,
            MatchType: match.MatchType.ToString(),
            CurrentQuantity: shopify.CurrentQuantity,
            ProposedQuantity: approvedUpdate?.NewQuantity,
            HasInventoryDifference: match.HasInventoryDifference,
            DiagnosticStatus: status.ToString(),
            RequiresReview: diagnosticService.RequiresReview(match),
            CombinedQuantity: match.CombinedQuantity,
            Contributors: contributors,
            ReviewReasons: match.ReviewReasons
                .Select(reason => reason.ToString())
                .ToList()
        );
    }
}