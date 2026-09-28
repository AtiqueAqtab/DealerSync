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
    
    [Fact]
    public void Reconcile_ShouldNotMatchBlankSkus()
    {
        var lightspeedItems = new List<LightspeedInventoryItem>
        {
            new()
            {
                PartNumber = "",
                Quantity = 5
            }
        };

        var shopifyItems = new List<ShopifyInventoryItem>
        {
            new()
            {
                Sku = "",
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
    
    [Fact]
    public void ReconcileShopify_ShouldLeaveUnknownShopifySkuUnmatched()
    {
        var lightspeedItems = new List<LightspeedInventoryItem>
        {
            new()
            {
                PartNumber = "KLIM-001",
                Quantity = 5
            }
        };

        var shopifyItems = new List<ShopifyInventoryItem>
        {
            new()
            {
                Sku = "NOT-IN-LIGHTSPEED",
                CurrentQuantity = 2
            }
        };

        var results = _service.ReconcileShopify(
            lightspeedItems,
            shopifyItems);

        Assert.Single(results);
        Assert.Equal(SkuMatchType.Unmatched, results[0].MatchType);
        Assert.Null(results[0].LightspeedItem);
        Assert.Equal("NOT-IN-LIGHTSPEED", results[0].ShopifyItem?.Sku);
    }
    
    [Fact]
    public void GetInventoryUpdates_ShouldOnlyReturnChangedMatchedItems()
    {
        var lightspeedItems = new List<LightspeedInventoryItem>
        {
            new() { PartNumber = "ABC-001", Quantity = 5 },
            new() { PartNumber = "ABC-002", Quantity = 3 },
            new() { PartNumber = "ABC-003", Quantity = 8 }
        };

        var shopifyItems = new List<ShopifyInventoryItem>
        {
            new()
            {
                Sku = "ABC-001",
                Title = "Product One",
                Location = "Ottawa",
                CurrentQuantity = 2
            },
            new()
            {
                Sku = "ABC-002",
                Title = "Product Two",
                Location = "Ottawa",
                CurrentQuantity = 3
            }
        };

        var matches = _service.ReconcileShopify(
            lightspeedItems,
            shopifyItems);

        var updates = _service.GetInventoryUpdates(matches);

        Assert.Single(updates);

        Assert.Equal("ABC-001", updates[0].Sku);
        Assert.Equal(2, updates[0].CurrentQuantity);
        Assert.Equal(5, updates[0].NewQuantity);
    }
    
    [Fact]
    public void GetInventoryUpdates_ShouldCollapseIdenticalDuplicates()
    {
        var lightspeedItem = new LightspeedInventoryItem
        {
            PartNumber = "TEST-001",
            Quantity = 5
        };

        var shopifyItem = new ShopifyInventoryItem
        {
            Sku = "TEST-001",
            Title = "Test Product",
            Location = "Ottawa",
            CurrentQuantity = 2
        };

        var matches = new List<InventoryMatch>
        {
            new()
            {
                LightspeedItem = lightspeedItem,
                ShopifyItem = shopifyItem,
                MatchType = SkuMatchType.Exact
            },
            new()
            {
                LightspeedItem = lightspeedItem,
                ShopifyItem = shopifyItem,
                MatchType = SkuMatchType.Exact
            }
        };

        var updates = _service.GetInventoryUpdates(matches);

        Assert.Single(updates);
        Assert.Equal("TEST-001", updates[0].Sku);
        Assert.Equal(5, updates[0].NewQuantity);
    }
    
    [Fact]
    public void GetInventoryUpdates_ShouldRejectConflictingDuplicates()
    {
        var shopifyItem = new ShopifyInventoryItem
        {
            Sku = "TEST-001",
            Title = "Test Product",
            Location = "Ottawa",
            CurrentQuantity = 2
        };

        var matches = new List<InventoryMatch>
        {
            new()
            {
                LightspeedItem = new LightspeedInventoryItem
                {
                    PartNumber = "TEST-001",
                    Quantity = 5
                },
                ShopifyItem = shopifyItem,
                MatchType = SkuMatchType.Exact
            },
            new()
            {
                LightspeedItem = new LightspeedInventoryItem
                {
                    PartNumber = "TEST-001",
                    Quantity = 7
                },
                ShopifyItem = shopifyItem,
                MatchType = SkuMatchType.Exact
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            _service.GetInventoryUpdates(matches));
    }
    
    [Fact]
    public void GetInventoryUpdates_ShouldExcludeNegativeLightspeedQuantity()
    {
        var matches = new List<InventoryMatch>
        {
            new()
            {
                LightspeedItem = new LightspeedInventoryItem
                {
                    PartNumber = "TEST-001",
                    Quantity = -1
                },
                ShopifyItem = new ShopifyInventoryItem
                {
                    Sku = "TEST-001",
                    Title = "Test Product",
                    Location = "Ottawa",
                    CurrentQuantity = 2
                },
                MatchType = SkuMatchType.Exact
            }
        };

        var updates = _service.GetInventoryUpdates(matches);

        Assert.Empty(updates);
    }
}