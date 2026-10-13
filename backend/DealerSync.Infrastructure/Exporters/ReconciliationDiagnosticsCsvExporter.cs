using System.Globalization;
using CsvHelper;
using DealerSync.Core.Enums;
using DealerSync.Core.Models;
using DealerSync.Core.Services;

namespace DealerSync.Infrastructure.Exporters;

public class ReconciliationDiagnosticsCsvExporter
{
    public void Export(
        TextWriter writer,
        IEnumerable<InventoryMatch> matches)
    {
        var diagnosticService = new InventoryDiagnosticService();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteField("Shopify SKU");
        csv.WriteField("Shopify Title");
        csv.WriteField("Location");
        csv.WriteField("Lightspeed Part Number");
        csv.WriteField("Match Type");
        csv.WriteField("Current Quantity");
        csv.WriteField("Proposed Quantity");
        csv.WriteField("Inventory Difference");
        csv.WriteField("Diagnostic Status");
        csv.WriteField("Requires Review");
        csv.NextRecord();

        foreach (var match in matches)
        {
            var shopify = match.ShopifyItem;

            if (shopify is null)
            {
                throw new InvalidOperationException(
                    "Diagnostic records must contain a Shopify item.");
            }

            var lightspeed = match.LightspeedItem;

            var status = diagnosticService.GetStatus(match);
            var requiresReview = diagnosticService.RequiresReview(match);

            csv.WriteField(shopify.Sku);
            csv.WriteField(shopify.Title);
            csv.WriteField(shopify.Location);
            csv.WriteField(lightspeed?.PartNumber);
            csv.WriteField(match.MatchType.ToString());
            csv.WriteField(shopify.CurrentQuantity);

            // Do not suggest unsafe negative inventory quantities.
            csv.WriteField(
                lightspeed is not null && lightspeed.Quantity >= 0
                    ? lightspeed.Quantity.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);

            csv.WriteField(match.HasInventoryDifference);
            csv.WriteField(status.ToString());
            csv.WriteField(requiresReview);

            csv.NextRecord();
        }
    }
}