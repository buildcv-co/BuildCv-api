using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.FeatureFlags;

public sealed class FeatureFlagMigrationService(
    IServiceProvider services,
    IOptions<FeatureFlagsOptions> options,
    ILogger<FeatureFlagMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetService<IFeatureFlagStore>();
        if (store is null)
        {
            logger.LogInformation("FeatureFlagMigrationService skipped — IFeatureFlagStore not registered");
            return;
        }

        foreach (var (name, defaultValue) in options.Value.Defaults)
        {
            try
            {
                var existing = await store.GetAsync(name, ct);
                var seed = existing ?? FeatureFlag.Create(name, defaultValue);
                await store.UpsertAsync(seed, ct);
                logger.LogInformation(
                    "Feature flag seeded (flagName={FlagName}, defaultValue={DefaultValue})", name, defaultValue);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Feature flag seed failed (flagName={FlagName})", name);
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
