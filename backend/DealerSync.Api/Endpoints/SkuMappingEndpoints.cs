using DealerSync.Infrastructure.Persistence.Entities;
using DealerSync.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DealerSync.Api.Endpoints;

public static class SkuMappingEndpoints
{
    public static void MapSkuMappingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sku-mappings")
            .WithTags("SKU Mappings");

        // GET /api/sku-mappings
        group.MapGet("/", async (
            SkuMappingRepository repository) =>
        {
            var mappings = await repository.GetAllAsync();

            return Results.Ok(mappings);
        });

        // GET /api/sku-mappings/{id}
        group.MapGet("/{id:guid}", async (
            Guid id,
            SkuMappingRepository repository) =>
        {
            var mapping = await repository.GetByIdAsync(id);

            return mapping is null
                ? Results.NotFound()
                : Results.Ok(mapping);
        });

        // POST /api/sku-mappings
        group.MapPost("/", async (
            CreateSkuMappingRequest request,
            SkuMappingRepository repository) =>
        {
            // Normalize input formatting
            var lightspeedPartNumber =
                request.LightspeedPartNumber?.Trim();

            var shopifySku =
                request.ShopifySku?.Trim();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(lightspeedPartNumber) ||
                string.IsNullOrWhiteSpace(shopifySku))
            {
                return Results.BadRequest(new
                {
                    Error = "Both Lightspeed part number and Shopify SKU are required."
                });
            }

            // Validate database column limits
            if (lightspeedPartNumber.Length > 255 ||
                shopifySku.Length > 255)
            {
                return Results.BadRequest(new
                {
                    Error = "SKU identifiers cannot exceed 255 characters."
                });
            }

            // Check for existing Lightspeed mapping
            var existingLightspeed =
                await repository.GetByLightspeedPartNumberAsync(
                    lightspeedPartNumber);

            if (existingLightspeed is not null)
            {
                return Results.Conflict(new
                {
                    Error = "This Lightspeed part number is already mapped."
                });
            }


            var mapping = new SkuMapping
            {
                Id = Guid.NewGuid(),
                LightspeedPartNumber = lightspeedPartNumber,
                ShopifySku = shopifySku,
                IsVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await repository.AddAsync(mapping);

                return Results.Created(
                    $"/api/sku-mappings/{mapping.Id}",
                    mapping);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                })
            {
                // Handle concurrent requests attempting duplicate mappings
                return Results.Conflict(new
                {
                    Error = "A conflicting SKU mapping already exists."
                });
            }
        });

        // PATCH /api/sku-mappings/{id}/verify
        group.MapPatch("/{id:guid}/verify", async (
            Guid id,
            SkuMappingRepository repository) =>
        {
            var verified = await repository.VerifyAsync(id);

            if (!verified)
            {
                return Results.NotFound(new
                {
                    Error = "SKU mapping not found."
                });
            }

            var mapping = await repository.GetByIdAsync(id);

            return Results.Ok(mapping);
        });
    }
}

public sealed record CreateSkuMappingRequest(
    string? LightspeedPartNumber,
    string? ShopifySku);