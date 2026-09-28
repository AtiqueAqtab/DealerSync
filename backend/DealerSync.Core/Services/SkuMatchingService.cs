using System.Text.RegularExpressions;
using DealerSync.Core.Enums;
using DealerSync.Core.Models;

namespace DealerSync.Core.Services;

public class SkuMatchingService
{
    public SkuMatchType DetermineMatchType(
        string lightspeedPartNumber,
        string shopifySku)
    {
        if (string.Equals(
                lightspeedPartNumber,
                shopifySku,
                StringComparison.OrdinalIgnoreCase))
        {
            return SkuMatchType.Exact;
        }

        var normalizedLightspeed = NormalizeSku(lightspeedPartNumber);
        var normalizedShopify = NormalizeSku(shopifySku);

        if (normalizedLightspeed == normalizedShopify)
        {
            return SkuMatchType.Normalized;
        }

        return SkuMatchType.Unmatched;
    }

    public string NormalizeSku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return string.Empty;

        return Regex.Replace(
                sku,
                @"[\s\-_./]",
                string.Empty)
            .ToUpperInvariant();
    }

    public List<InventoryMatch> Reconcile(
        IEnumerable<LightspeedInventoryItem> lightspeedItems,
        IEnumerable<ShopifyInventoryItem> shopifyItems)
    {
        var results = new List<InventoryMatch>();
        var shopifyList = shopifyItems.ToList();

        foreach (var lightspeedItem in lightspeedItems)
        {
            var shopifyItem = shopifyList.FirstOrDefault(item =>
                string.Equals(
                    lightspeedItem.PartNumber,
                    item.Sku,
                    StringComparison.OrdinalIgnoreCase));

            var matchType = SkuMatchType.Exact;

            if (shopifyItem == null)
            {
                var normalizedLightspeed =
                    NormalizeSku(lightspeedItem.PartNumber);

                shopifyItem = shopifyList.FirstOrDefault(item =>
                    NormalizeSku(item.Sku) == normalizedLightspeed);

                matchType = shopifyItem != null
                    ? SkuMatchType.Normalized
                    : SkuMatchType.Unmatched;
            }

            results.Add(new InventoryMatch
            {
                LightspeedItem = lightspeedItem,
                ShopifyItem = shopifyItem,
                MatchType = matchType
            });
        }

        return results;
    }
}