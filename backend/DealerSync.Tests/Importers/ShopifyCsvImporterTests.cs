using DealerSync.Infrastructure.Importers;

namespace DealerSync.Tests.Importers;

public class ShopifyCsvImporterTests
{
    [Fact]
    public void Import_ShouldReadValidShopifyInventoryRows()
    {
        // Arrange
        var importer = new ShopifyCsvImporter();

        var filePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "..",
            "..",
            "..",
            "..",
            "samples",
            "shopify_inventory_sample.csv"
        );

        // Act
        var items = importer.Import(filePath);

        // Assert
        Assert.Equal(4, items.Count);

        Assert.Equal("KLIM-001", items[0].Sku);
        Assert.Equal("KLIM Adventure Jacket", items[0].Title);
        Assert.Equal("Ottawa", items[0].Location);
        Assert.Equal(3, items[0].CurrentQuantity);

        Assert.Equal("TRIUMPH-004", items[3].Sku);
        Assert.Equal(1, items[3].CurrentQuantity);
    }
}