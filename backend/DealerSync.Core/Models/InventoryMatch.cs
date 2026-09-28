using DealerSync.Core.Enums;

namespace DealerSync.Core.Models;

public class InventoryMatch
{
    public LightspeedInventoryItem LightspeedItem { get; set; } = null!;

    public ShopifyInventoryItem? ShopifyItem { get; set; }

    public SkuMatchType MatchType { get; set; }

    public bool HasInventoryDifference =>
        ShopifyItem != null &&
        LightspeedItem.Quantity != ShopifyItem.CurrentQuantity;
}