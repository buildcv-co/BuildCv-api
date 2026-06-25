using BuildCv.Domain.Subscriptions;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Subscriptions;

public sealed class ProcessRetriesHandler(
    ISubscriptionStore store,
    ISubscriptionProvider provider,
    HandleRecurringChargeHandler chargeHandler,
    ILogger<ProcessRetriesHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var due = await store.GetDueForRetryAsync(now, 50, ct);

        foreach (var sub in due)
        {
            try
            {
                var chargeId = await provider.CreateScheduledChargeAsync(sub.PaymentSourceId, sub.AmountCop, "COP", now, ct);
                await chargeHandler.HandleSuccessAsync(sub.PaymentSourceId, now, chargeId, ct);
            }
            catch (Exception ex)
            {
                await chargeHandler.HandleFailureAsync(sub.PaymentSourceId, now, ex.Message, ct);
            }
        }

        logger.LogInformation("Processed {Count} subscription retries", due.Count);
    }
}
