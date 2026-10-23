using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using DealerSync.Core.Models;

namespace DealerSync.Infrastructure.Exporters;

public class ShopifyInventoryCsvExporter
{
    // A supported All states update subset. Omitted optional columns are not blanked.
    private static readonly string[] Headers =
    [
        "Handle", "Title", "Option 1 Name", "Option 1 Value", "Option 2 Name", "Option 2 Value",
        "Option 3 Name", "Option 3 Value", "SKU", "Location", "On hand (current)", "On hand (new)"
    ];

    public string ExportToString(IEnumerable<InventoryUpdate> updates,
        IEnumerable<ShopifyInventoryItem>? sourceItems = null)
    {
        var approved = updates.ToList();
        var sources = sourceItems?.ToList();
        var identities = sources is null
            ? approved.Select(update => update.ExportIdentity).ToList()
            : sources.Select(item => item.ExportIdentity).ToList();
        ValidateIdentities(identities);
        var sourceKeys = identities.Select(identity => identity!).ToHashSet();
        var currentQuantities = sources?.ToDictionary(source => source.ExportIdentity!, source => source.CurrentQuantity);

        foreach (var update in approved)
        {
            var identity = update.ExportIdentity;
            if (identity is null || !sourceKeys.Contains(identity) ||
                identity.Sku.Trim() != update.Sku || identity.Location.Trim() != update.Location ||
                update.NewQuantity < 0)
                throw new InvalidOperationException("An approved update has inconsistent source identity or quantity.");
            if (currentQuantities is not null && (!currentQuantities.TryGetValue(identity, out var current) ||
                    current != update.CurrentQuantity))
                throw new InvalidOperationException("An approved update does not preserve the original current quantity.");
        }
        if (approved.GroupBy(update => update.ExportIdentity).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Duplicate approved inventory updates are ambiguous.");

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture, leaveOpen: true))
        {
            foreach (var header in Headers) csv.WriteField(header);
            csv.NextRecord();
            foreach (var update in approved)
            {
                var identity = update.ExportIdentity!;
                foreach (var field in new[] {identity.Handle, identity.Title, identity.Option1Name,
                             identity.Option1Value, identity.Option2Name, identity.Option2Value,
                             identity.Option3Name, identity.Option3Value, identity.Sku, identity.Location})
                    csv.WriteField(field);
                csv.WriteField(update.CurrentQuantity);
                csv.WriteField(update.NewQuantity);
                csv.NextRecord();
            }
        }
        var output = writer.ToString();
        if (Encoding.UTF8.GetByteCount(output) + 3 > 15_000_000)
            throw new InvalidOperationException("The update file exceeds Shopify's 15 MB limit. Reconcile a smaller input.");
        return output;
    }

    public void Export(string filePath, IEnumerable<InventoryUpdate> updates)
    {
        // Validate the complete file before creating or truncating a destination.
        var output = ExportToString(updates);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void ValidateIdentities(List<ShopifyVariantIdentity?> identities)
    {
        foreach (var identity in identities)
        {
            if (identity is null || string.IsNullOrWhiteSpace(identity.Handle) ||
                !Regex.IsMatch(identity.Handle, @"^[\p{L}\p{Nd}-]+$") ||
                string.IsNullOrWhiteSpace(identity.Option1Value) ||
                string.IsNullOrWhiteSpace(identity.Sku) || string.IsNullOrWhiteSpace(identity.Location))
                throw new InvalidOperationException("Source identity is incomplete. Keep the source Handle, SKU, location, and required option values from the All states export.");
            if ((!string.IsNullOrEmpty(identity.Option2Name) && string.IsNullOrWhiteSpace(identity.Option2Value)) ||
                (!string.IsNullOrEmpty(identity.Option3Name) && string.IsNullOrWhiteSpace(identity.Option3Value)) ||
                (!string.IsNullOrEmpty(identity.Option2Value) && string.IsNullOrWhiteSpace(identity.Option2Value)) ||
                (!string.IsNullOrEmpty(identity.Option3Value) && string.IsNullOrWhiteSpace(identity.Option3Value)) ||
                (!string.IsNullOrWhiteSpace(identity.Option3Value) && string.IsNullOrWhiteSpace(identity.Option2Value)))
                throw new InvalidOperationException("An applicable option value is missing or option values contain a gap.");
            if (new[] {identity.Handle, identity.Title, identity.Option1Name, identity.Option1Value,
                    identity.Option2Name, identity.Option2Value, identity.Option3Name, identity.Option3Value,
                    identity.Sku, identity.Location}.Any(CsvTextSafety.IsFormulaLike))
                throw new InvalidOperationException("Source text is formula-like. Identity was not modified; review the input before exporting.");
        }
        var valid = identities.Select(identity => identity!).ToList();
        if (valid.Select(identity => identity.Location).Distinct(StringComparer.Ordinal).Count() > 1)
            throw new InvalidOperationException("Export requires one exact, case-sensitive Shopify location.");
        // Do not use delimiters in keys: option values can themselves contain them.
        if (valid.GroupBy(identity => (identity.Handle.ToUpperInvariant(),
                identity.Option1Value.ToUpperInvariant(), identity.Option2Value?.ToUpperInvariant() ?? string.Empty,
                identity.Option3Value?.ToUpperInvariant() ?? string.Empty, identity.Location)).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Duplicate product/variant/location identity is ambiguous, even when SKUs differ.");
    }
}
