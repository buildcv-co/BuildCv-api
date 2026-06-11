namespace BuildCv.Application.Features.Payments;

public interface IPaymentReconciliationService
{
    Task<int> ReconcileAsync(CancellationToken ct);
}
