using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;
using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Invoicing;

public sealed class EfNumberingRangeStore : INumberingRangeStore
{
    private readonly BuildCvDbContext _db;

    public EfNumberingRangeStore(BuildCvDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(NumberingRange range, CancellationToken ct = default)
    {
        _db.NumberingRanges.Add(range);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<NumberingRange?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.NumberingRanges.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<NumberingRange?> GetByProviderIdAsync(int providerId, CancellationToken ct = default)
    {
        return await _db.NumberingRanges.FirstOrDefaultAsync(r => r.ProviderId == providerId, ct);
    }

    public async Task<NumberingRange?> GetByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        return await _db.NumberingRanges.FirstOrDefaultAsync(r => r.Prefix == prefix, ct);
    }

    public async Task<IReadOnlyList<NumberingRange>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.NumberingRanges.ToListAsync(ct);
    }

    public async Task UpdateAsync(NumberingRange range, CancellationToken ct = default)
    {
        _db.NumberingRanges.Update(range);
        await _db.SaveChangesAsync(ct);
    }
}
