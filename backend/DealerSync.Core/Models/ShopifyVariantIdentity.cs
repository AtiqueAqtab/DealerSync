namespace DealerSync.Core.Models;

// Exact source values for Shopify imports, separate from normalized matching fields.
public sealed record ShopifyVariantIdentity(
    string Handle,
    string Option1Name,
    string Option1Value,
    string Option2Name,
    string? Option2Value,
    string Option3Name,
    string? Option3Value,
    string Sku,
    string Title,
    string Location);
