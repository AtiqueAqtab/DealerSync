namespace DealerSync.Core.Models;

public class InventoryUpdate
{
    public ShopifyVariantIdentity? ExportIdentity { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public int CurrentQuantity { get; set; }

    public int NewQuantity { get; set; }
}
