using DealerSync.Core.Enums;

namespace DealerSync.Core.Models;

public sealed class InventoryContributor
{
    public required LightspeedInventoryItem LightspeedItem { get; init; }

    public required SkuMatchType MatchType { get; init; }
}