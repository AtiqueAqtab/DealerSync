using DealerSync.Core.Enums;
using DealerSync.Core.Models;
using DealerSync.Core.Services;
using DealerSync.Infrastructure.Importers;

namespace DealerSync.Infrastructure.Services;

public class ReconciliationService
{
    private readonly LightspeedCsvImporter _lightspeedImporter;
    private readonly ShopifyCsvImporter _shopifyImporter;
    private readonly SkuMatchingService _matchingService;

    public ReconciliationService(
        LightspeedCsvImporter lightspeedImporter,
        ShopifyCsvImporter shopifyImporter,
        SkuMatchingService matchingService)
    {
        _lightspeedImporter = lightspeedImporter;
        _shopifyImporter = shopifyImporter;
        _matchingService = matchingService;
    }

    // Used by the CLI — accepts file paths
    public ReconciliationResult Run(
        string lightspeedFile,
        string shopifyFile,
        IEnumerable<VerifiedSkuMapping>? verifiedMappings = null,
        IEnumerable<SkuMappingInfo>? allMappings = null)
    {
        using var lightspeedReader =
            new StreamReader(lightspeedFile);

        using var shopifyReader =
            new StreamReader(shopifyFile);

        return Run(
            lightspeedReader,
            shopifyReader,
            verifiedMappings,
            allMappings);
    }

    // Used by the API — accepts readers
    public ReconciliationResult Run(
        TextReader lightspeedReader,
        TextReader shopifyReader,
        IEnumerable<VerifiedSkuMapping>? verifiedMappings = null,
        IEnumerable<SkuMappingInfo>? allMappings = null)
    {
        // Import inventory records
        var lightspeedItems =
            _lightspeedImporter.Import(lightspeedReader);

        var shopifyItems =
            _shopifyImporter.Import(shopifyReader);

        // Track why each Shopify SKU requires manual review.
        var reviewReasonsBySku =
            new Dictionary<string, HashSet<InventoryReviewReason>>(
                StringComparer.OrdinalIgnoreCase);

        void AddReviewReason(
            string shopifySku,
            InventoryReviewReason reason)
        {
            if (!reviewReasonsBySku.TryGetValue(
                    shopifySku,
                    out var reasons))
            {
                reasons = new HashSet<InventoryReviewReason>();
                reviewReasonsBySku[shopifySku] = reasons;
            }

            reasons.Add(reason);
        }

        if (allMappings is not null)
        {
            var availableLightspeedParts = lightspeedItems
                .Select(item => item.PartNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var mapping in allMappings)
            {
                // An unverified supplier blocks its Shopify product.
                if (!mapping.IsVerified)
                {
                    AddReviewReason(
                        mapping.ShopifySku,
                        InventoryReviewReason.UnverifiedSupplier);
                }

                // A verified supplier missing from Lightspeed also blocks it.
                if (mapping.IsVerified &&
                    !availableLightspeedParts.Contains(
                        mapping.LightspeedPartNumber))
                {
                    AddReviewReason(
                        mapping.ShopifySku,
                        InventoryReviewReason.MissingSupplierInventory);
                }
            }
        }

        if (allMappings is not null)
        {
            var availableLightspeedParts = lightspeedItems
                .Select(item => item.PartNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var mapping in allMappings)
            {
                // An unverified supplier requires manual review.
                if (!mapping.IsVerified)
                {
                    AddReviewReason(
                        mapping.ShopifySku,
                        InventoryReviewReason.UnverifiedSupplier);
                }

                // A verified supplier missing from the Lightspeed CSV
                // also requires manual review.
                if (mapping.IsVerified &&
                    !availableLightspeedParts.Contains(
                        mapping.LightspeedPartNumber))
                {
                    AddReviewReason(
                        mapping.ShopifySku,
                        InventoryReviewReason.MissingSupplierInventory);
                }
            }
        }

        // Reconcile with optional verified SKU mappings
        var matches = _matchingService.ReconcileShopify(
            lightspeedItems,
            shopifyItems,
            verifiedMappings);

        // Attach review reasons to each Shopify inventory match.
        foreach (var match in matches)
        {
            if (match.ShopifyItem is null)
                continue;

            if (reviewReasonsBySku.TryGetValue(
                    match.ShopifyItem.Sku,
                    out var reasons))
            {
                match.ReviewReasons = reasons.ToList();
            }
        }

        // Match statistics
        var exactMatches = matches.Count(
            match => match.MatchType == SkuMatchType.Exact);

        var verifiedMatches = matches.Count(
            match => match.MatchType == SkuMatchType.Verified);

        var normalizedMatches = matches.Count(
            match => match.MatchType == SkuMatchType.Normalized);

        var unmatched = matches.Count(
            match => match.MatchType == SkuMatchType.Unmatched);

        // Inventory differences
        var inventoryDifferences = matches.Count(
            match =>
                match.MatchType != SkuMatchType.Unmatched &&
                match.LightspeedItem != null &&
                match.ShopifyItem != null &&
                match.HasInventoryDifference);

        // Exclude negative Lightspeed quantities
        var negativeQuantitiesExcluded = matches.Count(match =>
            match.ShopifyItem != null &&
            match.Contributors.Any(contributor =>
                contributor.LightspeedItem.Quantity < 0) &&
            match.HasInventoryDifference);

        var normalizedUpdatesExcluded = matches.Count(match =>
            match.ShopifyItem != null &&
            match.HasInventoryDifference &&
            match.Contributors.Any(contributor =>
                contributor.MatchType == SkuMatchType.Normalized) &&
            match.Contributors.All(contributor =>
                contributor.LightspeedItem.Quantity >= 0));

        // Exclude products requiring supplier review
        // before generating automatic inventory updates.
        var eligibleMatches = matches
            .Where(match =>
                match.ShopifyItem is not null &&
                !reviewReasonsBySku.ContainsKey(match.ShopifyItem.Sku))
            .ToList();

        // Generate updates only for eligible products.
        var inventoryUpdates =
            _matchingService.GetInventoryUpdates(eligibleMatches);

        // All successful matching types contribute to coverage
        var matched =
            exactMatches +
            verifiedMatches +
            normalizedMatches;

        var coverage = shopifyItems.Count == 0
            ? 0
            : (double)matched / shopifyItems.Count * 100;

        return new ReconciliationResult
        {
            LightspeedItemCount = lightspeedItems.Count,
            ShopifyItemCount = shopifyItems.Count,

            ExactMatches = exactMatches,
            VerifiedMatches = verifiedMatches,
            NormalizedMatches = normalizedMatches,
            Unmatched = unmatched,

            InventoryDifferences = inventoryDifferences,

            InventoryUpdatesGenerated = inventoryUpdates.Count,
            NegativeQuantitiesExcluded = negativeQuantitiesExcluded,
            NormalizedUpdatesExcluded = normalizedUpdatesExcluded,

            ShopifyCoverage = coverage,

            Matches = matches,
            InventoryUpdates = inventoryUpdates
        };
    }
}