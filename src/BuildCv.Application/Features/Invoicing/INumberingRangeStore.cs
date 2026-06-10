using BuildCv.Domain.Invoicing;

namespace BuildCv.Application.Features.Invoicing;

public interface INumberingRangeStore
{
    Task AddAsync(NumberingRange range, CancellationToken ct = default);
    Task<NumberingRange?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NumberingRange?> GetByProviderIdAsync(int providerId, CancellationToken ct = default);
    Task<NumberingRange?> GetByPrefixAsync(string prefix, CancellationToken ct = default);
    Task<IReadOnlyList<NumberingRange>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(NumberingRange range, CancellationToken ct = default);
}
