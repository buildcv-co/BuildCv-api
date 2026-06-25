using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Subscriptions;

public sealed class SubscriptionReconciliationWorker(
    Func<IServiceProvider, CancellationToken, Task> tickAction,
    IServiceProvider services,
    ILogger<SubscriptionReconciliationWorker> logger,
    TimeSpan? pollInterval = null) : BackgroundService
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _pollInterval = pollInterval ?? DefaultPollInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Subscription reconciliation worker started (pollIntervalSeconds={Interval}s)",
            (int)_pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                await tickAction(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Subscription reconciliation tick failed; will retry after poll interval");
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

        logger.LogInformation("Subscription reconciliation worker stopped");
    }
}
