using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Subscriptions;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Subscriptions;

public sealed class HandleRecurringChargeHandler(
    ISubscriptionStore store,
    AccreditPurchaseHandler accreditPurchase,
    ILogger<HandleRecurringChargeHandler> logger)
{
    public async Task HandleSuccessAsync(string paymentSourceId, DateTime chargedAt, string chargeId, CancellationToken ct = default)
    {
        var sub = await store.GetByPaymentSourceIdAsync(paymentSourceId, ct);
        if (sub is null)
        {
            logger.LogWarning(
                "Recurring charge {ChargeId} for unknown payment source ignored",
                chargeId);
            return;
        }

        var updated = SubscriptionStateMachine.TransitionToActive(sub, chargedAt, DateTime.UtcNow);
        await store.UpsertAsync(updated, ct);
        await accreditPurchase.HandleAsync(
            sub.UserId,
            sub.Plan,
            $"subscription:{sub.Id}:{chargedAt:O}",
            sub.CreditsPerMonth,
            null,
            ct);
        logger.LogInformation(
            "Recurring charge {ChargeId} succeeded for subscription {SubscriptionId}",
            chargeId, sub.Id);
    }

    public async Task HandleFailureAsync(string paymentSourceId, DateTime attemptedAt, string reason, CancellationToken ct = default)
    {
        _ = attemptedAt;
        var sub = await store.GetByPaymentSourceIdAsync(paymentSourceId, ct);
        if (sub is null)
        {
            return;
        }

        var updated = SubscriptionStateMachine.TransitionToPastDue(sub, DateTime.UtcNow, attemptNumber: sub.RetryCount + 1);
        await store.UpsertAsync(updated, ct);
        logger.LogWarning(
            "Recurring charge failed for subscription {SubscriptionId} attempt {Attempt}: {Reason}",
            sub.Id, updated.RetryCount, reason);
    }
}
