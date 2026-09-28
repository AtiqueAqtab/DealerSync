using DealerSync.Infrastructure.Services;

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

            var service = new ReconciliationService();

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
}