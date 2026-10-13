namespace DealerSync.Infrastructure.Persistence.Entities;

public class SkuMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string LightspeedPartNumber { get; set; } = string.Empty;

    public string ShopifySku { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsVerified { get; set; }
}