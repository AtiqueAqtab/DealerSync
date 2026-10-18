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

        _exporter.Export(
            writer,
            matches,
            Array.Empty<InventoryUpdate>());

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

        _exporter.Export(
            writer,
            matches,
            Array.Empty<InventoryUpdate>());

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

        _exporter.Export(
            writer,
            matches,
            Array.Empty<InventoryUpdate>());

        var csv = writer.ToString();

        Assert.Contains("BMW-456", csv);
        Assert.Contains("True", csv);
        using var reader = new StringReader(writer.ToString());

        using var csvReader = new CsvHelper.CsvReader(
            reader,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(csvReader.Read());
        csvReader.ReadHeader();

        Assert.True(csvReader.Read());

        // Negative quantities must never be proposed as Shopify updates.
        Assert.Equal(
            "",
            csvReader.GetField("Proposed Quantity"));

        // The negative quantity remains visible for diagnostics.
        Assert.Equal(
            "-3",
            csvReader.GetField("Combined Quantity"));

        // The record must be marked invalid.
        Assert.Equal(
            "InvalidQuantity",
            csvReader.GetField("Diagnostic Status"));

        Assert.Equal(
            "True",
            csvReader.GetField("Requires Review"));
    }

    [Fact]
    public void Export_NormalizedMatch_RequiresReview()
    {
        var matches = new[]
        {
            new InventoryMatch
            {
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
                },
                MatchType = SkuMatchType.Normalized
            }
        };

        using var writer = new StringWriter();

        _exporter.Export(
            writer,
            matches,
            Array.Empty<InventoryUpdate>());

        var csv = writer.ToString();

        Assert.Contains("Diagnostic Status", csv);
        Assert.Contains("ReviewRecommended", csv);
        Assert.Contains("True", csv);
    }

    [Fact]
    public void Export_TwoVerifiedSuppliers_UsesCombinedQuantity()
    {
        var supplierA = new LightspeedInventoryItem
        {
            PartNumber = "BAT-A",
            Description = "Battery Supplier A",
            Quantity = 5
        };

        var supplierB = new LightspeedInventoryItem
        {
            PartNumber = "BAT-B",
            Description = "Battery Supplier B",
            Quantity = 3
        };

        var match = new InventoryMatch
        {
            ShopifyItem = new ShopifyInventoryItem
            {
                Sku = "BATTERY-001",
                Title = "Battery",
                Location = "Main",
                CurrentQuantity = 2
            },

            LightspeedItem = supplierA,
            MatchType = SkuMatchType.Verified,

            Contributors =
            [
                new InventoryContributor
                {
                    LightspeedItem = supplierA,
                    MatchType = SkuMatchType.Verified
                },
                new InventoryContributor
                {
                    LightspeedItem = supplierB,
                    MatchType = SkuMatchType.Verified
                }
            ]
        };

        var approvedUpdate = new InventoryUpdate
        {
            Sku = "BATTERY-001",
            Title = "Battery",
            Location = "Main",
            CurrentQuantity = 2,
            NewQuantity = 8
        };

        using var writer = new StringWriter();

        _exporter.Export(
            writer,
            new[] { match },
            new[] { approvedUpdate });

        using var reader = new StringReader(writer.ToString());

        using var csv = new CsvHelper.CsvReader(
            reader,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(csv.Read());
        csv.ReadHeader();

        Assert.True(csv.Read());

        // Both suppliers contribute to the combined quantity.
        Assert.Equal("8", csv.GetField("Combined Quantity"));

        // The approved Shopify update must also be 8.
        Assert.Equal("8", csv.GetField("Proposed Quantity"));

        // Both suppliers must appear in diagnostics.
        var contributors = csv.GetField("Contributors");

        Assert.NotNull(contributors);
        Assert.Contains("BAT-A", contributors);
        Assert.Contains("BAT-B", contributors);

        // No manual review is necessary.
        Assert.Equal("Ready", csv.GetField("Diagnostic Status"));
        Assert.Equal("False", csv.GetField("Requires Review"));
        Assert.Equal("", csv.GetField("Review Reasons"));
    }

    [Fact]
    public void Export_UnverifiedSupplier_RequiresReviewAndExcludesUpdate()
    {
        var supplierA = new LightspeedInventoryItem
        {
            PartNumber = "BAT-A",
            Description = "Battery Supplier A",
            Quantity = 5
        };

        var match = new InventoryMatch
        {
            ShopifyItem = new ShopifyInventoryItem
            {
                Sku = "BATTERY-001",
                Title = "Battery",
                Location = "Main",
                CurrentQuantity = 2
            },

            LightspeedItem = supplierA,
            MatchType = SkuMatchType.Verified,

            Contributors =
            [
                new InventoryContributor
                {
                    LightspeedItem = supplierA,
                    MatchType = SkuMatchType.Verified
                }
            ],

            ReviewReasons =
            [
                InventoryReviewReason.UnverifiedSupplier
            ]
        };

        using var writer = new StringWriter();

        // No approved updates because another supplier is unverified.
        _exporter.Export(
            writer,
            new[] { match },
            Array.Empty<InventoryUpdate>());

        using var reader = new StringReader(writer.ToString());

        using var csv = new CsvHelper.CsvReader(
            reader,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(csv.Read());
        csv.ReadHeader();

        Assert.True(csv.Read());

        // The recognized supplier contributes 5 units.
        Assert.Equal("5", csv.GetField("Combined Quantity"));

        // No inventory update is approved.
        Assert.Equal("", csv.GetField("Proposed Quantity"));

        // The product requires manual review.
        Assert.Equal(
            "ReviewRecommended",
            csv.GetField("Diagnostic Status"));

        Assert.Equal("True", csv.GetField("Requires Review"));

        Assert.Equal(
            "UnverifiedSupplier",
            csv.GetField("Review Reasons"));

        // The known supplier remains visible.
        Assert.Contains(
            "BAT-A",
            csv.GetField("Contributors")!);
    }

    [Fact]
    public void Export_MissingVerifiedSupplier_RequiresReviewAndExcludesUpdate()
    {
        var supplierA = new LightspeedInventoryItem
        {
            PartNumber = "BAT-A",
            Description = "Battery Supplier A",
            Quantity = 5
        };

        var match = new InventoryMatch
        {
            ShopifyItem = new ShopifyInventoryItem
            {
                Sku = "BATTERY-001",
                Title = "Battery",
                Location = "Main",
                CurrentQuantity = 2
            },

            LightspeedItem = supplierA,
            MatchType = SkuMatchType.Verified,

            Contributors =
            [
                new InventoryContributor
                {
                    LightspeedItem = supplierA,
                    MatchType = SkuMatchType.Verified
                }
            ],

            ReviewReasons =
            [
                InventoryReviewReason.MissingSupplierInventory
            ]
        };

        using var writer = new StringWriter();

        // BAT-B is verified but absent from the uploaded
        // Lightspeed inventory, so no update is approved.
        _exporter.Export(
            writer,
            new[] { match },
            Array.Empty<InventoryUpdate>());

        using var reader = new StringReader(writer.ToString());

        using var csv = new CsvHelper.CsvReader(
            reader,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(csv.Read());
        csv.ReadHeader();

        Assert.True(csv.Read());

        // Only BAT-A contributed a known quantity.
        Assert.Equal(
            "5",
            csv.GetField("Combined Quantity"));

        // The incomplete quantity cannot be exported.
        Assert.Equal(
            "",
            csv.GetField("Proposed Quantity"));

        Assert.Equal(
            "ReviewRecommended",
            csv.GetField("Diagnostic Status"));

        Assert.Equal(
            "True",
            csv.GetField("Requires Review"));

        Assert.Equal(
            "MissingSupplierInventory",
            csv.GetField("Review Reasons"));

        Assert.Contains(
            "BAT-A",
            csv.GetField("Contributors")!);
    }

}