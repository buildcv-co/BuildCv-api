using BuildCv.Domain.Common;
using BuildCv.Domain.Payments;

namespace BuildCv.Application.Features.Payments;

public sealed class GetPaymentHandler(IPaymentStore store)
{
    public async Task<Result<Payment>> HandleAsync(GetPaymentQuery query, CancellationToken ct)
    {
        var payment = await store.GetByIdAsync(query.PaymentId, ct);
        if (payment is null || payment.UserId.ToString() != query.UserId)
        {
            return Result.Failure<Payment>(
                new Error("PAYMENT/NOT_FOUND", "Payment not found"));
        }

        return Result.Success(payment);
    }
}

public sealed record GetPaymentQuery
{
    public Guid PaymentId { get; init; }
    public string UserId { get; init; } = "";
}
