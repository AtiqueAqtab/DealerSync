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
        IEnumerable<ShopifyInventoryItem> shopifyItems,
        IEnumerable<VerifiedSkuMapping>? verifiedMappings = null)
    {
        var shopifyList = shopifyItems.ToList();

        // Validate Shopify inventory locations
        var inventoryItems = shopifyList
            .Where(item => !string.IsNullOrWhiteSpace(item.Sku))
            .ToList();

        // Reject missing locations
        if (inventoryItems.Any(item =>
                string.IsNullOrWhiteSpace(item.Location)))
        {
            throw new InvalidOperationException(
                "Shopify inventory contains records with missing locations.");
        }

        // Identify distinct locations
        var locations = inventoryItems
            .Select(item => item.Location.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // DealerSync currently supports one Shopify location
        if (locations.Count > 1)
        {
            throw new InvalidOperationException(
                "Multiple Shopify locations detected. " +
                "DealerSync currently supports one inventory location.");
        }

        var duplicateShopifyRecords = shopifyList
            .Where(item => !string.IsNullOrWhiteSpace(item.Sku))
            .GroupBy(item => new
            {
                Sku = item.Sku.Trim().ToUpperInvariant(),
                Location = item.Location.Trim().ToUpperInvariant()
            })
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateShopifyRecords != null)
        {
            var duplicate = duplicateShopifyRecords.First();

            throw new InvalidOperationException(
                $"Duplicate Shopify inventory record detected for " +
                $"SKU '{duplicate.Sku}' at location '{duplicate.Location}'.");
        }

        var lightspeedList = lightspeedItems
            .Where(item => !string.IsNullOrWhiteSpace(item.PartNumber))
            .ToList();

        // Detect duplicate Lightspeed part numbers
        var duplicateLightspeedSkus = lightspeedList
            .GroupBy(
                item => item.PartNumber,
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();

        if (duplicateLightspeedSkus.Count > 0)
        {
            var duplicateSku = duplicateLightspeedSkus[0].Key;

            throw new InvalidOperationException(
                $"Duplicate Lightspeed part number detected: " +
                $"'{duplicateSku}'. Reconciliation cannot continue.");
        }

        // Build exact Lightspeed lookup
        var exactLookup = lightspeedList.ToDictionary(
            item => item.PartNumber,
            StringComparer.OrdinalIgnoreCase);

        // Build normalized Lightspeed lookup
        var normalizedLookup = lightspeedList
            .GroupBy(item => NormalizeSku(item.PartNumber))
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        // Build verified mapping lookup
        var mappingList = (verifiedMappings ??
            Enumerable.Empty<VerifiedSkuMapping>()).ToList();

        if (mappingList.Any(mapping =>
                string.IsNullOrWhiteSpace(mapping.LightspeedPartNumber) ||
                string.IsNullOrWhiteSpace(mapping.ShopifySku)))
        {
            throw new InvalidOperationException(
                "Verified SKU mappings cannot contain empty identifiers.");
        }

        // Multiple Lightspeed part numbers may map to one Shopify SKU.
        // A Lightspeed part number must not map to multiple Shopify SKUs.
        var duplicateMappingLightspeed = mappingList
            .GroupBy(
                mapping => mapping.LightspeedPartNumber,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateMappingLightspeed != null)
        {
            throw new InvalidOperationException(
                "Conflicting verified SKU mappings detected.");
        }

        var verifiedLookup = mappingList
            .GroupBy(
                mapping => mapping.ShopifySku,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        // Store matches by their position in the Shopify inventory
        var results = shopifyList
            .Select(shopifyItem => new InventoryMatch
            {
                ShopifyItem = shopifyItem,
                LightspeedItem = null,
                MatchType = SkuMatchType.Unmatched
            })
            .ToList();

        // Track which Shopify SKU claims each Lightspeed part number
        var assignedLightspeed = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        void AssignMatch(
            int index,
            LightspeedInventoryItem lightspeedItem,
            SkuMatchType matchType)
        {
            var shopifySku = shopifyList[index].Sku;

            if (assignedLightspeed.TryGetValue(
                    lightspeedItem.PartNumber,
                    out var existingShopifySku))
            {
                if (!string.Equals(
                        existingShopifySku,
                        shopifySku,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Conflicting inventory assignment: Lightspeed part " +
                        $"'{lightspeedItem.PartNumber}' is claimed by Shopify SKUs " +
                        $"'{existingShopifySku}' and '{shopifySku}'.");
                }

                // Already assigned to this Shopify product.
                // Do not count the same physical inventory twice.
                return;
            }

            assignedLightspeed.Add(
                lightspeedItem.PartNumber,
                shopifySku);

            var match = results[index];

            match.Contributors.Add(new InventoryContributor
            {
                LightspeedItem = lightspeedItem,
                MatchType = matchType
            });

            // Retain the first contributing item for compatibility
            // with existing API responses and diagnostics.
            if (match.LightspeedItem is null)
            {
                match.LightspeedItem = lightspeedItem;
                match.MatchType = matchType;
            }
            else if (matchType == SkuMatchType.Exact)
            {
                // Exact is the highest-confidence overall match type.
                match.MatchType = SkuMatchType.Exact;
            }
        }

        // Pass 1: Exact matches always have first priority
        for (var i = 0; i < shopifyList.Count; i++)
        {
            var shopifySku = shopifyList[i].Sku;

            if (string.IsNullOrWhiteSpace(shopifySku))
                continue;

            if (exactLookup.TryGetValue(
                    shopifySku,
                    out var exactMatch))
            {
                AssignMatch(i, exactMatch, SkuMatchType.Exact);
            }
        }

        // Pass 2: Apply all verified mappings.
        for (var i = 0; i < shopifyList.Count; i++)
        {
            var shopifySku = shopifyList[i].Sku;

            if (string.IsNullOrWhiteSpace(shopifySku))
                continue;

            if (!verifiedLookup.TryGetValue(
                    shopifySku,
                    out var mappings))
            {
                continue;
            }

            foreach (var mapping in mappings)
            {
                // Ignore mappings whose Lightspeed item is absent.
                if (!exactLookup.TryGetValue(
                        mapping.LightspeedPartNumber,
                        out var mappedLightspeed))
                {
                    continue;
                }

                AssignMatch(
                    i,
                    mappedLightspeed,
                    SkuMatchType.Verified);
            }
        }

        // Pass 3: Apply normalized matching to remaining Shopify items
        for (var i = 0; i < shopifyList.Count; i++)
        {
            if (results[i].MatchType != SkuMatchType.Unmatched)
                continue;

            var shopifySku = shopifyList[i].Sku;

            if (string.IsNullOrWhiteSpace(shopifySku))
                continue;

            var normalizedSku = NormalizeSku(shopifySku);

            if (!normalizedLookup.TryGetValue(
                    normalizedSku,
                    out var normalizedMatches))
            {
                continue;
            }

            if (normalizedMatches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Ambiguous normalized SKU match for " +
                    $"Shopify SKU '{shopifySku}'. " +
                    "Multiple Lightspeed part numbers match.");
            }

            AssignMatch(
                i,
                normalizedMatches[0],
                SkuMatchType.Normalized);
        }

        return results;
    }

    public List<InventoryUpdate> GetInventoryUpdates(
    IEnumerable<InventoryMatch> matches)
    {
        var updates = new List<InventoryUpdate>();

        foreach (var match in matches)
        {
            if (match.ShopifyItem is null)
                continue;

            // Preserve compatibility with existing one-to-one matches.
            var contributors = match.Contributors.Count > 0
                ? match.Contributors
                : match.LightspeedItem is not null
                    ? new List<InventoryContributor>
                    {
                        new()
                        {
                            LightspeedItem = match.LightspeedItem,
                            MatchType = match.MatchType
                        }
                    }
                    : new List<InventoryContributor>();

            if (contributors.Count == 0)
                continue;

            // Every contributor must be an exact or verified match.
            if (contributors.Any(contributor =>
                    contributor.MatchType != SkuMatchType.Exact &&
                    contributor.MatchType != SkuMatchType.Verified))
            {
                continue;
            }

            // Never publish negative inventory quantities.
            if (contributors.Any(contributor =>
                contributor.LightspeedItem.Quantity < 0))
            {
                continue;
            }

            // Prevent duplicate contributors from inflating inventory.
            var hasDuplicateContributors = contributors
                .GroupBy(
                    contributor => contributor.LightspeedItem.PartNumber,
                   StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1);

            if (hasDuplicateContributors)
            {
                throw new InvalidOperationException(
                    $"Duplicate inventory contributors detected for " +
                    $"Shopify SKU '{match.ShopifyItem.Sku}'.");
            }

            // Sum using long to avoid overflowing int during aggregation.
            var combinedQuantity = contributors.Sum(
                contributor => (long)contributor.LightspeedItem.Quantity);

            if (combinedQuantity > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Combined inventory quantity exceeds the supported " +
                    $"range for Shopify SKU '{match.ShopifyItem.Sku}'.");
            }

            if (combinedQuantity == match.ShopifyItem.CurrentQuantity)
                continue;

            updates.Add(new InventoryUpdate
            {
                Sku = match.ShopifyItem.Sku,
                Title = match.ShopifyItem.Title,
                Location = match.ShopifyItem.Location,
                CurrentQuantity = match.ShopifyItem.CurrentQuantity,
                NewQuantity = (int)combinedQuantity
            });
        }

        // Preserve the existing protection against conflicting updates
        // targeting the same Shopify SKU and location.
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