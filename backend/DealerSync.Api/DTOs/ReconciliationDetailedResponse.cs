using DealerSync.Core.Models;

namespace DealerSync.Api.DTOs;

public record ReconciliationDetailedResponse(
    ReconciliationSummaryResponse Summary,
    List<ReconciliationMatchResponse> Matches
)
{
    public static ReconciliationDetailedResponse FromResult(
        ReconciliationResult result)
    {
        return new ReconciliationDetailedResponse(
            Summary: ReconciliationSummaryResponse.FromResult(result),
            Matches: result.Matches
                .Select(ReconciliationMatchResponse.FromMatch)
                .ToList()
        );
    }
}