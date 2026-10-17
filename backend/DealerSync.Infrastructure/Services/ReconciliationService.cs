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
        IEnumerable<VerifiedSkuMapping>? verifiedMappings = null)
    {
        using var lightspeedReader =
            new StreamReader(lightspeedFile);

        using var shopifyReader =
            new StreamReader(shopifyFile);

        return Run(
            lightspeedReader,
            shopifyReader,
            verifiedMappings);
    }

    // Used by the API — accepts readers
    public ReconciliationResult Run(
        TextReader lightspeedReader,
        TextReader shopifyReader,
        IEnumerable<VerifiedSkuMapping>? verifiedMappings = null)
    {
        // Import inventory records
        var lightspeedItems =
            _lightspeedImporter.Import(lightspeedReader);

        var shopifyItems =
            _shopifyImporter.Import(shopifyReader);

        // Reconcile with optional verified SKU mappings
        var matches = _matchingService.ReconcileShopify(
            lightspeedItems,
            shopifyItems,
            verifiedMappings);

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

        // Generate inventory updates
        var inventoryUpdates =
            _matchingService.GetInventoryUpdates(matches);

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