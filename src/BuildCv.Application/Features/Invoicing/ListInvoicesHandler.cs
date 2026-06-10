using BuildCv.Domain.Common;
using BuildCv.Domain.Invoicing;

namespace BuildCv.Application.Features.Invoicing;

public sealed class ListInvoicesHandler(IInvoiceStore store)
{
    public async Task<Result<IReadOnlyList<Invoice>>> HandleAsync(ListInvoicesQuery query, CancellationToken ct)
    {
        var invoices = await store.GetByUserIdAsync(query.UserId, ct);
        return Result.Success(invoices);
    }
}

public sealed record ListInvoicesQuery
{
    public Guid UserId { get; init; }
}
