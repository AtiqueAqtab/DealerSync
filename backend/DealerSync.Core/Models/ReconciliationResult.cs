using DealerSync.Core.Enums;

namespace DealerSync.Core.Models;

public class ReconciliationResult
{
    public int LightspeedItemCount { get; set; }

    public int ShopifyItemCount { get; set; }

    public int ExactMatches { get; set; }

    public int NormalizedMatches { get; set; }

    public int Unmatched { get; set; }

    public int InventoryDifferences { get; set; }

    public int InventoryUpdatesGenerated { get; set; }

    public int NegativeQuantitiesExcluded { get; set; }

    public double ShopifyCoverage { get; set; }

    public List<InventoryMatch> Matches { get; set; } = [];

    public List<InventoryUpdate> InventoryUpdates { get; set; } = [];
}