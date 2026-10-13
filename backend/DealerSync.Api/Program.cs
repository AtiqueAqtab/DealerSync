using DealerSync.Api.Endpoints;
using DealerSync.Core.Services;
using DealerSync.Infrastructure.Importers;
using DealerSync.Infrastructure.Services;
using DealerSync.Infrastructure.Persistence;
using DealerSync.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure PostgreSQL database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException(
                           "Database connection string 'DefaultConnection' is missing.");

builder.Services.AddDbContext<DealerSyncDbContext>(options =>
    options.UseNpgsql(connectionString));

// OpenAPI documentation
builder.Services.AddOpenApi();

// Register dependencies
builder.Services.AddTransient<LightspeedCsvImporter>();
builder.Services.AddTransient<ShopifyCsvImporter>();
builder.Services.AddTransient<SkuMatchingService>();
builder.Services.AddTransient<ReconciliationService>();
builder.Services.AddScoped<ReconciliationHistoryRepository>();
builder.Services.AddScoped<SkuMappingRepository>();

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
app.MapSkuMappingEndpoints();

app.Run();

// Expose Program for integration testing
public partial class Program { }