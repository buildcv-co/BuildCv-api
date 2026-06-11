using BuildCv.Application.Features.Payments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Payments;

public sealed class PaymentReconciliationWorker : BackgroundService
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(60);

    private readonly IPaymentReconciliationService _reconciliationService;
    private readonly ILogger<PaymentReconciliationWorker> _logger;
    private readonly TimeSpan _pollInterval;

    public PaymentReconciliationWorker(
        IPaymentReconciliationService reconciliationService,
        ILogger<PaymentReconciliationWorker> logger)
        : this(reconciliationService, logger, DefaultPollInterval)
    {
    }

    public PaymentReconciliationWorker(
        IPaymentReconciliationService reconciliationService,
        ILogger<PaymentReconciliationWorker> logger,
        TimeSpan pollInterval)
    {
        _reconciliationService = reconciliationService;
        _logger = logger;
        _pollInterval = pollInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Payment reconciliation worker started (pollIntervalSeconds={Interval}s)",
            (int)_pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var reconciled = await _reconciliationService.ReconcileAsync(stoppingToken);
                if (reconciled > 0)
                {
                    _logger.LogInformation(
                        "Payment reconciliation cycle reconciled {Count} payments",
                        reconciled);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Payment reconciliation cycle failed; will retry after poll interval");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Payment reconciliation worker stopped");
    }
}
