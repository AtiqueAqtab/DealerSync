using DealerSync.Core.Enums;

namespace DealerSync.Core.Models;

public class InventoryMatch
{
    public LightspeedInventoryItem? LightspeedItem { get; set; }

    public ShopifyInventoryItem? ShopifyItem { get; set; }

    public SkuMatchType MatchType { get; set; }

    public bool HasInventoryDifference =>
        LightspeedItem != null &&
        ShopifyItem != null &&
        LightspeedItem.Quantity != ShopifyItem.CurrentQuantity;
}