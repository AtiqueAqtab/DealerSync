using DealerSync.Core.Enums;
using DealerSync.Core.Services;
using DealerSync.Infrastructure.Importers;

if (args.Length != 2)
{
    Console.WriteLine("Usage:");
    Console.WriteLine(
        "dotnet run --project backend/DealerSync.Runner -- <lightspeed.csv> <shopify.csv>");

    return;
}

var lightspeedFile = args[0];
var shopifyFile = args[1];

if (!File.Exists(lightspeedFile))
{
    Console.WriteLine($"Lightspeed file not found: {lightspeedFile}");
    return;
}

if (!File.Exists(shopifyFile))
{
    Console.WriteLine($"Shopify file not found: {shopifyFile}");
    return;
}

var lightspeedImporter = new LightspeedCsvImporter();
var shopifyImporter = new ShopifyCsvImporter();
var matchingService = new SkuMatchingService();

Console.WriteLine("Importing Lightspeed inventory...");
var lightspeedItems = lightspeedImporter.Import(lightspeedFile);

Console.WriteLine("Importing Shopify inventory...");
var shopifyItems = shopifyImporter.Import(shopifyFile);

Console.WriteLine("Reconciling inventory...");
var results = matchingService.Reconcile(
    lightspeedItems,
    shopifyItems);

var exactMatches = results.Count(
    result => result.MatchType == SkuMatchType.Exact);

var normalizedMatches = results.Count(
    result => result.MatchType == SkuMatchType.Normalized);

var unmatched = results.Count(
    result => result.MatchType == SkuMatchType.Unmatched);

var inventoryDifferences = results.Count(
    result =>
        result.ShopifyItem != null &&
        result.HasInventoryDifference);

Console.WriteLine();
Console.WriteLine("DealerSync Inventory Reconciliation");
Console.WriteLine("-----------------------------------");

Console.WriteLine($"Lightspeed items:       {lightspeedItems.Count:N0}");
Console.WriteLine($"Shopify variants:       {shopifyItems.Count:N0}");

Console.WriteLine();

Console.WriteLine($"Exact matches:          {exactMatches:N0}");
Console.WriteLine($"Normalized matches:     {normalizedMatches:N0}");
Console.WriteLine($"Unmatched:              {unmatched:N0}");

Console.WriteLine();

Console.WriteLine($"Inventory differences:  {inventoryDifferences:N0}");