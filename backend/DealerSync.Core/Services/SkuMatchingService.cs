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
    
    public List<InventoryMatch> ReconcileShopify(
        IEnumerable<LightspeedInventoryItem> lightspeedItems,
        IEnumerable<ShopifyInventoryItem> shopifyItems)
    {
        var lightspeedList = lightspeedItems
            .Where(item => !string.IsNullOrWhiteSpace(item.PartNumber))
            .ToList();

        var exactLookup = lightspeedList
            .GroupBy(
                item => item.PartNumber,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var normalizedLookup = lightspeedList
            .GroupBy(item => NormalizeSku(item.PartNumber))
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(
                group => group.Key,
                group => group.First());

        var results = new List<InventoryMatch>();

        foreach (var shopifyItem in shopifyItems)
        {
            LightspeedInventoryItem? lightspeedItem = null;
            var matchType = SkuMatchType.Unmatched;

            if (!string.IsNullOrWhiteSpace(shopifyItem.Sku))
            {
                if (exactLookup.TryGetValue(
                        shopifyItem.Sku,
                        out var exactMatch))
                {
                    lightspeedItem = exactMatch;
                    matchType = SkuMatchType.Exact;
                }
                else
                {
                    var normalizedSku = NormalizeSku(shopifyItem.Sku);

                    if (normalizedLookup.TryGetValue(
                            normalizedSku,
                            out var normalizedMatch))
                    {
                        lightspeedItem = normalizedMatch;
                        matchType = SkuMatchType.Normalized;
                    }
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
    
    public List<InventoryUpdate> GetInventoryUpdates(
        IEnumerable<InventoryMatch> matches)
    {
        var updates = matches
            .Where(match =>
                match.MatchType != SkuMatchType.Unmatched &&
                match.LightspeedItem != null &&
                match.ShopifyItem != null &&
                match.HasInventoryDifference &&
                match.LightspeedItem.Quantity >= 0)
            .Select(match => new InventoryUpdate
            {
                Sku = match.ShopifyItem!.Sku,
                Title = match.ShopifyItem.Title,
                Location = match.ShopifyItem.Location,
                CurrentQuantity = match.ShopifyItem.CurrentQuantity,
                NewQuantity = match.LightspeedItem!.Quantity
            })
            .ToList();

        var duplicateGroups = updates
            .GroupBy(
                update => $"{update.Sku}|{update.Location}",
                StringComparer.OrdinalIgnoreCase);

        var results = new List<InventoryUpdate>();

        foreach (var group in duplicateGroups)
        {
            var groupUpdates = group.ToList();

            var conflictingQuantities = groupUpdates
                .Select(update => update.NewQuantity)
                .Distinct()
                .Count() > 1;

            if (conflictingQuantities)
            {
                throw new InvalidOperationException(
                    $"Conflicting inventory quantities found for " +
                    $"SKU '{groupUpdates[0].Sku}' at " +
                    $"location '{groupUpdates[0].Location}'.");
            }

            results.Add(groupUpdates[0]);
        }

        return results;
    }
}