using System.Text;
using DealerSync.Core.Enums;
using DealerSync.Core.Services;
using DealerSync.Infrastructure.Exporters;
using DealerSync.Infrastructure.Importers;

// ------------------------------------------------------------
// Validate command-line arguments
// ------------------------------------------------------------

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

// ------------------------------------------------------------
// Create services
// ------------------------------------------------------------

var lightspeedImporter = new LightspeedCsvImporter();
var shopifyImporter = new ShopifyCsvImporter();
var matchingService = new SkuMatchingService();

// ------------------------------------------------------------
// Import inventory data
// ------------------------------------------------------------

Console.WriteLine("Importing Lightspeed inventory...");
var lightspeedItems = lightspeedImporter.Import(lightspeedFile);

Console.WriteLine("Importing Shopify inventory...");
var shopifyItems = shopifyImporter.Import(shopifyFile);

// ------------------------------------------------------------
// Lightspeed -> Shopify reconciliation
// ------------------------------------------------------------

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

// ------------------------------------------------------------
// Shopify -> Lightspeed reconciliation
// ------------------------------------------------------------

var shopifyResults = matchingService.ReconcileShopify(
    lightspeedItems,
    shopifyItems);

var shopifyExact = shopifyResults.Count(
    result => result.MatchType == SkuMatchType.Exact);

var shopifyNormalized = shopifyResults.Count(
    result => result.MatchType == SkuMatchType.Normalized);

var shopifyUnmatched = shopifyResults.Count(
    result => result.MatchType == SkuMatchType.Unmatched);

var shopifyMatched =
    shopifyExact + shopifyNormalized;

var coverage = shopifyItems.Count == 0
    ? 0
    : (double)shopifyMatched / shopifyItems.Count * 100;

// ------------------------------------------------------------
// Find negative Lightspeed quantities
// ------------------------------------------------------------

var negativeQuantityMatches = shopifyResults
    .Where(result =>
        result.MatchType != SkuMatchType.Unmatched &&
        result.LightspeedItem != null &&
        result.ShopifyItem != null &&
        result.HasInventoryDifference &&
        result.LightspeedItem.Quantity < 0)
    .ToList();

// ------------------------------------------------------------
// Generate safe inventory updates
// ------------------------------------------------------------

var inventoryUpdates =
    matchingService.GetInventoryUpdates(shopifyResults);

// ------------------------------------------------------------
// Generate diagnostic reports
// ------------------------------------------------------------

var diagnosticsDirectory = Path.Combine(
    "data",
    "diagnostics");

Directory.CreateDirectory(diagnosticsDirectory);

// Unmatched Shopify report

var unmatchedFile = Path.Combine(
    diagnosticsDirectory,
    "unmatched_shopify.csv");

using (var writer = new StreamWriter(
           unmatchedFile,
           false,
           Encoding.UTF8))
{
    writer.WriteLine(
        "SKU,Title,Location,CurrentQuantity");

    foreach (var result in shopifyResults
                 .Where(result =>
                     result.MatchType == SkuMatchType.Unmatched))
    {
        var item = result.ShopifyItem;

        if (item == null)
            continue;

        writer.WriteLine(
            $"{EscapeCsv(item.Sku)}," +
            $"{EscapeCsv(item.Title)}," +
            $"{EscapeCsv(item.Location)}," +
            $"{item.CurrentQuantity}");
    }
}

// Lightspeed diagnostic report

var lightspeedDiagnosticFile = Path.Combine(
    diagnosticsDirectory,
    "lightspeed_inventory.csv");

using (var writer = new StreamWriter(
           lightspeedDiagnosticFile,
           false,
           Encoding.UTF8))
{
    writer.WriteLine(
        "PartNumber,Description,Quantity");

    foreach (var item in lightspeedItems)
    {
        writer.WriteLine(
            $"{EscapeCsv(item.PartNumber)}," +
            $"{EscapeCsv(item.Description)}," +
            $"{item.Quantity}");
    }
}

// ------------------------------------------------------------
// Generate Shopify inventory update CSV
// ------------------------------------------------------------

var outputDirectory = Path.Combine(
    "data",
    "output");

Directory.CreateDirectory(outputDirectory);

var updateFile = Path.Combine(
    outputDirectory,
    "shopify_inventory_update.csv");

var exporter = new ShopifyInventoryCsvExporter();

exporter.Export(
    updateFile,
    inventoryUpdates);

// ------------------------------------------------------------
// Console summary
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("DealerSync Inventory Reconciliation");
Console.WriteLine("-----------------------------------");

Console.WriteLine(
    $"Lightspeed items:       {lightspeedItems.Count:N0}");

Console.WriteLine(
    $"Shopify variants:       {shopifyItems.Count:N0}");

Console.WriteLine();

Console.WriteLine(
    $"Exact matches:          {exactMatches:N0}");

Console.WriteLine(
    $"Normalized matches:     {normalizedMatches:N0}");

Console.WriteLine(
    $"Unmatched:              {unmatched:N0}");

Console.WriteLine();

Console.WriteLine(
    $"Inventory differences:  {inventoryDifferences:N0}");

// ------------------------------------------------------------
// Shopify coverage
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("Shopify Match Coverage");
Console.WriteLine("----------------------");

Console.WriteLine(
    $"Exact:                  {shopifyExact:N0}");

Console.WriteLine(
    $"Normalized:             {shopifyNormalized:N0}");

Console.WriteLine(
    $"Unmatched:              {shopifyUnmatched:N0}");

Console.WriteLine(
    $"Coverage:               {coverage:F2}%");

// ------------------------------------------------------------
// Inventory update summary
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("Shopify Inventory Update");
Console.WriteLine("------------------------");

Console.WriteLine(
    $"Inventory updates generated: {inventoryUpdates.Count:N0}");

Console.WriteLine(
    $"Negative quantities excluded: {negativeQuantityMatches.Count:N0}");

Console.WriteLine(
    $"Output file: {updateFile}");

// ------------------------------------------------------------
// Diagnostic file locations
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("Diagnostics");
Console.WriteLine("-----------");

Console.WriteLine(
    $"Unmatched Shopify report: {unmatchedFile}");

Console.WriteLine(
    $"Lightspeed report: {lightspeedDiagnosticFile}");

// ------------------------------------------------------------
// CSV helper
// ------------------------------------------------------------

static string EscapeCsv(string value)
{
    if (string.IsNullOrEmpty(value))
        return string.Empty;

    if (value.Contains(',') ||
        value.Contains('"') ||
        value.Contains('\n') ||
        value.Contains('\r'))
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    return value;
}