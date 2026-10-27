using DealerSync.Core.Enums;

namespace DealerSync.Core.Models;

public class InventoryMatch
{
    // Existing properties retained for compatibility.
    public LightspeedInventoryItem? LightspeedItem { get; set; }

    public ShopifyInventoryItem? ShopifyItem { get; set; }

    public SkuMatchType MatchType { get; set; }

    // New: all Lightspeed records contributing to this Shopify item.
    public List<InventoryContributor> Contributors { get; set; } = [];

    // Reasons this Shopify product requires manual review.
    public List<InventoryReviewReason> ReviewReasons { get; set; } = [];

    // New: combined quantity, when contributors are available.
    public int? CombinedQuantity
    {
        get
        {
            if (Contributors.Count == 0)
                return LightspeedItem?.Quantity;

            var total = Contributors.Sum(contributor => (long)contributor.LightspeedItem.Quantity);

            return total is >= int.MinValue and <= int.MaxValue
                ? (int)total
                : null;
        }
    }

    public bool HasInventoryDifference =>
        ShopifyItem != null &&
        CombinedQuantity.HasValue &&
        CombinedQuantity.Value != ShopifyItem.CurrentQuantity;
}