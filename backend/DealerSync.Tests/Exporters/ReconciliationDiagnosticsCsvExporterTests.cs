using DealerSync.Core.Enums;
using DealerSync.Core.Models;
using DealerSync.Infrastructure.Exporters;
using Xunit;

namespace DealerSync.Tests.Exporters;

public class ReconciliationDiagnosticsCsvExporterTests
{
    private readonly ReconciliationDiagnosticsCsvExporter _exporter = new();

    [Fact]
    public void Export_MatchedItem_IncludesInventoryDetails()
    {
        var matches = new[]
        {
            new InventoryMatch
            {
                LightspeedItem = new LightspeedInventoryItem
                {
                    PartNumber = "BMW-123",
                    Quantity = 10
                },
                ShopifyItem = new ShopifyInventoryItem
                {
                    Sku = "BMW-123",
                    Title = "BMW Jacket",
                    Location = "Main",
                    CurrentQuantity = 5
                },
                MatchType = SkuMatchType.Exact
            }
        };

        using var writer = new StringWriter();

        _exporter.Export(writer, matches);

        var csv = writer.ToString();

        Assert.Contains("BMW-123", csv);
        Assert.Contains("Exact", csv);
        Assert.Contains("10", csv);
    }

    [Fact]
    public void Export_UnmatchedItem_RequiresReview()
    {
        var matches = new[]
        {
            new InventoryMatch
            {
                ShopifyItem = new ShopifyInventoryItem
                {
                    Sku = "UNKNOWN-123",
                    Title = "Unknown Jacket",
                    Location = "Main",
                    CurrentQuantity = 4
                },
                MatchType = SkuMatchType.Unmatched
            }
        };

        using var writer = new StringWriter();

        _exporter.Export(writer, matches);

        var csv = writer.ToString();

        Assert.Contains("UNKNOWN-123", csv);
        Assert.Contains("Unmatched", csv);
        Assert.Contains("True", csv);
    }

    [Fact]
    public void Export_NegativeLightspeedQuantity_DoesNotProposeUpdate()
    {
        var matches = new[]
        {
            new InventoryMatch
            {
                LightspeedItem = new LightspeedInventoryItem
                {
                    PartNumber = "BMW-456",
                    Quantity = -3
                },
                ShopifyItem = new ShopifyInventoryItem
                {
                    Sku = "BMW-456",
                    Title = "BMW Gloves",
                    Location = "Main",
                    CurrentQuantity = 2
                },
                MatchType = SkuMatchType.Exact
            }
        };

        using var writer = new StringWriter();

        _exporter.Export(writer, matches);

        var csv = writer.ToString();

        Assert.Contains("BMW-456", csv);
        Assert.Contains("True", csv);
        Assert.DoesNotContain(",-3,", csv);
    }
}