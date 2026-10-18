using System.Globalization;
using CsvHelper;
using DealerSync.Core.Models;
using DealerSync.Core.Services;

namespace DealerSync.Infrastructure.Exporters;

public class ReconciliationDiagnosticsCsvExporter
{
    public void Export(
        TextWriter writer,
        IEnumerable<InventoryMatch> matches,
        IEnumerable<InventoryUpdate> inventoryUpdates)
    {
        var diagnosticService = new InventoryDiagnosticService();

        var updatesBySku = inventoryUpdates
            .GroupBy(
                update => update.Sku,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        using var csv = new CsvWriter(
            writer,
            CultureInfo.InvariantCulture);

        // Preserve existing columns.
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

        // New multi-supplier columns.
        csv.WriteField("Combined Quantity");
        csv.WriteField("Contributors");
        csv.WriteField("Review Reasons");

        csv.NextRecord();

        foreach (var match in matches)
        {
            var shopify = match.ShopifyItem
                ?? throw new InvalidOperationException(
                    "Diagnostic records must contain a Shopify item.");

            var status = diagnosticService.GetStatus(match);
            var requiresReview =
                diagnosticService.RequiresReview(match);

            // Find an inventory update that was actually approved
            // for this Shopify SKU and location.
            InventoryUpdate? approvedUpdate = null;

            if (updatesBySku.TryGetValue(
                    shopify.Sku,
                    out var skuUpdates))
            {
                approvedUpdate = skuUpdates.SingleOrDefault(
                    update => string.Equals(
                        update.Location,
                        shopify.Location,
                        StringComparison.OrdinalIgnoreCase));
            }

            // Show every contributing Lightspeed supplier.
            var contributors = match.Contributors.Count > 0
                ? match.Contributors.Select(contributor =>
                    $"{contributor.LightspeedItem.PartNumber} " +
                    $"({contributor.LightspeedItem.Quantity}, " +
                    $"{contributor.MatchType})")
                : Enumerable.Empty<string>();

            var contributorSummary =
                string.Join(" | ", contributors);

            var reviewReasons = string.Join(
                " | ",
                match.ReviewReasons.Select(reason =>
                    reason.ToString()));

            csv.WriteField(shopify.Sku);
            csv.WriteField(shopify.Title);
            csv.WriteField(shopify.Location);
            csv.WriteField(match.LightspeedItem?.PartNumber);
            csv.WriteField(match.MatchType.ToString());
            csv.WriteField(shopify.CurrentQuantity);

            // Only show quantities from approved inventory updates.
            csv.WriteField(
                approvedUpdate is not null
                    ? approvedUpdate.NewQuantity.ToString(
                        CultureInfo.InvariantCulture)
                    : string.Empty);

            csv.WriteField(match.HasInventoryDifference);
            csv.WriteField(status.ToString());
            csv.WriteField(requiresReview);

            // CombinedQuantity is informational and may be partial.
            csv.WriteField(
                match.CombinedQuantity.HasValue
                    ? match.CombinedQuantity.Value.ToString(
                        CultureInfo.InvariantCulture)
                    : string.Empty);

            csv.WriteField(contributorSummary);
            csv.WriteField(reviewReasons);

            csv.NextRecord();
        }
    }
}