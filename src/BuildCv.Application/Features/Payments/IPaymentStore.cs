using BuildCv.Domain.Payments;

namespace BuildCv.Application.Features.Payments;

public interface IPaymentStore
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Payment?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default);
    Task<Payment?> GetByWompiTransactionIdAsync(string wompiTransactionId, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> ListByUserIdAsync(string userId, int page, int perPage, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
}
