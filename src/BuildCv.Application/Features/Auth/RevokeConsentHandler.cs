using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;

namespace BuildCv.Application.Features.Auth;

public sealed class RevokeConsentHandler(IConsentStore store)
{
    public async Task<Result> HandleAsync(RevokeConsentCommand command, CancellationToken ct)
    {
        var active = await store.GetActiveAsync(command.UserId, command.Purpose, ct);
        if (active is null)
        {
            return Result.Failure(new Error("CONSENT/REQUIRED", "No active consent to revoke"));
        }

        var revoked = active with { RevokedAt = DateTime.UtcNow };
        await store.AddAsync(revoked, ct);
        return Result.Success();
    }
}
