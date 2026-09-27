using System.Globalization;
using CsvHelper;
using DealerSync.Core.Models;

namespace DealerSync.Infrastructure.Importers;

public class ShopifyCsvImporter
{
    public List<ShopifyInventoryItem> Import(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var records = new List<ShopifyInventoryItem>();

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var sku = csv.GetField("SKU");

            if (string.IsNullOrWhiteSpace(sku))
                continue;

            var quantityText = csv.GetField("On hand (current)");

            if (!int.TryParse(quantityText, out var quantity))
                continue;

            records.Add(new ShopifyInventoryItem
            {
                Sku = sku.Trim(),
                Title = csv.GetField("Title")?.Trim() ?? string.Empty,
                Location = csv.GetField("Location")?.Trim() ?? string.Empty,
                CurrentQuantity = quantity
            });
        }

        return records;
    }
}