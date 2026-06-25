namespace BuildCv.Application.Features.Credits;

public sealed class GetCreditHistoryHandler(ICreditConsumptionService service)
{
    public Task<CreditHistoryPage> HandleAsync(GetCreditHistoryQuery query, CancellationToken ct) =>
        service.GetHistoryAsync(query.UserId, query.Limit, query.Cursor, ct);
}

public sealed record GetCreditHistoryQuery
{
    public Guid UserId { get; init; }
    public int Limit { get; init; } = 50;
    public string? Cursor { get; init; }
}
