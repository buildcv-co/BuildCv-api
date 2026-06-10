using BuildCv.Domain.Auth;

namespace BuildCv.Application.Features.Auth;

public sealed class GetConsentHistoryHandler(IConsentStore store)
{
    public Task<IReadOnlyList<ConsentRecord>> HandleAsync(GetConsentHistoryQuery query, CancellationToken ct)
        => store.GetHistoryAsync(query.UserId, ct);
}
