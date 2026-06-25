using BuildCv.Application.Common;
using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;

namespace BuildCv.Infrastructure.Payments;

public sealed class FeatureFlagPaymentAdapter(
    IFeatureFlag flags,
    WompiAdapter wompiAdapter,
    DisabledPaymentProvider disabledProvider) : IPaymentProvider
{
    public async Task<CheckoutSession> CreateCheckoutAsync(
        string userId, CreditPackage package, string idempotencyKey, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("wompi-enabled", ct);
        return enabled
            ? await wompiAdapter.CreateCheckoutAsync(userId, package, idempotencyKey, ct)
            : await disabledProvider.CreateCheckoutAsync(userId, package, idempotencyKey, ct);
    }

    public async Task<TransactionStatus?> GetTransactionStatusAsync(
        string wompiTransactionId, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("wompi-enabled", ct);
        return enabled
            ? await wompiAdapter.GetTransactionStatusAsync(wompiTransactionId, ct)
            : await disabledProvider.GetTransactionStatusAsync(wompiTransactionId, ct);
    }

    public bool VerifyWebhookSignature(string payload, string signatureHeader)
    {
        return wompiAdapter.VerifyWebhookSignature(payload, signatureHeader);
    }
}
