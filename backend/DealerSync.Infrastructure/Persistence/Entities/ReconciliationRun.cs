namespace DealerSync.Infrastructure.Persistence.Entities;

public class ReconciliationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int LightspeedItemCount { get; set; }

    public int ShopifyItemCount { get; set; }
    
    public int VerifiedMatches { get; set; }

    public int ExactMatches { get; set; }

    public int NormalizedMatches { get; set; }

    public int Unmatched { get; set; }

    public int InventoryDifferences { get; set; }

    public int InventoryUpdatesGenerated { get; set; }

    public int NegativeQuantitiesExcluded { get; set; }

    public int NormalizedUpdatesExcluded { get; set; }

    public double ShopifyCoverage { get; set; }
}