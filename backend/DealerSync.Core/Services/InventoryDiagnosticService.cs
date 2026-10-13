using DealerSync.Core.Enums;
using DealerSync.Core.Models;

namespace DealerSync.Core.Services;

public class InventoryDiagnosticService
{
    public DiagnosticStatus GetStatus(InventoryMatch match)
    {
        if (match.LightspeedItem is not null &&
            match.LightspeedItem.Quantity < 0)
        {
            return DiagnosticStatus.InvalidQuantity;
        }

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