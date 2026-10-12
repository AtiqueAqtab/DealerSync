using System.Globalization;
using CsvHelper;
using DealerSync.Core.Models;

namespace DealerSync.Infrastructure.Importers;

public class LightspeedCsvImporter
{
    // Used by the CLI
    public List<LightspeedInventoryItem> Import(string filePath)
    {
        using var reader = new StreamReader(filePath);
        return Import(reader);
    }

    // Used by the API and CLI
    public List<LightspeedInventoryItem> Import(TextReader reader)
    {
        using var csv = new CsvReader(
            reader,
            CultureInfo.InvariantCulture,
            leaveOpen: true);

        var records = new List<LightspeedInventoryItem>();

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var partNumber = csv.GetField("Part Number");

            if (string.IsNullOrWhiteSpace(partNumber))
                continue;

            var quantityText = csv.GetField("Qty");

            if (!int.TryParse(
                    quantityText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var quantity))
            {
                continue;
            }

            records.Add(new LightspeedInventoryItem
            {
                PartNumber = partNumber.Trim(),
                Description = csv.GetField("Description")?.Trim()
                              ?? string.Empty,
                Quantity = quantity
            });
        }

        return records;
    }
}