using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Auth;

public sealed class DeleteUserDataHandler(
    IConsentStore consentStore,
    IUserDataStore userDataStore,
    ISubscriptionStore subscriptionStore,
    ISubscriptionProvider subscriptionProvider,
    ILogger<DeleteUserDataHandler>? logger = null)
{
    public const string AnonymizedEmail = "[deleted]@anonymized";
    public const string AnonymizedName = "[Deleted User]";

    public async Task<Result> HandleAsync(DeleteUserDataCommand command, CancellationToken ct)
    {
        var consent = await consentStore.GetActiveAsync(command.UserId, "data-access", ct);
        if (consent is null)
        {
            return Result.Failure(new Error("CONSENT/REQUIRED", "Active consent required for data deletion"));
        }

        var activeSubscription = await subscriptionStore.GetByUserIdAsync(command.UserId, includeCanceled: false, ct);
        if (activeSubscription is not null)
        {
            try
            {
                await subscriptionProvider.CancelScheduledChargeAsync(activeSubscription.PaymentSourceId, ct);
                logger?.LogInformation(
                    "ARCO delete: pre-canceled Wompi scheduled charge for subscription {SubscriptionId} (user {UserId})",
                    activeSubscription.Id, command.UserId);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "ARCO delete: failed to pre-cancel Wompi scheduled charge for subscription {SubscriptionId} (user {UserId}); FK cascade will still remove the row",
                    activeSubscription.Id, command.UserId);
            }
        }

        var hasPayments = await userDataStore.HasPaymentsAsync(command.UserId, ct);

        if (hasPayments)
        {
            await userDataStore.AnonymizeAsync(command.UserId, ct);
            logger?.LogInformation(
                "ARCO delete: anonymized user {UserId} (had paid invoices — payments/invoices preserved)",
                command.UserId);
        }
        else
        {
            await userDataStore.DeleteAsync(command.UserId, ct);
            logger?.LogInformation(
                "ARCO delete: hard-deleted user {UserId} (no paid invoices)",
                command.UserId);
        }

        await consentStore.RevokeAllAsync(command.UserId, DateTime.UtcNow, ct);

        await userDataStore.AddTreatmentLogAsync(new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            DataType = "profile",
            Action = hasPayments ? "anonymize" : "delete",
            Timestamp = DateTime.UtcNow,
            Reason = hasPayments
                ? "ARCO Cancellation request — anonymized (paid invoices retained per DIAN legal hold)"
                : "ARCO Cancellation request",
        }, ct);

        return Result.Success();
    }
}
