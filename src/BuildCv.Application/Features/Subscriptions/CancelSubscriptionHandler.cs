using BuildCv.Domain.Subscriptions;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Subscriptions;

public sealed class CancelSubscriptionHandler(
    ISubscriptionStore store,
    ISubscriptionProvider provider,
    ILogger<CancelSubscriptionHandler> logger)
{
    public async Task<Subscription> HandleAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await store.GetByUserIdAsync(userId, includeCanceled: false, ct);
        if (sub is null)
        {
            throw new InvalidOperationException($"No active subscription for user {userId}");
        }

        await provider.CancelScheduledChargeAsync(sub.PaymentSourceId, ct);
        var canceled = SubscriptionStateMachine.TransitionToCanceled(sub, DateTime.UtcNow, "user canceled");
        await store.UpsertAsync(canceled, ct);
        logger.LogInformation(
            "Subscription {SubscriptionId} canceled by user {UserId} accessUntil {AccessUntil}",
            canceled.Id, userId, canceled.CurrentPeriodEnd);
        return canceled;
    }
}
