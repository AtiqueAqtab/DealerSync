using System.Globalization;
using CsvHelper;
using DealerSync.Core.Models;

namespace DealerSync.Infrastructure.Importers;

public class LightspeedCsvImporter
{
    public List<LightspeedInventoryItem> Import(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var records = new List<LightspeedInventoryItem>();

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var partNumber = csv.GetField("Part Number");

            if (string.IsNullOrWhiteSpace(partNumber))
                continue;

            var quantityText = csv.GetField("Qty");

            if (!int.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
                continue;

            records.Add(new LightspeedInventoryItem
            {
                PartNumber = partNumber.Trim(),
                Description = csv.GetField("Description")?.Trim() ?? string.Empty,
                Quantity = quantity
            });
        }

        return records;
    }
}
