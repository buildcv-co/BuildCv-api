using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Payments;

public sealed class DisabledPaymentProvider : IPaymentProvider
{
    private readonly ILogger<DisabledPaymentProvider> _logger;

    public DisabledPaymentProvider(ILogger<DisabledPaymentProvider> logger)
    {
        _logger = logger;
    }

    public Task<CheckoutSession> CreateCheckoutAsync(
        string userId,
        CreditPackage package,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        _logger.LogWarning("Wompi is disabled; CreateCheckoutAsync was called for user {UserId}", userId);
        throw new InvalidOperationException("Wompi payment provider is disabled");
    }

    public Task<TransactionStatus?> GetTransactionStatusAsync(
        string wompiTransactionId,
        CancellationToken ct = default)
    {
        _logger.LogWarning("Wompi is disabled; GetTransactionStatusAsync was called for transaction {TransactionId}", wompiTransactionId);
        throw new InvalidOperationException("Wompi payment provider is disabled");
    }

    public bool VerifyWebhookSignature(string payload, string signatureHeader) => false;
}
