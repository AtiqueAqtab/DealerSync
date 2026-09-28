using System.Text;
using DealerSync.Core.Enums;
using DealerSync.Infrastructure.Exporters;
using DealerSync.Infrastructure.Services;

// ------------------------------------------------------------
// Validate arguments
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
// Run reconciliation
// ------------------------------------------------------------

Console.WriteLine("Running inventory reconciliation...");

var reconciliationService = new ReconciliationService();

var result = reconciliationService.Run(
    lightspeedFile,
    shopifyFile);

// ------------------------------------------------------------
// Create output directories
// ------------------------------------------------------------

var diagnosticsDirectory = Path.Combine(
    "data",
    "diagnostics");

var outputDirectory = Path.Combine(
    "data",
    "output");

Directory.CreateDirectory(diagnosticsDirectory);
Directory.CreateDirectory(outputDirectory);

// ------------------------------------------------------------
// Export unmatched Shopify diagnostics
// ------------------------------------------------------------

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

    foreach (var match in result.Matches
                 .Where(match =>
                     match.MatchType == SkuMatchType.Unmatched))
    {
        var item = match.ShopifyItem;

        if (item == null)
            continue;

        writer.WriteLine(
            $"{EscapeCsv(item.Sku)}," +
            $"{EscapeCsv(item.Title)}," +
            $"{EscapeCsv(item.Location)}," +
            $"{item.CurrentQuantity}");
    }
}

// ------------------------------------------------------------
// Export Shopify inventory update CSV
// ------------------------------------------------------------

var updateFile = Path.Combine(
    outputDirectory,
    "shopify_inventory_update.csv");

var exporter = new ShopifyInventoryCsvExporter();

exporter.Export(
    updateFile,
    result.InventoryUpdates);

// ------------------------------------------------------------
// Display summary
// ------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("DealerSync Inventory Reconciliation");
Console.WriteLine("-----------------------------------");

Console.WriteLine(
    $"Lightspeed items:       {result.LightspeedItemCount:N0}");

Console.WriteLine(
    $"Shopify variants:       {result.ShopifyItemCount:N0}");

Console.WriteLine();

Console.WriteLine("Shopify Match Coverage");
Console.WriteLine("----------------------");

Console.WriteLine(
    $"Exact:                  {result.ExactMatches:N0}");

Console.WriteLine(
    $"Normalized:             {result.NormalizedMatches:N0}");

Console.WriteLine(
    $"Unmatched:              {result.Unmatched:N0}");

Console.WriteLine(
    $"Coverage:               {result.ShopifyCoverage:F2}%");

Console.WriteLine();

Console.WriteLine("Shopify Inventory Update");
Console.WriteLine("------------------------");

Console.WriteLine(
    $"Inventory differences:        {result.InventoryDifferences:N0}");

Console.WriteLine(
    $"Inventory updates generated:  {result.InventoryUpdatesGenerated:N0}");

Console.WriteLine(
    $"Negative quantities excluded: {result.NegativeQuantitiesExcluded:N0}");

Console.WriteLine();

Console.WriteLine("Output");
Console.WriteLine("------");

Console.WriteLine(
    $"Shopify update file: {updateFile}");

Console.WriteLine(
    $"Unmatched report:    {unmatchedFile}");

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