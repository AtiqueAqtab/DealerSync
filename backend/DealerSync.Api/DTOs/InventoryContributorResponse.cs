namespace DealerSync.Api.DTOs;

public record InventoryContributorResponse(
    string PartNumber,
    int Quantity,
    string MatchType
);