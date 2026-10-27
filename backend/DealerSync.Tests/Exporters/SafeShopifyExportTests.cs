using System.Globalization;
using CsvHelper;
using DealerSync.Core.Models;
using DealerSync.Core.Services;
using DealerSync.Infrastructure.Exporters;
using DealerSync.Infrastructure.Importers;

namespace DealerSync.Tests.Exporters;

public class SafeShopifyExportTests
{
    private static ShopifyVariantIdentity Identity => new(
        "synthetic-product", "Title", "Default Title", "", "", "", "", "TEST-SKU", "Synthetic product", "Ottawa East");

    private static InventoryUpdate Update(ShopifyVariantIdentity? identity, int quantity = 7) => new()
    {
        ExportIdentity = identity, Sku = identity?.Sku.Trim() ?? "TEST-SKU",
        Title = identity?.Title.Trim() ?? "", Location = identity?.Location.Trim() ?? "Ottawa East",
        CurrentQuantity = 5, NewQuantity = quantity
    };

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 7)]
    [InlineData(3, 9)]
    public void Export_PreservesDocumentedIdentityAndCurrentStock(int options, int quantity)
    {
        var identity = Identity with
        {
            Handle = options == 3 ? "café-product" : "synthetic-product",
            Option1Name = "Color", Option1Value = "Blue, \"navy\"",
            Option2Name = options >= 2 ? "Size" : "", Option2Value = options >= 2 ? "L" : "",
            Option3Name = options == 3 ? "Finish" : "", Option3Value = options == 3 ? "Café\nMatte" : "",
            Title = "Original, \"quoted\"\nproduct"
        };
        var output = new ShopifyInventoryCsvExporter().ExportToString([Update(identity, quantity)]);
        using var reader = new CsvReader(new StringReader(output), CultureInfo.InvariantCulture);
        Assert.True(reader.Read());
        reader.ReadHeader();
        Assert.Equal(new[] {"Handle", "Title", "Option 1 Name", "Option 1 Value", "Option 2 Name", "Option 2 Value",
            "Option 3 Name", "Option 3 Value", "SKU", "Location", "On hand (current)", "On hand (new)"}, reader.HeaderRecord);
        Assert.True(reader.Read());
        Assert.Equal(identity.Handle, reader.GetField("Handle"));
        Assert.Equal(identity.Sku, reader.GetField("SKU"));
        Assert.Equal(identity.Title, reader.GetField("Title"));
        Assert.Equal(identity.Location, reader.GetField("Location"));
        Assert.Equal(identity.Option1Value, reader.GetField("Option 1 Value"));
        Assert.Equal(identity.Option2Value, reader.GetField("Option 2 Value"));
        Assert.Equal(identity.Option3Value, reader.GetField("Option 3 Value"));
        Assert.Equal("5", reader.GetField("On hand (current)"));
        Assert.Equal(quantity.ToString(CultureInfo.InvariantCulture), reader.GetField("On hand (new)"));
        Assert.False(reader.Read());
    }

    [Fact]
    public void ImportAndMatching_PreserveRawIdentityWithoutChangingMatchingNormalization()
    {
        var input = "Handle,Title,Option 1 Name,Option 1 Value,Option 2 Value,Option 3 Value,SKU,Location,On hand (current)\n" +
                    "synthetic-product,  Original title  ,Title,Default Title,,, TEST-SKU ,Ottawa East,5\n";
        var shopify = new ShopifyCsvImporter().Import(new StringReader(input));
        Assert.Equal("TEST-SKU", shopify[0].Sku);
        Assert.Equal(" TEST-SKU ", shopify[0].ExportIdentity!.Sku);
        Assert.Equal("  Original title  ", shopify[0].ExportIdentity!.Title);
        var matcher = new SkuMatchingService();
        var matches = matcher.ReconcileShopify([new LightspeedInventoryItem {PartNumber = "TEST-SKU", Quantity = 7}], shopify);
        var updates = matcher.GetInventoryUpdates(matches);
        Assert.Same(shopify[0].ExportIdentity, Assert.Single(updates).ExportIdentity);
    }

    public static IEnumerable<object?[]> InvalidIdentities()
    {
        yield return [null];
        yield return [Identity with {Handle = ""}];
        yield return [Identity with {Handle = "invalid handle"}];
        yield return [Identity with {Option1Value = ""}];
        yield return [Identity with {Option2Name = "Size", Option2Value = null}];
        yield return [Identity with {Option3Name = "Finish", Option3Value = null}];
        yield return [Identity with {Option2Name = "Size", Option2Value = ""}];
        yield return [Identity with {Option2Value = "   "}];
        yield return [Identity with {Option3Name = "Finish", Option3Value = "Matte"}];
        yield return [Identity with {Title = " \t=1+1"}];
        yield return [Identity with {Sku = "+123"}];
        yield return [Identity with {Option1Value = "@SUM(1,2)"}];
        yield return [Identity with {Location = "-Ottawa"}];
    }

    [Theory]
    [MemberData(nameof(InvalidIdentities))]
    public void Export_InvalidIdentityDoesNotWriteAPartialFile(ShopifyVariantIdentity? identity)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ShopifyInventoryCsvExporter().ExportToString([Update(Identity), Update(identity)]));
    }

    [Fact]
    public void Import_DefaultVariantCanOmitUnusedOptionsAndOptionalTitle()
    {
        var source = new ShopifyCsvImporter().Import(new StringReader(
            "Handle,Option1 Value,SKU,Location,On hand (current)\nproduct,Default Title,TEST-SKU,Ottawa,5\n"));
        var matcher = new SkuMatchingService();
        var matches = matcher.ReconcileShopify([new LightspeedInventoryItem {PartNumber = "TEST-SKU", Quantity = 7}], source);
        var output = new ShopifyInventoryCsvExporter().ExportToString(matcher.GetInventoryUpdates(matches), source);
        using var csv = new CsvReader(new StringReader(output), CultureInfo.InvariantCulture);
        csv.Read(); csv.ReadHeader(); Assert.True(csv.Read());
        Assert.Equal("product", csv.GetField("Handle"));
        Assert.Equal("Default Title", csv.GetField("Option 1 Value"));
        Assert.Equal("", csv.GetField("Option 2 Value"));
        Assert.Equal("", csv.GetField("Option 3 Value"));
        Assert.Equal("", csv.GetField("Title"));
    }

    [Fact]
    public void Export_InvalidIdentityDoesNotCreateADestinationFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dealersync-invalid-export-{Guid.NewGuid()}.csv");
        try
        {
            Assert.Throws<InvalidOperationException>(() => new ShopifyInventoryCsvExporter().Export(path,
                [Update(Identity), Update(Identity with {Handle = ""})]));
            Assert.False(File.Exists(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Export_DuplicateVariantWithDifferentSkuIsAmbiguousEvenIfOneRowIsBlocked()
    {
        var first = new ShopifyInventoryItem {ExportIdentity = Identity};
        var second = new ShopifyInventoryItem {ExportIdentity = Identity with {Sku = "OTHER-SKU"}};
        Assert.Throws<InvalidOperationException>(() =>
            new ShopifyInventoryCsvExporter().ExportToString([Update(Identity)], [first, second]));
    }

    [Fact]
    public void Export_RejectsChangedCurrentStock()
    {
        var source = new ShopifyInventoryItem {ExportIdentity = Identity, CurrentQuantity = 2};
        Assert.Throws<InvalidOperationException>(() =>
            new ShopifyInventoryCsvExporter().ExportToString([Update(Identity)], [source]));
    }

    [Fact]
    public void Export_RejectsFilesAboveShopifyLimit()
    {
        var identity = Identity with {Title = new string('x', 15_000_000)};
        Assert.Throws<InvalidOperationException>(() => new ShopifyInventoryCsvExporter().ExportToString([Update(identity)]));
    }

    [Fact]
    public void Export_LocationCaseIsNotNormalized()
    {
        Assert.Throws<InvalidOperationException>(() => new ShopifyInventoryCsvExporter().ExportToString([
            Update(Identity), Update(Identity with {Sku = "OTHER-SKU", Handle = "other", Location = "OTTAWA EAST"})]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Import_MissingSkuDoesNotSilentlyDiscardSourceIdentity(string sku)
    {
        Assert.Throws<FormatException>(() => new ShopifyCsvImporter().Import(new StringReader(
            $"SKU,Title,Location,On hand (current)\n{sku},Product,Ottawa,2\n")));
    }

    [Theory]
    [InlineData("Handle,Handle,Title,SKU,Location,On hand (current)")]
    [InlineData("Handle,Option1 Value,Option 1 Value,Title,SKU,Location,On hand (current)")]
    public void Import_DuplicateOrAliasedIdentityHeadersAreRejected(string headers)
    {
        var row = headers.StartsWith("Handle,Handle", StringComparison.Ordinal)
            ? "a,a,Product,TEST-SKU,Ottawa,2" : "a,Blue,Blue,Product,TEST-SKU,Ottawa,2";
        Assert.Throws<FormatException>(() => new ShopifyCsvImporter().Import(new StringReader(headers + "\n" + row + "\n")));
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData(" +1+1")]
    [InlineData("\t-1+1")]
    [InlineData("\uFEFF@SUM(1,2)")]
    public void Diagnostics_BackendEscapesFormulaTextWithoutChangingNumericQuantities(string text)
    {
        var match = new InventoryMatch
        {
            ShopifyItem = new ShopifyInventoryItem {Sku = text, Title = text, Location = text, CurrentQuantity = -2},
            MatchType = DealerSync.Core.Enums.SkuMatchType.Unmatched
        };
        using var writer = new StringWriter();
        new ReconciliationDiagnosticsCsvExporter().Export(writer, [match], []);
        using var csv = new CsvReader(new StringReader(writer.ToString()), CultureInfo.InvariantCulture);
        csv.Read(); csv.ReadHeader(); csv.Read();
        Assert.Equal("'" + text, csv.GetField("Shopify SKU"));
        Assert.Equal("'" + text, csv.GetField("Shopify Title"));
        Assert.Equal("'" + text, csv.GetField("Location"));
        Assert.Equal("-2", csv.GetField("Current Quantity"));
    }
}
