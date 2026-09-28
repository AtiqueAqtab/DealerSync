using DealerSync.Core.Models;
using DealerSync.Infrastructure.Exporters;

namespace DealerSync.Tests.Exporters;

public class ShopifyInventoryCsvExporterTests
{
    [Fact]
    public void Export_ShouldCreateShopifyInventoryCsv()
    {
        var exporter = new ShopifyInventoryCsvExporter();

        var updates = new List<InventoryUpdate>
        {
            new()
            {
                Sku = "KLIM-001",
                Title = "KLIM Adventure Jacket",
                Location = "Ottawa",
                CurrentQuantity = 2,
                NewQuantity = 5
            }
        };

        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"dealersync-{Guid.NewGuid()}.csv");

        try
        {
            exporter.Export(filePath, updates);

            Assert.True(File.Exists(filePath));

            var lines = File.ReadAllLines(filePath);

            Assert.Equal(2, lines.Length);

            Assert.Contains("On hand (new)", lines[0]);
            Assert.Contains("KLIM-001", lines[1]);
            Assert.Contains("KLIM Adventure Jacket", lines[1]);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}