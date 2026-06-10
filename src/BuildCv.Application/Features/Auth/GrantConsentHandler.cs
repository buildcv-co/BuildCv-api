using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class GrantConsentHandler(IConsentStore store)
{
    public async Task<Result<ConsentRecord>> HandleAsync(GrantConsentCommand command, CancellationToken ct)
    {
        var latest = await store.GetLatestAsync(command.UserId, command.Purpose, ct);
        if (latest is not null && latest.PolicyVersion >= command.PolicyVersion && latest.IsValid)
        {
            return Result.Failure<ConsentRecord>(new Error("CONSENT/ALREADY_GRANTED", "Consent already granted for this or newer policy"));
        }

        var record = new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            PolicyVersion = command.PolicyVersion,
            ConsentDate = DateTime.UtcNow,
            Purpose = command.Purpose
        };
        await store.AddAsync(record, ct);
        return Result.Success(record);
    }
}
