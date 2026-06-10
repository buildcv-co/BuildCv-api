using System.Collections.Concurrent;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;

namespace BuildCv.Infrastructure.Invoicing;

public sealed class InMemoryInvoiceStore : IInvoiceStore
{
    private readonly ConcurrentDictionary<Guid, Invoice> _invoices = new();

    public Task AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        _invoices[invoice.Id] = invoice;
        return Task.CompletedTask;
    }

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _invoices.TryGetValue(id, out var invoice);
        return Task.FromResult(invoice);
    }

    public Task<Invoice?> GetByReferenceCodeAsync(string referenceCode, CancellationToken ct = default)
    {
        var invoice = _invoices.Values.FirstOrDefault(i => i.ReferenceCode == referenceCode);
        return Task.FromResult(invoice);
    }

    public Task<Invoice?> GetByNumberAsync(string number, CancellationToken ct = default)
    {
        var invoice = _invoices.Values.FirstOrDefault(i => i.Number == number);
        return Task.FromResult(invoice);
    }

    public Task<IReadOnlyList<Invoice>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var invoices = _invoices.Values.Where(i => i.UserId == userId).ToList();
        return Task.FromResult<IReadOnlyList<Invoice>>(invoices);
    }

    public Task<IReadOnlyList<Invoice>> ListAsync(int page = 1, int perPage = 20, CancellationToken ct = default)
    {
        var invoices = _invoices.Values
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToList();
        return Task.FromResult<IReadOnlyList<Invoice>>(invoices);
    }

    public Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        _invoices[invoice.Id] = invoice;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _invoices.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
