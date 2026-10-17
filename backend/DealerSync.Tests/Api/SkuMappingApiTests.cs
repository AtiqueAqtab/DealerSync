using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DealerSync.Tests.Fixtures;
using Xunit;

namespace DealerSync.Tests.Api;

public class SkuMappingApiTests
    : IClassFixture<DealerSyncApiFactory>
{
    private readonly HttpClient _client;

    public SkuMappingApiTests(DealerSyncApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateMapping_ValidRequest_ReturnsCreated()
    {
        var request = new
        {
            LightspeedPartNumber = "TEST-LS-001",
            ShopifySku = "TEST-SH-001"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var mapping = json.RootElement;

        Assert.Equal(
            "TEST-LS-001",
            mapping.GetProperty("lightspeedPartNumber").GetString());

        Assert.Equal(
            "TEST-SH-001",
            mapping.GetProperty("shopifySku").GetString());

        Assert.False(
            mapping.GetProperty("isVerified").GetBoolean());

        Assert.NotEqual(
            Guid.Empty,
            mapping.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetMappings_ReturnsCreatedMapping()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var request = new
        {
            LightspeedPartNumber = $"LS-{suffix}",
            ShopifySku = $"SH-{suffix}"
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var created = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var id = created.RootElement.GetProperty("id").GetGuid();

        var response = await _client.GetAsync(
            "/api/sku-mappings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Contains(
            json.RootElement.EnumerateArray(),
            mapping => mapping.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task GetMapping_ExistingId_ReturnsMapping()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var request = new
        {
            LightspeedPartNumber = $"LS-{suffix}",
            ShopifySku = $"SH-{suffix}"
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var created = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var id = created.RootElement.GetProperty("id").GetGuid();

        var response = await _client.GetAsync(
            $"/api/sku-mappings/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            id,
            json.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetMapping_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/sku-mappings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateMapping_EmptySku_ReturnsBadRequest()
    {
        var request = new
        {
            LightspeedPartNumber = "TEST-LS-EMPTY",
            ShopifySku = "   "
        };

        var response = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateMapping_DuplicateLightspeed_ReturnsConflict()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var first = new
        {
            LightspeedPartNumber = $"LS-{suffix}",
            ShopifySku = $"SH-A-{suffix}"
        };

        var second = new
        {
            LightspeedPartNumber = $"LS-{suffix}",
            ShopifySku = $"SH-B-{suffix}"
        };

        var firstResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            first);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            second);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task CreateMapping_DuplicateShopify_ReturnsConflict()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var first = new
        {
            LightspeedPartNumber = $"LS-A-{suffix}",
            ShopifySku = $"SH-{suffix}"
        };

        var second = new
        {
            LightspeedPartNumber = $"LS-B-{suffix}",
            ShopifySku = $"SH-{suffix}"
        };

        var firstResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            first);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            second);

        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
    }

    [Fact]
    public async Task CreateMapping_CaseInsensitiveDuplicate_ReturnsConflict()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var first = new
        {
            LightspeedPartNumber = $"KLIM-{suffix}",
            ShopifySku = $"SHOP-A-{suffix}"
        };

        var second = new
        {
            LightspeedPartNumber = $"klim-{suffix}",
            ShopifySku = $"SHOP-B-{suffix}"
        };

        var firstResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            first);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            second);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task CreateMapping_TrimsWhitespace()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var request = new
        {
            LightspeedPartNumber = $"  LS-{suffix}  ",
            ShopifySku = $"  SH-{suffix}  "
        };

        var response = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            $"LS-{suffix}",
            json.RootElement.GetProperty("lightspeedPartNumber").GetString());

        Assert.Equal(
            $"SH-{suffix}",
            json.RootElement.GetProperty("shopifySku").GetString());
    }

    [Fact]
    public async Task VerifyMapping_ExistingMapping_ReturnsVerified()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");

        var request = new
        {
            LightspeedPartNumber = $"LS-{suffix}",
            ShopifySku = $"SH-{suffix}"
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var created = createdJson.RootElement;

        var id = created.GetProperty("id").GetGuid();

        Assert.False(
            created.GetProperty("isVerified").GetBoolean());

        // Act
        var verifyResponse = await _client.PatchAsync(
            $"/api/sku-mappings/{id}/verify",
            null);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            verifyResponse.StatusCode);

        using var verifiedJson = JsonDocument.Parse(
            await verifyResponse.Content.ReadAsStringAsync());

        Assert.True(
            verifiedJson.RootElement
                .GetProperty("isVerified")
                .GetBoolean());

        // Verify the change persisted in PostgreSQL
        var getResponse = await _client.GetAsync(
            $"/api/sku-mappings/{id}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var persistedJson = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        Assert.True(
            persistedJson.RootElement
                .GetProperty("isVerified")
                .GetBoolean());
    }

    [Fact]
    public async Task VerifyMapping_UnknownId_ReturnsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var response = await _client.PatchAsync(
            $"/api/sku-mappings/{unknownId}/verify",
            null);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }


    [Fact]
    public async Task VerifiedMapping_ChangesReconciliationResult()
    {
        // Arrange: Use unique SKUs to avoid test interference
        var suffix = Guid.NewGuid().ToString("N");

        var lightspeedSku = $"BMW-{suffix}";
        var shopifySku = $"JACKET-{suffix}";

        // Create an unverified mapping
        var createResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            new
            {
                LightspeedPartNumber = lightspeedSku,
                ShopifySku = shopifySku
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var mappingId = createdJson.RootElement
            .GetProperty("id").GetGuid();

        // Prepare CSV files with different SKUs
        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            $"{lightspeedSku},BMW Jacket,10\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            $"{shopifySku},BMW Jacket,Main,5\n";

        // Helper to upload fresh multipart content for each request
        async Task<JsonDocument> ReconcileAsync()
        {
            using var content = new MultipartFormDataContent();

            content.Add(
                new ByteArrayContent(
                    System.Text.Encoding.UTF8.GetBytes(lightspeedCsv)),
                "lightspeedFile",
                "lightspeed.csv");

            content.Add(
                new ByteArrayContent(
                    System.Text.Encoding.UTF8.GetBytes(shopifyCsv)),
                "shopifyFile",
                "shopify.csv");

            using var response = await _client.PostAsync(
                "/api/reconciliation",
                content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());
        }

        // Act 1: Reconcile before verification
        using (var before = await ReconcileAsync())
        {
            Assert.Equal(
                0,
                before.RootElement.GetProperty("verifiedMatches").GetInt32());

            Assert.Equal(
                1,
                before.RootElement.GetProperty("unmatched").GetInt32());

            Assert.Equal(
                0,
                before.RootElement.GetProperty("shopifyCoverage").GetDouble());
        }

        // Verify the mapping
        var verifyResponse = await _client.PatchAsync(
            $"/api/sku-mappings/{mappingId}/verify",
            null);

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        // Act 2: Reconcile after verification
        using (var after = await ReconcileAsync())
        {
            Assert.Equal(
                1,
                after.RootElement.GetProperty("verifiedMatches").GetInt32());

            Assert.Equal(
                0,
                after.RootElement.GetProperty("unmatched").GetInt32());

            Assert.Equal(
                100,
                after.RootElement.GetProperty("shopifyCoverage").GetDouble());

            Assert.Equal(
                1,
                after.RootElement
                    .GetProperty("inventoryUpdatesGenerated").GetInt32());
        }

        // Assert: The latest reconciliation was saved to PostgreSQL
        using var historyResponse = await _client.GetAsync(
            "/api/reconciliation/history");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        using var historyJson = JsonDocument.Parse(
            await historyResponse.Content.ReadAsStringAsync());

        Assert.Contains(
            historyJson.RootElement.EnumerateArray(),
            run => run.GetProperty("verifiedMatches").GetInt32() == 1);
    }

    [Fact]
    public async Task VerifiedMapping_WithExactMatch_CombinesInventory()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");

        var exactSku = $"EXACT-{suffix}";
        var mappedSku = $"MAPPED-{suffix}";

        // Create an additional supplier mapping.
        var createResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            new
            {
                LightspeedPartNumber = mappedSku,
                ShopifySku = exactSku
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var mappingId = createdJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Verify the additional supplier mapping.
        using var verifyResponse = await _client.PatchAsync(
            $"/api/sku-mappings/{mappingId}/verify",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            verifyResponse.StatusCode);

        // Two Lightspeed part numbers represent one product.
        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            $"{exactSku},Exact Product,10\n" +
            $"{mappedSku},Additional Supplier,8\n";

        // Shopify has one inventory record.
        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            $"{exactSku},Exact Product,Main,5\n";

        using var content = new MultipartFormDataContent();

        content.Add(
            new ByteArrayContent(
                System.Text.Encoding.UTF8.GetBytes(lightspeedCsv)),
            "lightspeedFile",
            "lightspeed.csv");

        content.Add(
            new ByteArrayContent(
                System.Text.Encoding.UTF8.GetBytes(shopifyCsv)),
            "shopifyFile",
            "shopify.csv");

        // Act
        using var response = await _client.PostAsync(
            "/api/reconciliation",
            content);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        using var responseJson = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var summary = responseJson.RootElement;

        // One Shopify product, despite two Lightspeed contributors.
        Assert.Equal(
            1,
            summary.GetProperty("shopifyItemCount").GetInt32());

        Assert.Equal(
            2,
            summary.GetProperty("lightspeedItemCount").GetInt32());

        // The exact match remains the overall match type.
        Assert.Equal(
            1,
            summary.GetProperty("exactMatches").GetInt32());

        Assert.Equal(
            0,
        summary.GetProperty("unmatched").GetInt32());

        // Shopify currently has 5; combined Lightspeed quantity is 18.
        Assert.Equal(
            1,
            summary.GetProperty("inventoryDifferences").GetInt32());

        // Only one inventory update is generated.
        Assert.Equal(
            1,
            summary.GetProperty("inventoryUpdatesGenerated").GetInt32());

        Assert.Equal(
            100,
            summary.GetProperty("shopifyCoverage").GetDouble());
    }

    [Fact]
    public async Task CreateMappings_MultipleSuppliersForSameShopifySku_Succeeds()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");

        var shopifySku = $"BATTERY-{suffix}";
        var supplierA = $"BAT-A-{suffix}";
        var supplierB = $"BAT-B-{suffix}";

        // Act: Create the first supplier mapping.
        using var firstResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            new
            {
                LightspeedPartNumber = supplierA,
                ShopifySku = shopifySku
            });

        // Create a second supplier mapping to the same Shopify SKU.
        using var secondResponse = await _client.PostAsJsonAsync(
            "/api/sku-mappings",
            new
            {
                LightspeedPartNumber = supplierB,
                ShopifySku = shopifySku
            });

        // Assert: Both mappings should be created.
        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Created,
            secondResponse.StatusCode);

        using var firstJson = JsonDocument.Parse(
            await firstResponse.Content.ReadAsStringAsync());

        using var secondJson = JsonDocument.Parse(
            await secondResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            shopifySku,
            firstJson.RootElement.GetProperty("shopifySku").GetString());

        Assert.Equal(
            shopifySku,
            secondJson.RootElement.GetProperty("shopifySku").GetString());

        Assert.Equal(
            supplierA,
            firstJson.RootElement.GetProperty("lightspeedPartNumber").GetString());

        Assert.Equal(
            supplierB,
            secondJson.RootElement.GetProperty("lightspeedPartNumber").GetString());

        // Both mappings must initially be unverified.
        Assert.False(
            firstJson.RootElement.GetProperty("isVerified").GetBoolean());

        Assert.False(
            secondJson.RootElement.GetProperty("isVerified").GetBoolean());

        // Confirm that both mappings were persisted.
        using var listResponse = await _client.GetAsync(
            "/api/sku-mappings");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listJson = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync());

        var matchingMappings = listJson.RootElement
            .EnumerateArray()
            .Where(mapping =>
                mapping.GetProperty("shopifySku").GetString() == shopifySku)
            .ToList();

        Assert.Equal(2, matchingMappings.Count);
    }
}