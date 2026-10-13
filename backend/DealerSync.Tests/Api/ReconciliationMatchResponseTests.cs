using DealerSync.Api.DTOs;
using DealerSync.Core.Enums;
using DealerSync.Core.Models;
using Xunit;

namespace DealerSync.Tests.Api;

public class ReconciliationMatchResponseTests
{
    [Fact]
    public void FromMatch_VerifiedMatch_ReturnsCorrectDiagnostics()
    {
        var match = new InventoryMatch
        {
            LightspeedItem = new LightspeedInventoryItem
            {
                PartNumber = "BMW-123",
                Description = "BMW Jacket",
                Quantity = 10
            },
            ShopifyItem = new ShopifyInventoryItem
            {
                Sku = "BMW-JACKET-123",
                Title = "BMW Jacket",
                Location = "Main",
                CurrentQuantity = 5
            },
            MatchType = SkuMatchType.Verified
        };

        var response = ReconciliationMatchResponse.FromMatch(match);

        Assert.Equal("BMW-JACKET-123", response.ShopifySku);
        Assert.Equal("BMW-123", response.LightspeedPartNumber);
        Assert.Equal("Verified", response.MatchType);
        Assert.Equal(5, response.CurrentQuantity);
        Assert.Equal(10, response.ProposedQuantity);
        Assert.True(response.HasInventoryDifference);
        Assert.False(response.RequiresReview);
    }

    [Fact]
    public void FromMatch_UnmatchedItem_RequiresReview()
    {
        var match = new InventoryMatch
        {
            ShopifyItem = new ShopifyInventoryItem
            {
                Sku = "KLIM-999",
                Title = "Klim Jacket",
                Location = "Main",
                CurrentQuantity = 3
            },
            MatchType = SkuMatchType.Unmatched
        };

        var response = ReconciliationMatchResponse.FromMatch(match);

        Assert.Null(response.LightspeedPartNumber);
        Assert.Null(response.ProposedQuantity);
        Assert.True(response.RequiresReview);
        Assert.False(response.HasInventoryDifference);
    }

    [Fact]
    public void FromMatch_NegativeLightspeedQuantity_RequiresReview()
    {
        var match = new InventoryMatch
        {
            LightspeedItem = new LightspeedInventoryItem
            {
                PartNumber = "BMW-123",
                Quantity = -2
            },
            ShopifyItem = new ShopifyInventoryItem
            {
                Sku = "BMW-123",
                Location = "Main",
                CurrentQuantity = 5
            },
            MatchType = SkuMatchType.Exact
        };

        var response = ReconciliationMatchResponse.FromMatch(match);

        Assert.Null(response.ProposedQuantity);
        Assert.True(response.RequiresReview);
    }
}