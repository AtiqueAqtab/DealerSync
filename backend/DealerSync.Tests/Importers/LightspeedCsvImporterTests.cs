using DealerSync.Infrastructure.Importers;

namespace DealerSync.Tests.Importers;

public class LightspeedCsvImporterTests
{
    [Fact]
    public void Import_ShouldReadValidLightspeedInventoryRows()
    {
        // Arrange
        var importer = new LightspeedCsvImporter();

        var filePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "..",
            "..",
            "..",
            "..",
            "samples",
            "lightspeed_inventory_sample.csv"
        );

        // Act
        var items = importer.Import(filePath);

        // Assert
        Assert.Equal(4, items.Count);

        Assert.Equal("KLIM-001", items[0].PartNumber);
        Assert.Equal("KLIM Adventure Jacket", items[0].Description);
        Assert.Equal(4, items[0].Quantity);

        Assert.Equal("TRIUMPH-004", items[3].PartNumber);
        Assert.Equal(0, items[3].Quantity);
    }
}