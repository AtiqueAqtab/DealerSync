using DealerSync.Core.Services;
using DealerSync.Infrastructure.Importers;
using DealerSync.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Register reconciliation dependencies
builder.Services.AddTransient<LightspeedCsvImporter>();
builder.Services.AddTransient<ShopifyCsvImporter>();
builder.Services.AddTransient<SkuMatchingService>();
builder.Services.AddTransient<ReconciliationService>();

// Register API services
builder.Services.AddOpenApi();

var app = builder.Build();

// Enable OpenAPI documentation in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Health check endpoint
app.MapGet("/api/health", () =>
    {
        return Results.Ok(new
        {
            Status = "Healthy",
            Service = "DealerSync API",
            Version = "0.2.0"
        });
    })
    .WithName("GetHealth")
    .WithTags("Health");

app.MapGet("/api/reconciliation/status",
        (ReconciliationService service) =>
        {
            return Results.Ok(new
            {
                Status = "Ready",
                Message = "Reconciliation service resolved successfully"
            });
        })
    .WithName("GetReconciliationStatus")
    .WithTags("Reconciliation");

app.Run();