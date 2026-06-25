using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Subscriptions;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Subscriptions;

public sealed class SubscribeHandler(
    ISubscriptionStore store,
    ISubscriptionProvider provider,
    AccreditPurchaseHandler accreditPurchase,
    ILogger<SubscribeHandler> logger)
{
    public async Task<Subscription> HandleAsync(Guid userId, SubscriptionPlan plan, string paymentSourceId, CancellationToken ct = default)
    {
        var existing = await store.GetByUserIdAsync(userId, includeCanceled: false, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException($"User {userId} already has an active subscription {existing.Id}");
        }

        var sub = Subscription.Create(userId, plan, paymentSourceId, DateTime.UtcNow);
        var chargeId = await provider.CreateScheduledChargeAsync(paymentSourceId, sub.AmountCop, "COP", sub.NextChargeAt, ct);
        await store.UpsertAsync(sub, ct);
        await accreditPurchase.HandleAsync(userId, plan, $"subscription:{sub.Id}", sub.CreditsPerMonth, null, ct);
        logger.LogInformation(
            "Subscription {SubscriptionId} created for user {UserId} plan {Plan} charge {ChargeId}",
            sub.Id, userId, plan, chargeId);
        return sub;
    }
}
