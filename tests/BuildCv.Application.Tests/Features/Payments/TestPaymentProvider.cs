using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;

namespace BuildCv.Application.Tests.Features.Payments;

internal sealed class TestPaymentProvider : IPaymentProvider
{
    private bool _webhookSignatureValid;
    private string _transactionStatus = "PENDING";
    private readonly HashSet<string> _throwOnGetTransactionFor = [];

    public List<string> GetTransactionCalls { get; } = [];

    public void SetWebhookSignatureValid(bool valid) => _webhookSignatureValid = valid;
    public void SetTransactionStatus(string status) => _transactionStatus = status;
    public void ThrowOnGetTransactionFor(string wompiTransactionId) => _throwOnGetTransactionFor.Add(wompiTransactionId);

    public Task<CheckoutSession> CreateCheckoutAsync(
        string userId,
        CreditPackage package,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var session = new CheckoutSession
        {
            SessionId = $"sess-{Guid.NewGuid():N}",
            PublicKey = "test-public-key",
            AmountInCents = package.PriceInCents,
            Currency = package.Currency,
            Reference = idempotencyKey
        };
        return Task.FromResult(session);
    }

    public Task<TransactionStatus?> GetTransactionStatusAsync(
        string wompiTransactionId,
        CancellationToken ct = default)
    {
        GetTransactionCalls.Add(wompiTransactionId);
        if (_throwOnGetTransactionFor.Contains(wompiTransactionId))
        {
            throw new InvalidOperationException("simulated provider failure");
        }

        var status = new TransactionStatus
        {
            WompiTransactionId = wompiTransactionId,
            Status = _transactionStatus,
            AmountInCents = 1_500_000
        };
        return Task.FromResult<TransactionStatus?>(status);
    }

    public bool VerifyWebhookSignature(string payload, string signatureHeader) => _webhookSignatureValid;
}
