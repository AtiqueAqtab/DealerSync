using DealerSync.Infrastructure.Importers;
using Xunit;

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
    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    public void Import_InvalidQuantity_ThrowsFormatException(
        string invalidQuantity)
    {
        var csv =
            "SKU,Title,Location,On hand (current)\n" +
            $"BAD-001,Test Product,Main,{invalidQuantity}\n";

        using var reader = new StringReader(csv);

        var importer = new ShopifyCsvImporter();

        var exception = Assert.Throws<FormatException>(() =>
            importer.Import(reader));

        Assert.Contains("BAD-001", exception.Message);
        Assert.Contains("Invalid quantity", exception.Message);
    }
}