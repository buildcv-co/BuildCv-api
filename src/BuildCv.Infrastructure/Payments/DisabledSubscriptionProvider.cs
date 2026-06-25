using BuildCv.Application.Features.Subscriptions;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Payments;

public sealed class DisabledSubscriptionProvider(ILogger<DisabledSubscriptionProvider> logger) : ISubscriptionProvider
{
    public Task<string> CreateScheduledChargeAsync(
        string paymentSourceId,
        decimal amountCop,
        string currency,
        DateTime chargeDate,
        CancellationToken ct = default)
    {
        _ = paymentSourceId;
        _ = amountCop;
        _ = currency;
        _ = chargeDate;
        logger.LogWarning("Subscriptions disabled; CreateScheduledChargeAsync was called");
        throw new NotSupportedException("Subscriptions are disabled");
    }

    public Task<bool> CancelScheduledChargeAsync(string chargeId, CancellationToken ct = default)
    {
        _ = chargeId;
        logger.LogInformation("Subscriptions disabled; CancelScheduledChargeAsync treated as successful");
        return Task.FromResult(true);
    }

    public bool VerifyWebhookSignature(string payload, string signature)
    {
        _ = payload;
        _ = signature;
        return false;
    }
}
