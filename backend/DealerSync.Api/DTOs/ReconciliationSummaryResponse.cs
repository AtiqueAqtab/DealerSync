using DealerSync.Core.Models;

namespace DealerSync.Api.DTOs;

public record ReconciliationSummaryResponse(
    int LightspeedItemCount,
    int ShopifyItemCount,
    int ExactMatches,
    int VerifiedMatches,
    int NormalizedMatches,
    int Unmatched,
    int InventoryDifferences,
    int InventoryUpdatesGenerated,
    int NegativeQuantitiesExcluded,
    double ShopifyCoverage
)
{
    public static ReconciliationSummaryResponse FromResult(
        ReconciliationResult result)
    {
        return new ReconciliationSummaryResponse(
            result.LightspeedItemCount,
            result.ShopifyItemCount,
            result.ExactMatches,
            result.VerifiedMatches,
            result.NormalizedMatches,
            result.Unmatched,
            result.InventoryDifferences,
            result.InventoryUpdatesGenerated,
            result.NegativeQuantitiesExcluded,
            result.ShopifyCoverage
        );
    }
}