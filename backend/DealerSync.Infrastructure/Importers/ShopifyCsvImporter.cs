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

        while (csv.Read())
        {
            var sku = csv.GetField("SKU");

            if (string.IsNullOrWhiteSpace(sku))
                continue;

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
                Sku = sku.Trim(),
                Title = csv.GetField("Title")?.Trim()
                        ?? string.Empty,
                Location = csv.GetField("Location")?.Trim()
                           ?? string.Empty,
                CurrentQuantity = quantity
            });
        }

        return records;
    }
}