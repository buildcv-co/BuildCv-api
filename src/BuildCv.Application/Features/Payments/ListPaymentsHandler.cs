using BuildCv.Domain.Common;
using BuildCv.Domain.Payments;

namespace BuildCv.Application.Features.Payments;

public sealed class ListPaymentsHandler(IPaymentStore store)
{
    public async Task<Result<IReadOnlyList<Payment>>> HandleAsync(ListPaymentsQuery query, CancellationToken ct)
    {
        var payments = await store.ListByUserIdAsync(query.UserId, query.Page, query.PerPage, ct);
        return Result.Success(payments);
    }
}

public sealed record ListPaymentsQuery
{
    public string UserId { get; init; } = "";
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 20;
}
