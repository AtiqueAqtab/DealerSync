namespace DealerSync.Core.Models;

public sealed record VerifiedSkuMapping(
    string LightspeedPartNumber,
    string ShopifySku
);
