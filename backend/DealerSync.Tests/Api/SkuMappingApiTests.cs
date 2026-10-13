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

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
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
}