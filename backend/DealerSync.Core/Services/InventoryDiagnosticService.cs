using DealerSync.Core.Enums;
using DealerSync.Core.Models;

namespace DealerSync.Core.Services;

public class InventoryDiagnosticService
{
    public DiagnosticStatus GetStatus(InventoryMatch match)
    {
        // An invalid quantity from any supplier requires review.
        if (match.Contributors.Any(contributor =>
                contributor.LightspeedItem.Quantity < 0) ||
            (match.Contributors.Count == 0 &&
             match.LightspeedItem is not null &&
             match.LightspeedItem.Quantity < 0))
        {
            return DiagnosticStatus.InvalidQuantity;
        }

        // A known unverified or missing supplier blocks
        // the entire Shopify product from automatic updates.
        if (match.ReviewReasons.Count > 0)
        {
            return DiagnosticStatus.ReviewRecommended;
        }

        // An aggregate quantity outside the supported integer
        // range cannot be safely exported.
        if ((match.Contributors.Count > 0 ||
             match.LightspeedItem is not null) &&
            match.CombinedQuantity is null)
        {
            return DiagnosticStatus.InvalidQuantity;
        }

        // Preserve existing matching diagnostics.
        return match.MatchType switch
        {
            SkuMatchType.Unmatched =>
                DiagnosticStatus.MappingRequired,

            SkuMatchType.Normalized =>
                DiagnosticStatus.ReviewRecommended,

            SkuMatchType.Exact or SkuMatchType.Verified =>
                DiagnosticStatus.Ready,

            _ => throw new ArgumentOutOfRangeException(
                nameof(match.MatchType))
        };
    }

    public bool RequiresReview(InventoryMatch match)
    {
        return GetStatus(match) != DiagnosticStatus.Ready;
    }
}