using System.Globalization;
using CsvHelper;
using DealerSync.Core.Models;

namespace DealerSync.Infrastructure.Exporters;

public class ShopifyInventoryCsvExporter
{
    public void Export(
        string filePath,
        IEnumerable<InventoryUpdate> updates)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Match Shopify's inventory CSV format.
        csv.WriteField("Handle");
        csv.WriteField("Title");
        csv.WriteField("Option1 Name");
        csv.WriteField("Option1 Value");
        csv.WriteField("Option2 Name");
        csv.WriteField("Option2 Value");
        csv.WriteField("Option3 Name");
        csv.WriteField("Option3 Value");
        csv.WriteField("SKU");
        csv.WriteField("HS Code");
        csv.WriteField("COO");
        csv.WriteField("Location");
        csv.WriteField("Bin name");
        csv.WriteField("Incoming (not editable)");
        csv.WriteField("Unavailable (not editable)");
        csv.WriteField("Committed (not editable)");
        csv.WriteField("Available (not editable)");
        csv.WriteField("On hand (current)");
        csv.WriteField("On hand (new)");
        csv.NextRecord();

        foreach (var update in updates)
        {
            csv.WriteField(string.Empty); // Handle
            csv.WriteField(update.Title);
            csv.WriteField(string.Empty); // Option1 Name
            csv.WriteField(string.Empty); // Option1 Value
            csv.WriteField(string.Empty); // Option2 Name
            csv.WriteField(string.Empty); // Option2 Value
            csv.WriteField(string.Empty); // Option3 Name
            csv.WriteField(string.Empty); // Option3 Value
            csv.WriteField(update.Sku);
            csv.WriteField(string.Empty); // HS Code
            csv.WriteField(string.Empty); // COO
            csv.WriteField(update.Location);
            csv.WriteField(string.Empty); // Bin name
            csv.WriteField(string.Empty); // Incoming
            csv.WriteField(string.Empty); // Unavailable
            csv.WriteField(string.Empty); // Committed
            csv.WriteField(string.Empty); // Available
            csv.WriteField(update.CurrentQuantity);
            csv.WriteField(update.NewQuantity);

            csv.NextRecord();
        }
    }
}