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
        string shopifyFile)
    {
        using var lightspeedReader = new StreamReader(lightspeedFile);
        using var shopifyReader = new StreamReader(shopifyFile);

        return Run(lightspeedReader, shopifyReader);
    }

    // Used by the API — accepts readers
    public ReconciliationResult Run(
        TextReader lightspeedReader,
        TextReader shopifyReader)
    {
        var lightspeedItems =
            _lightspeedImporter.Import(lightspeedReader);

        var shopifyItems =
            _shopifyImporter.Import(shopifyReader);

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

        var matched = exactMatches + normalizedMatches;

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

            InventoryUpdatesGenerated = inventoryUpdates.Count,
            NegativeQuantitiesExcluded = negativeQuantitiesExcluded,

            ShopifyCoverage = coverage,

            Matches = matches,
            InventoryUpdates = inventoryUpdates
        };
    }
}