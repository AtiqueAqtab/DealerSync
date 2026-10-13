using DealerSync.Core.Services;
using DealerSync.Infrastructure.Importers;
using DealerSync.Infrastructure.Services;
using Xunit;

namespace DealerSync.Tests.Services;

public class ReconciliationServiceTests
{
    [Fact]
    public void Run_ShouldReconcileInventoryFiles()
    {
        var lightspeedFile = Path.GetTempFileName();
        var shopifyFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(
                lightspeedFile,
                """
                Part Number,Description,Qty
                TEST-001,Test Product,5
                """);

            File.WriteAllText(
                shopifyFile,
                """
                SKU,Title,Location,On hand (current)
                TEST-001,Test Product,Ottawa,2
                """);

            var service = new ReconciliationService(
                new LightspeedCsvImporter(),
                new ShopifyCsvImporter(),
                new SkuMatchingService()
            );

            var result = service.Run(
                lightspeedFile,
                shopifyFile);

            Assert.Equal(1, result.LightspeedItemCount);
            Assert.Equal(1, result.ShopifyItemCount);

            Assert.Equal(1, result.ExactMatches);
            Assert.Equal(0, result.Unmatched);

            Assert.Equal(1, result.InventoryDifferences);
            Assert.Equal(1, result.InventoryUpdatesGenerated);

            Assert.Equal(100, result.ShopifyCoverage);
        }
        finally
        {
            File.Delete(lightspeedFile);
            File.Delete(shopifyFile);
        }
    }

    [Fact]
    public void Run_NormalizedMatchWithQuantityDifference_CountsExcludedUpdate()
    {
        var service = new ReconciliationService(
            new LightspeedCsvImporter(),
            new ShopifyCsvImporter(),
            new SkuMatchingService());

        using var lightspeedReader = new StringReader(
            "Part Number,Description,Qty\n" +
            "KLIM-123,Test Jacket,10\n");

        using var shopifyReader = new StringReader(
            "SKU,Title,Location,On hand (current)\n" +
            "KLIM123,Test Jacket,Main,5\n");

        var result = service.Run(
            lightspeedReader,
            shopifyReader);

        Assert.Equal(1, result.NormalizedMatches);
        Assert.Equal(1, result.InventoryDifferences);
        Assert.Equal(1, result.NormalizedUpdatesExcluded);
        Assert.Equal(0, result.InventoryUpdatesGenerated);
        Assert.Equal(100, result.ShopifyCoverage);
    }
}