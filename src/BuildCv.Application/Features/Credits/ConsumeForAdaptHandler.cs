namespace BuildCv.Application.Features.Credits;

public sealed class ConsumeForAdaptHandler(ICreditConsumptionService service)
{
    public Task<CreditConsumeResult> HandleAsync(ConsumeForAdaptCommand command, CancellationToken ct) =>
        service.ConsumeForAdaptAsync(command.UserId, command.AdaptRequestId, ct);
}

public sealed record ConsumeForAdaptCommand
{
    public Guid UserId { get; init; }
    public Guid AdaptRequestId { get; init; }
}
