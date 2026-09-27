namespace DealerSync.Core.Models;

public class ShopifyInventoryItem
{
    public string Sku { get; set; } = string.Empty;
    
    public string Title { get; set; } = string.Empty;
    
    public string Location { get; set; } = string.Empty;
    
    public int CurrentQuantity { get; set; }
}