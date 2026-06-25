using BuildCv.Application.Common;
using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Payments;

public sealed class FeatureFlagPaymentAdapter(
    IFeatureFlag flags,
    IPaymentProvider inner,
    ILogger<FeatureFlagPaymentAdapter> logger) : IPaymentProvider
{
    public async Task<CheckoutSession> CreateCheckoutAsync(
        string userId, CreditPackage package, string idempotencyKey, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("wompi-enabled", ct);
        if (!enabled)
        {
            logger.LogInformation("Wompi disabled by feature flag, throwing disabled-provider signal for user {UserId}", userId);
            throw new InvalidOperationException("Wompi payment provider is disabled (feature flag)");
        }

        return await inner.CreateCheckoutAsync(userId, package, idempotencyKey, ct);
    }

    public async Task<TransactionStatus?> GetTransactionStatusAsync(
        string wompiTransactionId, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("wompi-enabled", ct);
        if (!enabled)
        {
            logger.LogInformation(
                "Wompi disabled by feature flag, throwing disabled-provider signal for transaction {TransactionId}",
                wompiTransactionId);
            throw new InvalidOperationException("Wompi payment provider is disabled (feature flag)");
        }

        return await inner.GetTransactionStatusAsync(wompiTransactionId, ct);
    }

    public bool VerifyWebhookSignature(string payload, string signatureHeader)
    {
        return inner.VerifyWebhookSignature(payload, signatureHeader);
    }
}
