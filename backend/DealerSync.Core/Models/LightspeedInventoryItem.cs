namespace DealerSync.Core.Models;

public class LightspeedInventoryItem
{
    public string PartNumber { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public int Quantity { get; set; } 
}