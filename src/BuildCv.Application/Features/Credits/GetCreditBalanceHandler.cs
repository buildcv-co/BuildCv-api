namespace BuildCv.Application.Features.Credits;

public sealed class GetCreditBalanceHandler(ICreditConsumptionService service)
{
    public Task<CreditBalanceView> HandleAsync(GetCreditBalanceQuery query, CancellationToken ct) =>
        service.GetBalanceAsync(query.UserId, ct);
}

public sealed record GetCreditBalanceQuery
{
    public Guid UserId { get; init; }
}
