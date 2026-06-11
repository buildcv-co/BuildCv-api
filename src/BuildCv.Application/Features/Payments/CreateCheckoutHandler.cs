using BuildCv.Domain.Common;
using BuildCv.Domain.Payments;

namespace BuildCv.Application.Features.Payments;

public sealed class CreateCheckoutHandler(IPaymentStore store, IPaymentProvider provider)
{
    public async Task<Result<CheckoutSession>> HandleAsync(CreateCheckoutCommand command, CancellationToken ct)
    {
        var package = CreditPackage.FindById(command.PackageId);
        if (package is null)
        {
            return Result.Failure<CheckoutSession>(
                new Error("PAYMENT/INVALID_PACKAGE", $"Unknown package: {command.PackageId}"));
        }

        var idempotencyKey = $"{command.UserId}:{command.PackageId}";
        var existing = await store.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing is not null)
        {
            var existingSession = new CheckoutSession
            {
                SessionId = existing.ProviderSessionId ?? existing.Id.ToString(),
                PublicKey = "",
                AmountInCents = existing.AmountInCents,
                Currency = existing.Currency,
                Reference = existing.IdempotencyKey
            };
            return Result.Success(existingSession);
        }

        var session = await provider.CreateCheckoutAsync(command.UserId, package, idempotencyKey, ct);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(command.UserId),
            PackageId = package.Id,
            Credits = package.Credits,
            AmountInCents = package.PriceInCents,
            Currency = package.Currency,
            Status = PaymentStatus.Pending,
            IdempotencyKey = idempotencyKey,
            ProviderSessionId = session.SessionId,
            WompiPaymentLink = session.Reference,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await store.AddAsync(payment, ct);

        return Result.Success(session);
    }
}

public sealed record CreateCheckoutCommand
{
    public string UserId { get; init; } = "";
    public string PackageId { get; init; } = "";
}
