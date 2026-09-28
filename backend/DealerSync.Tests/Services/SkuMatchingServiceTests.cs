using DealerSync.Core.Enums;
using DealerSync.Core.Models;
using DealerSync.Core.Services;

namespace DealerSync.Tests.Services;

public class SkuMatchingServiceTests
{
    private readonly SkuMatchingService _service = new();

    [Fact]
    public void DetermineMatchType_WithIdenticalSkus_ReturnsExact()
    {
        var result = _service.DetermineMatchType(
            "KLIM-001",
            "KLIM-001");

        Assert.Equal(SkuMatchType.Exact, result);
    }

    [Fact]
    public void DetermineMatchType_WithDifferentCase_ReturnsExact()
    {
        var result = _service.DetermineMatchType(
            "klim-001",
            "KLIM-001");

        Assert.Equal(SkuMatchType.Exact, result);
    }

    [Fact]
    public void DetermineMatchType_WithFormattingDifference_ReturnsNormalized()
    {
        var result = _service.DetermineMatchType(
            "TM-FI122GL-BLACK L",
            "TM-FI122GL-BLACK-L");

        Assert.Equal(SkuMatchType.Normalized, result);
    }

    [Fact]
    public void DetermineMatchType_WithDifferentSkus_ReturnsUnmatched()
    {
        var result = _service.DetermineMatchType(
            "ABC123",
            "XYZ789");

        Assert.Equal(SkuMatchType.Unmatched, result);
    }

    [Theory]
    [InlineData("ABC-123", "ABC123")]
    [InlineData("ABC 123", "ABC123")]
    [InlineData("ABC_123", "ABC123")]
    [InlineData("ABC.123", "ABC123")]
    [InlineData("abc/123", "ABC123")]
    public void NormalizeSku_RemovesFormattingDifferences(
        string input,
        string expected)
    {
        var result = _service.NormalizeSku(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Reconcile_ShouldMatchInventoryAndDetectQuantityDifferences()
    {
        var lightspeedItems = new List<LightspeedInventoryItem>
        {
            new()
            {
                PartNumber = "KLIM-001",
                Description = "KLIM Adventure Jacket",
                Quantity = 4
            },
            new()
            {
                PartNumber = "REVIT-002",
                Description = "REVIT Motorcycle Gloves",
                Quantity = 7
            },
            new()
            {
                PartNumber = "BMW-003",
                Description = "BMW Motorrad Jacket",
                Quantity = 2
            },
            new()
            {
                PartNumber = "TRIUMPH-004",
                Description = "Triumph Riding Jacket",
                Quantity = 0
            }
        };

        var shopifyItems = new List<ShopifyInventoryItem>
        {
            new()
            {
                Sku = "KLIM-001",
                CurrentQuantity = 3
            },
            new()
            {
                Sku = "REVIT-002",
                CurrentQuantity = 7
            },
            new()
            {
                Sku = "BMW-003",
                CurrentQuantity = 4
            },
            new()
            {
                Sku = "TRIUMPH-004",
                CurrentQuantity = 1
            }
        };

        var results = _service.Reconcile(
            lightspeedItems,
            shopifyItems);

        Assert.Equal(4, results.Count);

        Assert.All(
            results,
            result => Assert.Equal(
                SkuMatchType.Exact,
                result.MatchType));

        Assert.True(results[0].HasInventoryDifference);
        Assert.False(results[1].HasInventoryDifference);
        Assert.True(results[2].HasInventoryDifference);
        Assert.True(results[3].HasInventoryDifference);
    }

    [Fact]
    public void Reconcile_ShouldUseNormalizedSkuWhenFormattingDiffers()
    {
        var lightspeedItems = new List<LightspeedInventoryItem>
        {
            new()
            {
                PartNumber = "TM-FI122GL-BLACK L",
                Quantity = 5
            }
        };

        var shopifyItems = new List<ShopifyInventoryItem>
        {
            new()
            {
                Sku = "TM-FI122GL-BLACK-L",
                CurrentQuantity = 3
            }
        };

        var results = _service.Reconcile(
            lightspeedItems,
            shopifyItems);

        Assert.Single(results);
        Assert.Equal(
            SkuMatchType.Normalized,
            results[0].MatchType);

        Assert.NotNull(results[0].ShopifyItem);
        Assert.True(results[0].HasInventoryDifference);
    }

    [Fact]
    public void Reconcile_ShouldLeaveUnknownSkuUnmatched()
    {
        var lightspeedItems = new List<LightspeedInventoryItem>
        {
            new()
            {
                PartNumber = "UNKNOWN-123",
                Quantity = 5
            }
        };

        var shopifyItems = new List<ShopifyInventoryItem>
        {
            new()
            {
                Sku = "COMPLETELY-DIFFERENT",
                CurrentQuantity = 5
            }
        };

        var results = _service.Reconcile(
            lightspeedItems,
            shopifyItems);

        Assert.Single(results);
        Assert.Equal(
            SkuMatchType.Unmatched,
            results[0].MatchType);

        Assert.Null(results[0].ShopifyItem);
    }
}