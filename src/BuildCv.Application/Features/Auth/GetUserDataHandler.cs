using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class GetUserDataHandler(IConsentStore consentStore, IUserDataStore userDataStore)
{
    public async Task<Result<User>> HandleAsync(GetUserDataQuery query, CancellationToken ct)
    {
        var consent = await consentStore.GetActiveAsync(query.UserId, "data-access", ct);
        if (consent is null)
        {
            return Result.Failure<User>(new Error("CONSENT/REQUIRED", "Active consent required for data access"));
        }

        var userResult = await userDataStore.GetByIdAsync(query.UserId, ct);
        if (userResult.IsFailure)
        {
            return userResult;
        }

        await userDataStore.AddTreatmentLogAsync(new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = query.UserId,
            DataType = "profile",
            Action = "access",
            Timestamp = DateTime.UtcNow,
            Reason = "ARCO Access request"
        }, ct);

        return userResult;
    }
}
