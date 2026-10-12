using DealerSync.Api.Endpoints;
using DealerSync.Core.Services;
using DealerSync.Infrastructure.Importers;
using DealerSync.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI documentation
builder.Services.AddOpenApi();

// Register dependencies
builder.Services.AddTransient<LightspeedCsvImporter>();
builder.Services.AddTransient<ShopifyCsvImporter>();
builder.Services.AddTransient<SkuMatchingService>();
builder.Services.AddTransient<ReconciliationService>();

var app = builder.Build();

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Register API endpoints
app.MapHealthEndpoints();
app.MapReconciliationEndpoints();

app.Run();

// Expose Program for integration testing
public partial class Program { }