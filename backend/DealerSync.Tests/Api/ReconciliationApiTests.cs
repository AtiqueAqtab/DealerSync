using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Text;
using System.Text.Json;

namespace DealerSync.Tests.Api;

public class ReconciliationApiTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ReconciliationApiTests(
        WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReconciliationStatus_ReturnsReady()
    {
        var response = await _client.GetAsync(
            "/api/reconciliation/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<StatusResponse>();

        Assert.NotNull(result);
        Assert.Equal("Ready", result.Status);

    }

    private sealed class StatusResponse
    {
        public string Status { get; set; } = string.Empty;
    }

    [Fact]
    public async Task Reconciliation_ValidCsvFiles_ReturnsOk()
    {
        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            "ABC123,Test Product,10\n" +
            "XYZ456,Another Product,5\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            "ABC123,Test Product,Main,7\n" +
            "XYZ456,Another Product,Main,5\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv);

        var response = await _client.PostAsync(
            "/api/reconciliation",
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var result = json.RootElement;

        Assert.Equal(
            2,
            result.GetProperty("lightspeedItemCount").GetInt32());

        Assert.Equal(
            2,
            result.GetProperty("shopifyItemCount").GetInt32());

        Assert.Equal(
            2,
            result.GetProperty("exactMatches").GetInt32());

        Assert.Equal(
            1,
            result.GetProperty("inventoryDifferences").GetInt32());

        Assert.Equal(
            1,
            result.GetProperty("inventoryUpdatesGenerated").GetInt32());

        Assert.Equal(
            100,
            result.GetProperty("shopifyCoverage").GetDouble());
    }

    [Fact]
    public async Task Reconciliation_EmptyCsvFiles_ReturnsBadRequest()
    {
        using var content = CreateMultipartContent(
            "",
            "");

        var response = await _client.PostAsync(
            "/api/reconciliation",
            content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Reconciliation_InvalidFileExtensions_ReturnsBadRequest()
    {
        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            "ABC123,Test Product,10\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            "ABC123,Test Product,Main,7\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv,
            "lightspeed.txt",
            "shopify.txt");

        var response = await _client.PostAsync(
            "/api/reconciliation",
            content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
    [Fact]
    public async Task Reconciliation_MissingShopifyFile_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();

        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            "ABC123,Test Product,10\n";

        content.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes(lightspeedCsv)),
            "lightspeedFile",
            "lightspeed.csv");

        var response = await _client.PostAsync(
            "/api/reconciliation",
            content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
    [Fact]
    public async Task Reconciliation_InvalidCsvHeaders_ReturnsBadRequest()
    {
        var lightspeedCsv =
            "WrongHeader,Description,Qty\n" +
            "ABC123,Test Product,10\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            "ABC123,Test Product,Main,7\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv);

        var response = await _client.PostAsync(
            "/api/reconciliation",
            content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }


    private static MultipartFormDataContent CreateMultipartContent(
        string lightspeedCsv,
        string shopifyCsv,
        string lightspeedFileName = "lightspeed.csv",
        string shopifyFileName = "shopify.csv")
    {
        var content = new MultipartFormDataContent();

        var lightspeedContent = new ByteArrayContent(
            Encoding.UTF8.GetBytes(lightspeedCsv));

        var shopifyContent = new ByteArrayContent(
            Encoding.UTF8.GetBytes(shopifyCsv));

        lightspeedContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");

        shopifyContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");

        content.Add(
            lightspeedContent,
            "lightspeedFile",
            lightspeedFileName);

        content.Add(
            shopifyContent,
            "shopifyFile",
            shopifyFileName);

        return content;
    }
}