using BuildCv.Domain.Invoicing;

namespace BuildCv.Application.Features.Invoicing;

public interface IInvoiceStore
{
    Task AddAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invoice?> GetByReferenceCodeAsync(string referenceCode, CancellationToken ct = default);
    Task<Invoice?> GetByNumberAsync(string number, CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> ListAsync(int page = 1, int perPage = 20, CancellationToken ct = default);
    Task UpdateAsync(Invoice invoice, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
