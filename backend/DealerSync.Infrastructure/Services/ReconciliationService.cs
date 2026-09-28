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

    public ReconciliationService()
    {
        _lightspeedImporter = new LightspeedCsvImporter();
        _shopifyImporter = new ShopifyCsvImporter();
        _matchingService = new SkuMatchingService();
    }

    public ReconciliationResult Run(
        string lightspeedFile,
        string shopifyFile)
    {
        var lightspeedItems =
            _lightspeedImporter.Import(lightspeedFile);

        var shopifyItems =
            _shopifyImporter.Import(shopifyFile);

        var matches = _matchingService.ReconcileShopify(
            lightspeedItems,
            shopifyItems);

        var exactMatches = matches.Count(
            match => match.MatchType == SkuMatchType.Exact);

        var normalizedMatches = matches.Count(
            match => match.MatchType == SkuMatchType.Normalized);

        var unmatched = matches.Count(
            match => match.MatchType == SkuMatchType.Unmatched);

        var inventoryDifferences = matches.Count(
            match =>
                match.MatchType != SkuMatchType.Unmatched &&
                match.LightspeedItem != null &&
                match.ShopifyItem != null &&
                match.HasInventoryDifference);

        var negativeQuantitiesExcluded = matches.Count(
            match =>
                match.MatchType != SkuMatchType.Unmatched &&
                match.LightspeedItem != null &&
                match.ShopifyItem != null &&
                match.HasInventoryDifference &&
                match.LightspeedItem.Quantity < 0);

        var inventoryUpdates =
            _matchingService.GetInventoryUpdates(matches);

        var matched =
            exactMatches + normalizedMatches;

        var coverage = shopifyItems.Count == 0
            ? 0
            : (double)matched / shopifyItems.Count * 100;

        return new ReconciliationResult
        {
            LightspeedItemCount = lightspeedItems.Count,
            ShopifyItemCount = shopifyItems.Count,

            ExactMatches = exactMatches,
            NormalizedMatches = normalizedMatches,
            Unmatched = unmatched,

            InventoryDifferences = inventoryDifferences,

            InventoryUpdatesGenerated =
                inventoryUpdates.Count,

            NegativeQuantitiesExcluded =
                negativeQuantitiesExcluded,

            ShopifyCoverage = coverage,

            Matches = matches,
            InventoryUpdates = inventoryUpdates
        };
    }
}