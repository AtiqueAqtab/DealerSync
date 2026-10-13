using DealerSync.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DealerSync.Infrastructure.Persistence.Repositories;

public class SkuMappingRepository
{
    private readonly DealerSyncDbContext _context;

    public SkuMappingRepository(DealerSyncDbContext context)
    {
        _context = context;
    }

    public async Task<List<SkuMapping>> GetAllAsync()
    {
        return await _context.SkuMappings
            .AsNoTracking()
            .OrderBy(mapping => mapping.LightspeedPartNumber)
            .ToListAsync();
    }

    public async Task<SkuMapping?> GetByIdAsync(Guid id)
    {
        return await _context.SkuMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(mapping => mapping.Id == id);
    }

    public async Task<SkuMapping?> GetByLightspeedPartNumberAsync(
        string partNumber)
    {
        return await _context.SkuMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                mapping => mapping.LightspeedPartNumber == partNumber);
    }

    public async Task<SkuMapping?> GetByShopifySkuAsync(string sku)
    {
        return await _context.SkuMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(mapping => mapping.ShopifySku == sku);
    }

    public async Task<SkuMapping> AddAsync(SkuMapping mapping)
    {
        _context.SkuMappings.Add(mapping);

        await _context.SaveChangesAsync();

        return mapping;
    }

    public async Task<bool> VerifyAsync(Guid id)
    {
        var mapping = await _context.SkuMappings
            .FirstOrDefaultAsync(item => item.Id == id);

        if (mapping is null)
            return false;

        mapping.IsVerified = true;
        mapping.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }
    
    public async Task<List<SkuMapping>> GetVerifiedAsync()
    {
        return await _context.SkuMappings
            .AsNoTracking()
            .Where(mapping => mapping.IsVerified)
            .OrderBy(mapping => mapping.LightspeedPartNumber)
            .ToListAsync();
    }
    
}