using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CsvHelper;
using DealerSync.Tests.Fixtures;

namespace DealerSync.Tests.Api;

public class ApprovedShopifyExportApiTests : IClassFixture<DealerSyncApiFactory>
{
    private readonly HttpClient _client;
    private const string Header = "Handle,Title,Option1 Name,Option1 Value,Option2 Name,Option2 Value,Option3 Name,Option3 Value,SKU,Location,On hand (current)\n";

    public ApprovedShopifyExportApiTests(DealerSyncApiFactory factory) => _client = factory.CreateClient();

    private async Task Map(string part, string sku, bool verify)
    {
        using var created = await _client.PostAsJsonAsync("/api/sku-mappings", new {lightspeedPartNumber = part, shopifySku = sku});
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        if (!verify) return;
        using var verified = await _client.PatchAsync($"/api/sku-mappings/{json.RootElement.GetProperty("id").GetString()}/verify", null);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
    }

    private async Task<HttpResponseMessage> Reconcile(string lightspeed, string shopify)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(lightspeed)), "lightspeedFile", "lightspeed.csv");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(shopify)), "shopifyFile", "shopify.csv");
        return await _client.PostAsync("/api/reconciliation?detailed=true", content);
    }

    [Theory]
    [InlineData(true, 7)]
    [InlineData(false, null)]
    public async Task Export_UsesTheSameSavedMappingsAndApprovalAsTheDetailedResponse(bool verifySecond, int? approved)
    {
        var sku = "EXPORT-" + Guid.NewGuid().ToString("N");
        await Map(sku + "-A", sku, true);
        await Map(sku + "-B", sku, verifySecond);
        using var response = await Reconcile($"Part Number,Description,Qty\n{sku}-A,A,3\n{sku}-B,B,4\n",
            Header + $"synthetic-product,Product,Color,Blue,Size,L,Finish,Matte,{sku},Ottawa East,2\n");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var export = root.GetProperty("approvedInventoryExport");
        var match = root.GetProperty("matches")[0];
        Assert.Equal(verifySecond ? 1 : 0, export.GetProperty("updateCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, export.GetProperty("error").ValueKind);
        if (approved is null)
        {
            Assert.Equal(JsonValueKind.Null, match.GetProperty("proposedQuantity").ValueKind);
            Assert.Equal(JsonValueKind.Null, export.GetProperty("csv").ValueKind);
            return;
        }
        Assert.Equal(approved.Value, match.GetProperty("proposedQuantity").GetInt32());
        using var csv = new CsvReader(new StringReader(export.GetProperty("csv").GetString()!), CultureInfo.InvariantCulture);
        csv.Read(); csv.ReadHeader(); Assert.True(csv.Read());
        Assert.Equal(sku, csv.GetField("SKU"));
        Assert.Equal("synthetic-product", csv.GetField("Handle"));
        Assert.Equal("Blue", csv.GetField("Option 1 Value"));
        Assert.Equal("L", csv.GetField("Option 2 Value"));
        Assert.Equal("Matte", csv.GetField("Option 3 Value"));
        Assert.Equal("Ottawa East", csv.GetField("Location"));
        Assert.Equal("2", csv.GetField("On hand (current)"));
        Assert.Equal("7", csv.GetField("On hand (new)"));
        Assert.False(csv.Read());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Export_DistinctVariantsSharingSkuAndLocationRejectTheEntireReconciliation(
        bool changeCase, bool surroundWithWhitespace)
    {
        var sku = "JACKET-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var part = sku + "-SUPPLIER";
        var unrelatedSku = sku + "-OTHER";
        await Map(part, sku, true);
        var lightspeed = $"Part Number,Description,Qty\n{part},Jacket,7\n{unrelatedSku},Other,9\n";
        var validRows = $"jacket-red,Red jacket,Color,Red,,,,,{sku},Ottawa,2\n" +
                        $"other-product,Other,Title,Default Title,,,,,{unrelatedSku},Ottawa,1\n";

        // Prove the saved mapping authorizes an export before introducing ambiguity.
        using var control = await Reconcile(lightspeed, Header + validRows);
        Assert.Equal(HttpStatusCode.OK, control.StatusCode);
        using var controlJson = JsonDocument.Parse(await control.Content.ReadAsStringAsync());
        var mappedMatch = controlJson.RootElement.GetProperty("matches").EnumerateArray()
            .Single(match => match.GetProperty("shopifySku").GetString() == sku);
        Assert.Equal("Verified", mappedMatch.GetProperty("matchType").GetString());
        Assert.Equal(7, mappedMatch.GetProperty("proposedQuantity").GetInt32());
        var controlExport = controlJson.RootElement.GetProperty("approvedInventoryExport");
        Assert.Equal(2, controlExport.GetProperty("updateCount").GetInt32());
        Assert.False(string.IsNullOrEmpty(controlExport.GetProperty("csv").GetString()));

        var duplicateSku = changeCase ? sku.ToLowerInvariant() : sku;
        if (surroundWithWhitespace) duplicateSku = $"  {duplicateSku}  ";
        using var response = await Reconcile(lightspeed, Header + validRows +
            $"jacket-blue,Blue jacket,Color,Blue,,,,,{duplicateSku},Ottawa,2\n");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Inventory reconciliation failed due to conflicting data.",
            json.RootElement.GetProperty("error").GetString());
        Assert.False(json.RootElement.TryGetProperty("approvedInventoryExport", out _));
        Assert.False(json.RootElement.TryGetProperty("summary", out _));
        Assert.False(json.RootElement.TryGetProperty("matches", out _));
    }

    [Fact]
    public async Task Export_MissingIdentityBlocksTheEntireFileWithoutHidingApprovals()
    {
        var sku = "EXPORT-" + Guid.NewGuid().ToString("N");
        using var response = await Reconcile($"Part Number,Description,Qty\n{sku}-A,A,7\n{sku}-B,B,4\n",
            Header + $"product-a,A,Title,Default Title,,,,,{sku}-A,Ottawa,2\n" +
            $"product-b,B,Title,,,,,,{sku}-B,Ottawa,2\n");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("summary").GetProperty("inventoryUpdatesGenerated").GetInt32());
        var export = json.RootElement.GetProperty("approvedInventoryExport");
        Assert.Equal(2, export.GetProperty("updateCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, export.GetProperty("csv").ValueKind);
        Assert.StartsWith("Shopify export blocked:", export.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Export_OverflowRejectsReconciliationWithoutAnyDownloadPayload()
    {
        var sku = "EXPORT-" + Guid.NewGuid().ToString("N");
        await Map(sku + "-A", sku, true);
        await Map(sku + "-B", sku, true);
        using var response = await Reconcile($"Part Number,Description,Qty\n{sku}-A,A,2147483647\n{sku}-B,B,1\n",
            Header + $"product,Product,Title,Default Title,,,,,{sku},Ottawa,2\n");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.TryGetProperty("approvedInventoryExport", out _));
    }
}
