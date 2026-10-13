using DealerSync.Core.Models;
using DealerSync.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DealerSync.Infrastructure.Persistence.Repositories;

public class ReconciliationHistoryRepository
{
    private readonly DealerSyncDbContext _context;

    public ReconciliationHistoryRepository(DealerSyncDbContext context)
    {
        _context = context;
    }

    // Save a successful reconciliation
    public async Task<ReconciliationRun> SaveAsync(
        ReconciliationResult result)
    {
        var run = new ReconciliationRun
        {
            LightspeedItemCount = result.LightspeedItemCount,
            ShopifyItemCount = result.ShopifyItemCount,
            ExactMatches = result.ExactMatches,
            NormalizedMatches = result.NormalizedMatches,
            Unmatched = result.Unmatched,
            InventoryDifferences = result.InventoryDifferences,
            InventoryUpdatesGenerated = result.InventoryUpdatesGenerated,
            NegativeQuantitiesExcluded = result.NegativeQuantitiesExcluded,
            ShopifyCoverage = result.ShopifyCoverage
        };

        _context.ReconciliationRuns.Add(run);

        await _context.SaveChangesAsync();

        return run;
    }

    // Retrieve the 50 most recent reconciliation runs
    public async Task<List<ReconciliationRun>> GetRecentAsync()
    {
        return await _context.ReconciliationRuns
            .AsNoTracking()
            .OrderByDescending(run => run.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    // Retrieve a reconciliation run by ID
    public async Task<ReconciliationRun?> GetByIdAsync(Guid id)
    {
        return await _context.ReconciliationRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(run => run.Id == id);
    }
}