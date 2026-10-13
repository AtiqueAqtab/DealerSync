using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DealerSync.Tests.Fixtures;
using Xunit;

namespace DealerSync.Tests.Api;

public class ReconciliationApiTests
    : IClassFixture<DealerSyncApiFactory>
{
    private readonly HttpClient _client;

    public ReconciliationApiTests(
        DealerSyncApiFactory factory)
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
    [Fact]
    public async Task Reconciliation_InvalidShopifyQuantity_ReturnsBadRequest()
    {
        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            "ABC123,Test Product,10\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            "ABC123,Test Product,Main,invalid\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv);

        var response = await _client.PostAsync(
            "/api/reconciliation",
            content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Contains("Invalid quantity", responseBody);
        Assert.Contains("ABC123", responseBody);
    }
    [Fact]
    public async Task Reconciliation_InvalidLightspeedQuantity_ReturnsBadRequest()
    {
        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            "ABC123,Test Product,invalid\n";

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

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Contains("Invalid quantity", responseBody);
        Assert.Contains("ABC123", responseBody);
    }

    [Fact]
    public async Task Reconciliation_ValidCsvFiles_SavesHistoryToDatabase()
    {
        // Arrange
        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            "HISTORY-001,Test Jacket,10\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            "HISTORY-001,Test Jacket,Main,7\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv);

        // Act: Run reconciliation
        var response = await _client.PostAsync(
            "/api/reconciliation",
            content);

        // Assert: Reconciliation succeeded
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Retrieve reconciliation history
        var historyResponse = await _client.GetAsync(
            "/api/reconciliation/history");

        Assert.Equal(
            HttpStatusCode.OK,
            historyResponse.StatusCode);

        using var json = JsonDocument.Parse(
            await historyResponse.Content.ReadAsStringAsync());

        var runs = json.RootElement;

        Assert.Equal(
            JsonValueKind.Array,
            runs.ValueKind);

        // Verify that our reconciliation was saved
        Assert.Contains(
            runs.EnumerateArray(),
            run =>
                run.GetProperty("lightspeedItemCount").GetInt32() == 1 &&
                run.GetProperty("shopifyItemCount").GetInt32() == 1 &&
                run.GetProperty("exactMatches").GetInt32() == 1 &&
                run.GetProperty("inventoryDifferences").GetInt32() == 1 &&
                run.GetProperty("inventoryUpdatesGenerated").GetInt32() == 1);
    }

    [Fact]
    public async Task ReconciliationHistory_ExistingId_ReturnsRun()
    {
        // Arrange: Save a reconciliation
        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            "HISTORY-002,Test Helmet,5\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            "HISTORY-002,Test Helmet,Main,3\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv);

        var postResponse = await _client.PostAsync(
            "/api/reconciliation",
            content);

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        // Retrieve history
        var historyResponse = await _client.GetAsync(
            "/api/reconciliation/history");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        using var historyJson = JsonDocument.Parse(
            await historyResponse.Content.ReadAsStringAsync());

        var run = historyJson.RootElement
            .EnumerateArray()
            .First(item =>
                item.GetProperty("lightspeedItemCount").GetInt32() == 1 &&
                item.GetProperty("shopifyItemCount").GetInt32() == 1 &&
                item.GetProperty("exactMatches").GetInt32() == 1);

        var id = run.GetProperty("id").GetGuid();

        // Act: Retrieve the individual run
        var response = await _client.GetAsync(
            $"/api/reconciliation/history/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            id,
            json.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ReconciliationHistory_UnknownId_ReturnsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var response = await _client.GetAsync(
            $"/api/reconciliation/history/{unknownId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Reconciliation_InvalidCsv_DoesNotSaveHistory()
    {
        // Arrange: Record how many runs currently exist
        var beforeResponse = await _client.GetAsync(
            "/api/reconciliation/history");

        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);

        using var beforeJson = JsonDocument.Parse(
            await beforeResponse.Content.ReadAsStringAsync());

        var countBefore = beforeJson.RootElement.GetArrayLength();

        // Invalid Lightspeed quantity
        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            "INVALID-001,Test Jacket,invalid\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            "INVALID-001,Test Jacket,Main,5\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv);

        // Act
        var response = await _client.PostAsync(
            "/api/reconciliation",
            content);

        // Assert: Invalid inventory data is rejected
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        // Verify no additional history record was created
        var afterResponse = await _client.GetAsync(
            "/api/reconciliation/history");

        Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);

        using var afterJson = JsonDocument.Parse(
            await afterResponse.Content.ReadAsStringAsync());

        var countAfter = afterJson.RootElement.GetArrayLength();

        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task Reconciliation_Detailed_ReturnsIndividualMatches()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var matchedSku = $"MATCH-{suffix}";
        var unmatchedSku = $"UNMATCHED-{suffix}";

        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            $"{matchedSku},Test Jacket,10\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            $"{matchedSku},Test Jacket,Main,5\n" +
            $"{unmatchedSku},Test Helmet,Main,3\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv);
        using var response = await _client.PostAsync(
            "/api/reconciliation?detailed=true",
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        // Verify summary statistics
        var summary = root.GetProperty("summary");

        Assert.Equal(
            1,
            summary.GetProperty("exactMatches").GetInt32());

        Assert.Equal(
            1,
            summary.GetProperty("unmatched").GetInt32());

        Assert.Equal(
            50,
            summary.GetProperty("shopifyCoverage").GetDouble());

        // Verify individual matches
        var matches = root.GetProperty("matches");

        Assert.Equal(2, matches.GetArrayLength());

        var matched = matches.EnumerateArray()
            .Single(item =>
                item.GetProperty("shopifySku").GetString() == matchedSku);

        Assert.Equal(
            "Exact",
            matched.GetProperty("matchType").GetString());

        Assert.Equal(
            10,
            matched.GetProperty("proposedQuantity").GetInt32());

        var unmatched = matches.EnumerateArray()
            .Single(item =>
                item.GetProperty("shopifySku").GetString() == unmatchedSku);

        Assert.Equal(
            "Unmatched",
            unmatched.GetProperty("matchType").GetString());

        Assert.True(
            unmatched.GetProperty("requiresReview").GetBoolean());

        Assert.Equal(
            JsonValueKind.Null,
            unmatched.GetProperty("proposedQuantity").ValueKind);
    }

    [Fact]
    public async Task Reconciliation_CsvFormat_ReturnsDownloadableDiagnostics()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");

        var matchedSku = $"MATCH-{suffix}";
        var unmatchedSku = $"UNMATCHED-{suffix}";

        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            $"{matchedSku},Test Jacket,10\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            $"{matchedSku},Test Jacket,Main,5\n" +
            $"{unmatchedSku},Test Helmet,Main,3\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv);

        // Act
        using var response = await _client.PostAsync(
            "/api/reconciliation?format=csv",
            content);

        // Assert: Successful CSV response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(
            "text/csv",
            response.Content.Headers.ContentType?.MediaType);

        var disposition =
            response.Content.Headers.ContentDisposition;

        Assert.NotNull(disposition);

        Assert.Contains(
            "dealersync_diagnostics.csv",
            disposition.FileName ?? disposition.FileNameStar ?? "");

        // Verify CSV contents
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Contains("Shopify SKU", csv);
        Assert.Contains("Lightspeed Part Number", csv);

        Assert.Contains(matchedSku, csv);
        Assert.Contains(unmatchedSku, csv);

        Assert.Contains("Exact", csv);
        Assert.Contains("Unmatched", csv);
    }

    [Fact]
    public async Task Reconciliation_NormalizedMatch_ExcludedFromInventoryUpdates()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var lightspeedSku = $"KLIM-{suffix}";
        var shopifySku = $"KLIM{suffix}";

        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            $"{lightspeedSku},Test Jacket,10\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            $"{shopifySku},Test Jacket,Main,5\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv);

        using var response = await _client.PostAsync(
            "/api/reconciliation?detailed=true",
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var summary = json.RootElement.GetProperty("summary");

        Assert.Equal(1, summary.GetProperty("normalizedMatches").GetInt32());
        Assert.Equal(0, summary.GetProperty("verifiedMatches").GetInt32());
        Assert.Equal(1, summary.GetProperty("normalizedUpdatesExcluded").GetInt32());
        Assert.Equal(0, summary.GetProperty("inventoryUpdatesGenerated").GetInt32());
        Assert.Equal(100, summary.GetProperty("shopifyCoverage").GetDouble());

        var match = Assert.Single(
            json.RootElement.GetProperty("matches").EnumerateArray());

        Assert.Equal(
            "Normalized",
            match.GetProperty("matchType").GetString());

        Assert.Equal(
            "ReviewRecommended",
            match.GetProperty("diagnosticStatus").GetString());

        Assert.True(match.GetProperty("requiresReview").GetBoolean());
    }

    [Fact]
    public async Task Reconciliation_NormalizedExclusion_IsSavedToHistory()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var lightspeedSku = $"LS-{suffix}";
        var shopifySku = $"LS{suffix}";

        var lightspeedCsv =
            "Part Number,Description,Qty\n" +
            $"{lightspeedSku},Test Product,10\n";

        var shopifyCsv =
            "SKU,Title,Location,On hand (current)\n" +
            $"{shopifySku},Test Product,Main,5\n";

        using var content = CreateMultipartContent(
            lightspeedCsv,
            shopifyCsv);

        using var response = await _client.PostAsync(
            "/api/reconciliation",
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var historyResponse = await _client.GetAsync(
            "/api/reconciliation/history");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        using var json = JsonDocument.Parse(
            await historyResponse.Content.ReadAsStringAsync());

        Assert.Contains(
            json.RootElement.EnumerateArray(),
            run =>
                run.GetProperty("normalizedMatches").GetInt32() == 1 &&
                run.GetProperty("normalizedUpdatesExcluded").GetInt32() == 1 &&
                run.GetProperty("inventoryUpdatesGenerated").GetInt32() == 0);
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