using System.Globalization;
using CsvHelper;
using DealerSync.Core.Models;

namespace DealerSync.Infrastructure.Importers;

public class ShopifyCsvImporter
{
    // Used by the CLI
    public List<ShopifyInventoryItem> Import(string filePath)
    {
        using var reader = new StreamReader(filePath);
        return Import(reader);
    }

    // Used by the API and CLI
    public List<ShopifyInventoryItem> Import(TextReader reader)
    {
        using var csv = new CsvReader(
            reader,
            CultureInfo.InvariantCulture,
            leaveOpen: true);

        var records = new List<ShopifyInventoryItem>();

        csv.Read();
        csv.ReadHeader();

        var headers = csv.HeaderRecord ?? [];
        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new FormatException("Duplicate Shopify CSV headers are ambiguous.");

        string? Optional(string name, string? alternative = null)
        {
            var present = headers.Where(header => header == name || header == alternative).ToList();
            if (present.Count > 1)
                throw new FormatException($"Ambiguous Shopify CSV column '{name}'.");
            return present.Count == 0 ? null : csv.GetField(present[0]);
        }

        while (csv.Read())
        {
            var sku = csv.GetField("SKU");

            if (string.IsNullOrWhiteSpace(sku))
                throw new FormatException($"A Shopify SKU is required at CSV row {csv.Parser.Row}.");

            var quantityText = csv.GetField("On hand (current)");

            if (!int.TryParse(
                    quantityText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var quantity))
            {
                throw new FormatException(
                    $"Invalid quantity '{quantityText}' for SKU " +
                    $"'{sku}' at CSV row {csv.Parser.Row}.");
            }

            records.Add(new ShopifyInventoryItem
            {
                ExportIdentity = new ShopifyVariantIdentity(
                    Optional("Handle") ?? string.Empty,
                    Optional("Option1 Name", "Option 1 Name") ?? string.Empty,
                    Optional("Option1 Value", "Option 1 Value") ?? string.Empty,
                    Optional("Option2 Name", "Option 2 Name") ?? string.Empty,
                    Optional("Option2 Value", "Option 2 Value"),
                    Optional("Option3 Name", "Option 3 Name") ?? string.Empty,
                    Optional("Option3 Value", "Option 3 Value"),
                    sku,
                    Optional("Title") ?? string.Empty,
                    csv.GetField("Location") ?? string.Empty),
                Sku = sku.Trim(),
                Title = Optional("Title")?.Trim()
                        ?? string.Empty,
                Location = csv.GetField("Location")?.Trim()
                           ?? string.Empty,
                CurrentQuantity = quantity
            });
        }

        return records;
    }
}
