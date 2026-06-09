using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class RectifyUserDataHandler(InMemoryConsentStore consentStore, InMemoryUserDataStore userDataStore)
{
    public async Task<Result<User>> HandleAsync(RectifyUserDataCommand command, CancellationToken ct)
    {
        var consent = await consentStore.GetActiveAsync(command.UserId, "rectification", ct);
        if (consent is null)
        {
            return Result.Failure<User>(new Error("CONSENT/REQUIRED", "Active consent required for rectification"));
        }

        var userResult = await userDataStore.GetByIdAsync(command.UserId, ct);
        if (userResult.IsFailure)
        {
            return userResult;
        }

        var user = userResult.Value;
        var updated = user with
        {
            Email = command.Email ?? user.Email,
            Name = command.Name ?? user.Name
        };
        userDataStore.Upsert(updated);

        userDataStore.AddLog(new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            DataType = "profile",
            Action = "rectify",
            Timestamp = DateTime.UtcNow,
            Reason = "ARCO Rectification request"
        });

        return Result.Success(updated);
    }
}
