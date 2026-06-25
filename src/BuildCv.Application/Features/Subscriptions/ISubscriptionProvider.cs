namespace BuildCv.Application.Features.Subscriptions;

public interface ISubscriptionProvider
{
    Task<string> CreateScheduledChargeAsync(string paymentSourceId, decimal amountCop, string currency, DateTime chargeDate, CancellationToken ct = default);
    Task<bool> CancelScheduledChargeAsync(string chargeId, CancellationToken ct = default);
    bool VerifyWebhookSignature(string payload, string signature);
}
