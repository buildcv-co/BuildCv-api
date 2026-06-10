using BuildCv.Domain.Common;
using BuildCv.Domain.Invoicing;

namespace BuildCv.Application.Features.Invoicing;

public sealed class GetInvoiceHandler(IInvoiceStore store)
{
    public async Task<Result<Invoice>> HandleAsync(GetInvoiceQuery query, CancellationToken ct)
    {
        var invoice = await store.GetByIdAsync(query.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.Failure<Invoice>(new Error("INVOICE/NOT_FOUND", "Invoice not found"));
        }

        return Result.Success(invoice);
    }
}

public sealed record GetInvoiceQuery
{
    public Guid InvoiceId { get; init; }
}
