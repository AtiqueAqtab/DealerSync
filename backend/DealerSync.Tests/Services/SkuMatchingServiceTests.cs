using DealerSync.Core.Enums;
using DealerSync.Core.Models;
using DealerSync.Core.Services;
using Xunit;

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
                Sku = "KLIM-001",
                Location = "Main",
                CurrentQuantity = 3
            },
            new()
            {
                Sku = "UNKNOWN-001",
                Location = "Main",
                CurrentQuantity = 5
            }
        };

        var results = _service.ReconcileShopify(
            lightspeedItems,
            shopifyItems);

        // Both Shopify products should appear in the results
        Assert.Equal(2, results.Count);

        // First product: exact match
        Assert.Equal(SkuMatchType.Exact, results[0].MatchType);
        Assert.Equal("KLIM-001", results[0].ShopifyItem?.Sku);
        Assert.NotNull(results[0].LightspeedItem);

        // Second product: unmatched
        Assert.Equal(SkuMatchType.Unmatched, results[1].MatchType);
        Assert.Equal("UNKNOWN-001", results[1].ShopifyItem?.Sku);
        Assert.Null(results[1].LightspeedItem);
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

    [Fact]
    public void ReconcileShopify_DuplicateLightspeedSku_ThrowsException()
    {
        // Arrange
        var lightspeedItems = new List<LightspeedInventoryItem>
        {
            new()
            {
                PartNumber = "KLIM-001",
                Description = "Adventure Jacket",
                Quantity = 5
            },
            new()
            {
                PartNumber = "KLIM-001",
                Description = "Adventure Jacket",
                Quantity = 8
            }
        };

        var shopifyItems = new List<ShopifyInventoryItem>
        {
            new()
            {
                Sku = "KLIM-001",
                Title = "Adventure Jacket",
                Location = "Ottawa",
                CurrentQuantity = 3
            }
        };

        var service = new SkuMatchingService();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => service.ReconcileShopify(
                lightspeedItems,
                shopifyItems));

        Assert.Contains("KLIM-001", exception.Message);
    }

    [Fact]
    public void ReconcileShopify_AmbiguousNormalizedSku_ThrowsException()
    {
        // Arrange
        var lightspeedItems = new List<LightspeedInventoryItem>
        {
            new()
            {
                PartNumber = "KLIM-001",
                Description = "Adventure Jacket",
                Quantity = 5
            },
            new()
            {
                PartNumber = "KLIM001",
                Description = "Different Jacket",
                Quantity = 8
            }
        };

        var shopifyItems = new List<ShopifyInventoryItem>
        {
            new()
            {
                Sku = "KLIM_001",
                Title = "Adventure Jacket",
                Location = "Ottawa",
                CurrentQuantity = 3
            }
        };

        var service = new SkuMatchingService();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => service.ReconcileShopify(
                lightspeedItems,
                shopifyItems));

        Assert.Contains("KLIM_001", exception.Message);
    }

    [Fact]
    public void ReconcileShopify_ExactMatchWithNormalizedCollision_Succeeds()
    {
        // Arrange
        var lightspeedItems = new List<LightspeedInventoryItem>
        {
            new()
            {
                PartNumber = "KLIM-001",
                Quantity = 5
            },
            new()
            {
                PartNumber = "KLIM001",
                Quantity = 8
            }
        };

        var shopifyItems = new List<ShopifyInventoryItem>
        {
            new()
            {
                Sku = "KLIM-001",
                Location = "Ottawa",
                CurrentQuantity = 3
            }
        };

        var service = new SkuMatchingService();

        // Act
        var matches = service.ReconcileShopify(
            lightspeedItems,
            shopifyItems);

        // Assert
        Assert.Single(matches);

        Assert.Equal(
            SkuMatchType.Exact,
            matches[0].MatchType);

        Assert.Equal(
            5,
            matches[0].LightspeedItem!.Quantity);
    }

    [Fact]
    public void ReconcileShopify_DuplicateSkuAndLocation_ThrowsException()
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
                Sku = "KLIM-001",
                Location = "Main",
                CurrentQuantity = 3
            },
            new()
            {
                Sku = "KLIM-001",
                Location = "Main",
                CurrentQuantity = 4
            }
        };

        var service = new SkuMatchingService();

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.ReconcileShopify(
                lightspeedItems,
                shopifyItems));

        Assert.Contains("KLIM-001", exception.Message);
    }

    [Fact]
    public void ReconcileShopify_MultipleLocations_ThrowsException()
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
                Sku = "KLIM-001",
                Location = "Ottawa",
                CurrentQuantity = 3
            },
            new()
            {
                Sku = "KLIM-002",
                Location = "Toronto",
                CurrentQuantity = 2
            }
        };

        var service = new SkuMatchingService();

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.ReconcileShopify(
                lightspeedItems,
                shopifyItems));

        Assert.Contains("Multiple Shopify locations", exception.Message);
    }

    [Fact]
    public void ReconcileShopify_MissingLocation_ThrowsException()
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
                Sku = "KLIM-001",
                Location = "",
                CurrentQuantity = 3
            }
        };

        var service = new SkuMatchingService();

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.ReconcileShopify(
                lightspeedItems,
                shopifyItems));

        Assert.Contains("missing locations", exception.Message);
    }

    [Fact]
    public void ReconcileShopify_VerifiedMapping_MatchesDifferentSkus()
    {
        var service = new SkuMatchingService();

        var lightspeed = new[]
        {
            new LightspeedInventoryItem
            {
                PartNumber = "BMW-123",
                Description = "BMW Jacket",
                Quantity = 10
            }
        };

        var shopify = new[]
        {
            new ShopifyInventoryItem
            {
                Sku = "BMW-JACKET-123",
                Title = "BMW Jacket",
                Location = "Main",
                CurrentQuantity = 5
            }
        };

        var mappings = new[]
        {
            new VerifiedSkuMapping("BMW-123", "BMW-JACKET-123")
        };

        var result = service.ReconcileShopify(
            lightspeed,
            shopify,
            mappings);

        var match = Assert.Single(result);

        Assert.Equal(SkuMatchType.Verified, match.MatchType);
        Assert.Equal("BMW-123", match.LightspeedItem?.PartNumber);
    }

    [Fact]
    public void ReconcileShopify_ExactMatch_TakesPriorityOverVerified()
    {
        var service = new SkuMatchingService();

        var lightspeed = new[]
        {
            new LightspeedInventoryItem
            {
                PartNumber = "BMW-123",
                Quantity = 10
            }
        };

        var shopify = new[]
        {
            new ShopifyInventoryItem
            {
                Sku = "BMW-123",
                Location = "Main",
                CurrentQuantity = 5
            }
        };

        var mappings = new[]
        {
            new VerifiedSkuMapping("BMW-123", "BMW-123")
        };

        var result = service.ReconcileShopify(
            lightspeed,
            shopify,
            mappings);

        Assert.Equal(
            SkuMatchType.Exact,
            Assert.Single(result).MatchType);
    }

    [Fact]
    public void ReconcileShopify_ExactAndVerifiedSupplier_CombinesQuantities()
    {
        // Arrange
        var lightspeed = new[]
        {
            new LightspeedInventoryItem
            {
                PartNumber = "BMW-123",
                Quantity = 10
            },
            new LightspeedInventoryItem
            {
                PartNumber = "BMW-456",
                Quantity = 8
            }
        };

        var shopify = new[]
        {
            new ShopifyInventoryItem
            {
                Sku = "BMW-123",
                Location = "Main",
                CurrentQuantity = 5
            }
        };

        var mappings = new[]
        {
            new VerifiedSkuMapping("BMW-456", "BMW-123")
        };

        var service = new SkuMatchingService();

        // Act
        var matches = service.ReconcileShopify(
            lightspeed,
            shopify,
            mappings
        );

        // Assert
        var match = Assert.Single(matches);

        Assert.Equal(SkuMatchType.Exact, match.MatchType);
        Assert.Equal(2, match.Contributors.Count);

        Assert.Contains(
            match.Contributors,
            contributor =>
                contributor.LightspeedItem.PartNumber == "BMW-123" &&
                contributor.MatchType == SkuMatchType.Exact
        );

        Assert.Contains(
            match.Contributors,
            contributor =>
                contributor.LightspeedItem.PartNumber == "BMW-456" &&
                contributor.MatchType == SkuMatchType.Verified
        );

        Assert.Equal(18, match.CombinedQuantity);
        Assert.True(match.HasInventoryDifference);
    }

    [Fact]
    public void ReconcileShopify_DuplicateLightspeedAssignment_Throws()
    {
        var service = new SkuMatchingService();

        var lightspeed = new[]
        {
            new LightspeedInventoryItem
            {
                PartNumber = "BMW-123",
                Quantity = 10
            }
        };

        var shopify = new[]
        {
            new ShopifyInventoryItem
            {
                Sku = "BMW-123",
                Location = "Main",
                CurrentQuantity = 5
            },
            new ShopifyInventoryItem
            {
                Sku = "BMW123",
                Location = "Main",
                CurrentQuantity = 3
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            service.ReconcileShopify(
                lightspeed,
                shopify));
    }

    [Fact]
    public void ReconcileShopify_DuplicateVerifiedMappings_Throws()
    {
        var service = new SkuMatchingService();

        var lightspeed = new[]
        {
            new LightspeedInventoryItem
            {
                PartNumber = "BMW-123",
                Quantity = 10
            }
        };

        var shopify = new[]
        {
            new ShopifyInventoryItem
            {
                Sku = "SHOP-123",
                Location = "Main",
                CurrentQuantity = 5
            }
        };

        var mappings = new[]
        {
            new VerifiedSkuMapping("BMW-123", "SHOP-123"),
            new VerifiedSkuMapping("BMW-123", "SHOP-456")
        };

        Assert.Throws<InvalidOperationException>(() =>
            service.ReconcileShopify(
                lightspeed,
                shopify,
                mappings));
    }

    [Fact]
    public void ReconcileShopify_VerifiedMappingMissingLightspeed_RemainsUnmatched()
    {
        var service = new SkuMatchingService();

        var lightspeed = Array.Empty<LightspeedInventoryItem>();

        var shopify = new[]
        {
            new ShopifyInventoryItem
            {
                Sku = "SHOP-123",
                Location = "Main",
                CurrentQuantity = 5
            }
        };

        var mappings = new[]
        {
            new VerifiedSkuMapping("BMW-123", "SHOP-123")
        };

        var result = service.ReconcileShopify(
            lightspeed,
            shopify,
            mappings);

        Assert.Equal(
            SkuMatchType.Unmatched,
            Assert.Single(result).MatchType);
    }
    [Fact]
    public void GetInventoryUpdates_NormalizedMatch_IsExcluded()
    {
        var service = new SkuMatchingService();

        var matches = new[]
        {
            new InventoryMatch
            {
                MatchType = SkuMatchType.Normalized,
                LightspeedItem = new LightspeedInventoryItem
                {
                    PartNumber = "KLIM-123",
                    Quantity = 10
                },
                ShopifyItem = new ShopifyInventoryItem
                {
                    Sku = "KLIM123",
                    Location = "Main",
                    CurrentQuantity = 5
                }
            }
        };

        var updates = service.GetInventoryUpdates(matches);

        Assert.Empty(updates);
    }

    [Theory]
    [InlineData(SkuMatchType.Exact)]
    [InlineData(SkuMatchType.Verified)]
    public void GetInventoryUpdates_TrustedMatch_IsIncluded(
        SkuMatchType matchType)
    {
        var service = new SkuMatchingService();

        var matches = new[]
        {
            new InventoryMatch
            {
                MatchType = matchType,
                LightspeedItem = new LightspeedInventoryItem
                {
                    PartNumber = "BMW-123",
                    Quantity = 10
                },
                ShopifyItem = new ShopifyInventoryItem
                {
                    Sku = "BMW-JACKET-123",
                    Location = "Main",
                    CurrentQuantity = 5
                }
            }
        };

        var updates = service.GetInventoryUpdates(matches);

        var update = Assert.Single(updates);

        Assert.Equal(10, update.NewQuantity);
        Assert.Equal("BMW-JACKET-123", update.Sku);
    }

    [Fact]
    public void GetInventoryUpdates_NegativeVerifiedQuantity_IsExcluded()
    {
        var service = new SkuMatchingService();

        var matches = new[]
        {
            new InventoryMatch
            {
                MatchType = SkuMatchType.Verified,
                LightspeedItem = new LightspeedInventoryItem
                {
                    PartNumber = "BMW-456",
                    Quantity = -3
                },
                ShopifyItem = new ShopifyInventoryItem
                {
                    Sku = "BMW-JACKET-456",
                    Location = "Main",
                    CurrentQuantity = 5
                }
            }
        };

        var updates = service.GetInventoryUpdates(matches);

        Assert.Empty(updates);
    }

    [Fact]
    public void ReconcileShopify_TwoVerifiedSuppliers_CombinesQuantities()
    {
        // Arrange
        var lightspeedItems = new[]
        {
            new LightspeedInventoryItem
            {
                PartNumber = "BAT-A",
                Description = "Motorcycle Battery - Supplier A",
                Quantity = 5
            },
            new LightspeedInventoryItem
            {
                PartNumber = "BAT-B",
                Description = "Motorcycle Battery - Supplier B",
                Quantity = 3
            }
        };

        var shopifyItems = new[]
        {
            new ShopifyInventoryItem
            {
                Sku = "BATTERY-001",
                Title = "Motorcycle Battery",
                Location = "Main",
                CurrentQuantity = 6
            }
        };

        var verifiedMappings = new[]
        {
            new VerifiedSkuMapping("BAT-A", "BATTERY-001"),
            new VerifiedSkuMapping("BAT-B", "BATTERY-001")
        };

        var service = new SkuMatchingService();

        // Act
        var matches = service.ReconcileShopify(
            lightspeedItems,
            shopifyItems,
            verifiedMappings
        );

        // Assert
        var match = Assert.Single(matches);

        Assert.Equal("BATTERY-001", match.ShopifyItem?.Sku);

        Assert.Equal(SkuMatchType.Verified, match.MatchType);

        // Both Lightspeed part numbers must contribute.
        Assert.Equal(2, match.Contributors.Count);

        Assert.Contains(
            match.Contributors,
            contributor =>
                contributor.LightspeedItem.PartNumber == "BAT-A" &&
                contributor.LightspeedItem.Quantity == 5
        );

        Assert.Contains(
            match.Contributors,
            contributor =>
                contributor.LightspeedItem.PartNumber == "BAT-B" &&
                contributor.LightspeedItem.Quantity == 3
        );

        // Combined stock: 5 + 3 = 8
        Assert.Equal(8, match.CombinedQuantity);

        // Shopify currently has 6 units.
        Assert.True(match.HasInventoryDifference);
    }

    [Fact]
    public void GetInventoryUpdates_TwoVerifiedSuppliers_GeneratesOneCombinedUpdate()
    {
        // Arrange
        var lightspeed = new[]
        {
            new LightspeedInventoryItem
            {
                PartNumber = "BAT-A",
                Quantity = 5
            },
            new LightspeedInventoryItem
            {
                PartNumber = "BAT-B",
                Quantity = 3
            }
        };

        var shopify = new[]
        {
            new ShopifyInventoryItem
            {
                Sku = "BATTERY-001",
                Title = "Motorcycle Battery",
                Location = "Main",
                CurrentQuantity = 6
            }
        };

        var mappings = new[]
        {
            new VerifiedSkuMapping("BAT-A", "BATTERY-001"),
            new VerifiedSkuMapping("BAT-B", "BATTERY-001")
        };

        var service = new SkuMatchingService();

        // Act
        var matches = service.ReconcileShopify(
            lightspeed,
            shopify,
            mappings
        );

        var updates = service.GetInventoryUpdates(matches);

        // Assert
        var update = Assert.Single(updates);

        Assert.Equal("BATTERY-001", update.Sku);
        Assert.Equal(6, update.CurrentQuantity);
        Assert.Equal(8, update.NewQuantity);
    }

    [Fact]
    public void GetInventoryUpdates_MultipleSuppliersWithNegativeQuantity_ExcludesUpdate()
    {
        var lightspeed = new[]
        {
            new LightspeedInventoryItem
            {
                PartNumber = "BAT-A",
                Quantity = 5
            },
            new LightspeedInventoryItem
            {
                PartNumber = "BAT-B",
                Quantity = -2
            }
        };

        var shopify = new[]
        {
            new ShopifyInventoryItem
            {
                Sku = "BATTERY-001",
                Location = "Main",
                CurrentQuantity = 3
            }
        };

        var mappings = new[]
        {
            new VerifiedSkuMapping("BAT-A", "BATTERY-001"),
            new VerifiedSkuMapping("BAT-B", "BATTERY-001")
        };

        var service = new SkuMatchingService();

        var matches = service.ReconcileShopify(
            lightspeed,
            shopify,
            mappings
        );

        var updates = service.GetInventoryUpdates(matches);

        Assert.Empty(updates);
    }
}