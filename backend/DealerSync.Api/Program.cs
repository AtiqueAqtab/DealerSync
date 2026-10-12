var builder = WebApplication.CreateBuilder(args);

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

app.Run();