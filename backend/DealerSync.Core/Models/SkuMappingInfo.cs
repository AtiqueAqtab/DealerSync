namespace DealerSync.Core.Models;

public record SkuMappingInfo(
    string LightspeedPartNumber,
    string ShopifySku,
    bool IsVerified);