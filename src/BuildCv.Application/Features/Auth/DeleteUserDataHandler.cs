using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class DeleteUserDataHandler(InMemoryConsentStore consentStore, InMemoryUserDataStore userDataStore)
{
    public async Task<Result> HandleAsync(DeleteUserDataCommand command, CancellationToken ct)
    {
        var consent = await consentStore.GetActiveAsync(command.UserId, "data-access", ct);
        if (consent is null)
        {
            return Result.Failure(new Error("CONSENT/REQUIRED", "Active consent required for data deletion"));
        }

        userDataStore.Delete(command.UserId);
        consentStore.RevokeAll(command.UserId, DateTime.UtcNow);

        userDataStore.AddLog(new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            DataType = "profile",
            Action = "delete",
            Timestamp = DateTime.UtcNow,
            Reason = "ARCO Cancellation request"
        });

        return Result.Success();
    }
}
