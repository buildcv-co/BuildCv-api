namespace BuildCv.Application.Features.Auth;

public sealed class HasActiveConsentHandler(IConsentStore store)
{
    public async Task<bool> HandleAsync(HasActiveConsentQuery query, CancellationToken ct)
    {
        var active = await store.GetActiveAsync(query.UserId, query.Purpose, ct);
        return active is not null;
    }
}
