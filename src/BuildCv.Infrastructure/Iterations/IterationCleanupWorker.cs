using BuildCv.Application.Features.Iterations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Iterations;

public sealed class IterationCleanupWorker(
    IServiceProvider services,
    ILogger<IterationCleanupWorker> logger,
    TimeSpan? pollInterval = null) : BackgroundService
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromHours(1);

    private readonly TimeSpan _pollInterval = pollInterval ?? DefaultPollInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Iteration cleanup worker started (pollIntervalSeconds={Interval}s)",
            (int)_pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var cleanup = scope.ServiceProvider.GetService<IIterationCleanupCapable>();
                if (cleanup is null)
                {
                    logger.LogInformation("Iteration cleanup skipped — IIterationCleanupCapable not registered");
                }
                else
                {
                    var deleted = await cleanup.DeleteExpiredAsync(DateTime.UtcNow, stoppingToken);
                    logger.LogInformation("Iteration cleanup tick (deleted={Deleted})", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Iteration cleanup tick failed; will retry after poll interval");
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

        logger.LogInformation("Iteration cleanup worker stopped");
    }
}
