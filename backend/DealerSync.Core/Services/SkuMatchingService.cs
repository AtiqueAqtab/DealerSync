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

        // Build lookup tables once.
        var exactLookup = shopifyList
            .Where(item => !string.IsNullOrWhiteSpace(item.Sku))
            .GroupBy(
                item => item.Sku,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var normalizedLookup = shopifyList
            .Where(item => !string.IsNullOrWhiteSpace(item.Sku))
            .GroupBy(item => NormalizeSku(item.Sku))
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(
                group => group.Key,
                group => group.First());

        foreach (var lightspeedItem in lightspeedItems)
        {
            ShopifyInventoryItem? shopifyItem = null;
            var matchType = SkuMatchType.Unmatched;

            // Exact match
            if (exactLookup.TryGetValue(
                    lightspeedItem.PartNumber,
                    out var exactMatch))
            {
                shopifyItem = exactMatch; 
                matchType = SkuMatchType.Exact;
            }
            else
            {
               // Normalized match
               var normalizedPartNumber =
                    NormalizeSku(lightspeedItem.PartNumber);

                if (!string.IsNullOrWhiteSpace(normalizedPartNumber) &&
                    normalizedLookup.TryGetValue(
                        normalizedPartNumber, 
                        out var normalizedMatch)) 
                { 
                    shopifyItem = normalizedMatch; 
                    matchType = SkuMatchType.Normalized; 
                } 
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