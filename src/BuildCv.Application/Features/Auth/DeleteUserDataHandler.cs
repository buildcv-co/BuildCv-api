using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class DeleteUserDataHandler(IConsentStore consentStore, IUserDataStore userDataStore)
{
    public async Task<Result> HandleAsync(DeleteUserDataCommand command, CancellationToken ct)
    {
        var consent = await consentStore.GetActiveAsync(command.UserId, "data-access", ct);
        if (consent is null)
        {
            return Result.Failure(new Error("CONSENT/REQUIRED", "Active consent required for data deletion"));
        }

        await userDataStore.DeleteAsync(command.UserId, ct);
        await consentStore.RevokeAllAsync(command.UserId, DateTime.UtcNow, ct);

        await userDataStore.AddTreatmentLogAsync(new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            DataType = "profile",
            Action = "delete",
            Timestamp = DateTime.UtcNow,
            Reason = "ARCO Cancellation request"
        }, ct);

        return Result.Success();
    }
}
