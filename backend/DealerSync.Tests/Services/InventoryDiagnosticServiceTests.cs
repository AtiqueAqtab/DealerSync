using DealerSync.Core.Enums;
using DealerSync.Core.Models;
using DealerSync.Core.Services;
using Xunit;

namespace DealerSync.Tests.Services;

public class InventoryDiagnosticServiceTests
{
    private readonly InventoryDiagnosticService _service = new();

    [Theory]
    [InlineData(SkuMatchType.Exact, DiagnosticStatus.Ready)]
    [InlineData(SkuMatchType.Verified, DiagnosticStatus.Ready)]
    [InlineData(SkuMatchType.Normalized, DiagnosticStatus.ReviewRecommended)]
    [InlineData(SkuMatchType.Unmatched, DiagnosticStatus.MappingRequired)]
    public void GetStatus_ClassifiesMatchCorrectly(
        SkuMatchType matchType,
        DiagnosticStatus expected)
    {
        var match = new InventoryMatch
        {
            MatchType = matchType,
            ShopifyItem = new ShopifyInventoryItem
            {
                Sku = "SHOP-123",
                Location = "Main",
                CurrentQuantity = 5
            },
            LightspeedItem = matchType == SkuMatchType.Unmatched
                ? null
                : new LightspeedInventoryItem
                {
                    PartNumber = "LS-123",
                    Quantity = 10
                }
        };

        Assert.Equal(expected, _service.GetStatus(match));
    }

    [Fact]
    public void GetStatus_NegativeQuantity_ReturnsInvalidQuantity()
    {
        var match = new InventoryMatch
        {
            MatchType = SkuMatchType.Exact,
            LightspeedItem = new LightspeedInventoryItem
            {
                PartNumber = "LS-123",
                Quantity = -3
            },
            ShopifyItem = new ShopifyInventoryItem
            {
                Sku = "SHOP-123",
                Location = "Main",
                CurrentQuantity = 5
            }
        };

        Assert.Equal(
            DiagnosticStatus.InvalidQuantity,
            _service.GetStatus(match));

        Assert.True(_service.RequiresReview(match));
    }
}