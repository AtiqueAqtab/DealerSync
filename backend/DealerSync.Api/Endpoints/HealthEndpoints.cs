namespace DealerSync.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
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
    }
}