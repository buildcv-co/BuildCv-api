using BuildCv.Domain.Payments;

namespace BuildCv.Application.Features.Payments;

public interface IPaymentProvider
{
    Task<CheckoutSession> CreateCheckoutAsync(
        string userId,
        CreditPackage package,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<TransactionStatus?> GetTransactionStatusAsync(
        string wompiTransactionId,
        CancellationToken ct = default);

    bool VerifyWebhookSignature(string payload, string signatureHeader);
}
