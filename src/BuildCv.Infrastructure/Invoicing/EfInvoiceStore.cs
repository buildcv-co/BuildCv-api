using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;
using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Invoicing;

public sealed class EfInvoiceStore : IInvoiceStore
{
    private readonly BuildCvDbContext _db;

    public EfInvoiceStore(BuildCvDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<Invoice?> GetByReferenceCodeAsync(string referenceCode, CancellationToken ct = default)
    {
        return await _db.Invoices.FirstOrDefaultAsync(i => i.ReferenceCode == referenceCode, ct);
    }

    public async Task<Invoice?> GetByNumberAsync(string number, CancellationToken ct = default)
    {
        return await _db.Invoices.FirstOrDefaultAsync(i => i.Number == number, ct);
    }

    public async Task<IReadOnlyList<Invoice>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Invoices.Where(i => i.UserId == userId).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Invoice>> ListAsync(int page = 1, int perPage = 20, CancellationToken ct = default)
    {
        return await _db.Invoices
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        _db.Invoices.Update(invoice);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is not null)
        {
            _db.Invoices.Remove(invoice);
            await _db.SaveChangesAsync(ct);
        }
    }
}
