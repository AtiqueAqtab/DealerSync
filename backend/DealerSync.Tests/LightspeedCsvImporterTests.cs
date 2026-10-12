using DealerSync.Infrastructure.Importers;

namespace DealerSync.Tests;

public class LightspeedCsvImporterTests
{
    [Fact]
    public void Import_ReadsSampleIncludingZeroQuantity()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "samples", "lightspeed_inventory_sample.csv");

        var items = new LightspeedCsvImporter().Import(path);

        Assert.Equal(4, items.Count);
        Assert.Equal("KLIM-001", items[0].PartNumber);
        Assert.Equal("KLIM Adventure Jacket", items[0].Description);
        Assert.Equal(4, items[0].Quantity);
        Assert.Equal("TRIUMPH-004", items[3].PartNumber);
        Assert.Equal(0, items[3].Quantity);
    }

    [Fact]
    public void Import_TrimsFieldsAndSkipsMissingPartNumbers()
    {
        var path = Path.GetTempFileName();

        try
        {
            File.WriteAllText(path, """
                                    Part Number,Description,Qty
                                      KLIM-001  ,"  Jacket, adventure  ",4
                                    ,Missing part,3
                                       ,Whitespace part,2
                                    VALID-002,,0
                                    """);

            var items = new LightspeedCsvImporter().Import(path);

            Assert.Equal(2, items.Count);

            Assert.Equal("KLIM-001", items[0].PartNumber);
            Assert.Equal("Jacket, adventure", items[0].Description);
            Assert.Equal(4, items[0].Quantity);

            Assert.Equal("VALID-002", items[1].PartNumber);
            Assert.Equal(string.Empty, items[1].Description);
            Assert.Equal(0, items[1].Quantity);
        }
        finally
        {
            File.Delete(path);
        }
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
            "Part Number,Description,Qty\n" +
            $"BAD-001,Invalid quantity,{invalidQuantity}\n";

        using var reader = new StringReader(csv);

        var importer = new LightspeedCsvImporter();

        var exception = Assert.Throws<FormatException>(() =>
            importer.Import(reader));

        Assert.Contains("BAD-001", exception.Message);
        Assert.Contains("Invalid quantity", exception.Message);
    }
}
