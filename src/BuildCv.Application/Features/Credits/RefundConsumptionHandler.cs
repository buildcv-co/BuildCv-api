namespace BuildCv.Application.Features.Credits;

public sealed class RefundConsumptionHandler(ICreditConsumptionService service)
{
    public Task HandleAsync(RefundConsumptionCommand command, CancellationToken ct) =>
        service.RefundConsumptionAsync(command.UserId, command.AdaptRequestId, ct);
}

public sealed record RefundConsumptionCommand
{
    public Guid UserId { get; init; }
    public Guid AdaptRequestId { get; init; }
}
