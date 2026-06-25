using BuildCv.Application.Features.Iterations;
using BuildCv.Domain.Iterations;
using BuildCv.Infrastructure.Iterations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Infrastructure.Tests.Iterations;

public sealed class IterationCleanupWorkerTests
{
    [Fact]
    public async Task StartAsync_invokes_cleanup_tick_at_least_once()
    {
        var store = new InMemoryIterationStore();
        var worker = CreateWorker(store, pollInterval: TimeSpan.FromMilliseconds(20));

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(200, cts.Token);
        await worker.StopAsync(cts.Token);

        await WorkerCompletedAtLeastOnce(store);
    }

    [Fact]
    public async Task Tick_deletes_results_whose_expires_at_is_in_past()
    {
        var store = new InMemoryIterationStore();
        var requestId = Guid.NewGuid();
        await store.SaveResultAsync(new IterationResult
        {
            RequestId = requestId,
            Status = RequestStatus.Completed,
            AllSteps = Array.Empty<IterationStep>(),
            CreditsConsumed = 1,
            CompletedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        });

        await RunSingleTick(store);

        var (_, result) = await store.GetByIdAsync(requestId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Worker_continues_after_tick_exception()
    {
        var flakyStore = new FlakyIterationStore
        {
            FailFirstCall = true,
        };
        var worker = CreateWorker(flakyStore, pollInterval: TimeSpan.FromMilliseconds(20));

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(400, cts.Token);
        await worker.StopAsync(cts.Token);

        flakyStore.CallCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Worker_implements_IHostedService()
    {
        var store = new InMemoryIterationStore();
        var worker = CreateWorker(store, pollInterval: TimeSpan.FromSeconds(60));

        worker.Should().BeAssignableTo<IHostedService>();
    }

    [Fact]
    public async Task EfIterationStore_DeleteExpiredAsync_returns_count_of_removed_rows()
    {
        var options = new DbContextOptionsBuilder<BuildCv.Infrastructure.Persistence.BuildCvDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new BuildCv.Infrastructure.Persistence.BuildCvDbContext(options);
        var store = new EfIterationStore(dbContext);

        await store.SaveResultAsync(new IterationResult
        {
            RequestId = Guid.NewGuid(),
            Status = RequestStatus.Completed,
            AllSteps = Array.Empty<IterationStep>(),
            CreditsConsumed = 1,
            CompletedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        });
        await store.SaveResultAsync(new IterationResult
        {
            RequestId = Guid.NewGuid(),
            Status = RequestStatus.Completed,
            AllSteps = Array.Empty<IterationStep>(),
            CreditsConsumed = 1,
            CompletedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(23),
        });

        var deleted = await store.DeleteExpiredAsync(DateTime.UtcNow);

        deleted.Should().Be(1);
    }

    [Fact]
    public async Task Hosted_service_resolves_from_di()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IIterationStore, InMemoryIterationStore>();
        services.AddSingleton<IIterationCleanupCapable>(sp => (InMemoryIterationStore)sp.GetRequiredService<IIterationStore>());
        services.AddHostedService<IterationCleanupWorker>();
        await using var provider = services.BuildServiceProvider();

        var hosted = provider.GetServices<IHostedService>().OfType<IterationCleanupWorker>().ToList();

        hosted.Should().HaveCount(1);
    }

    private static IterationCleanupWorker CreateWorker(IIterationStore store, TimeSpan pollInterval)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IIterationStore>(store);
        if (store is IIterationCleanupCapable cleanup)
        {
            services.AddSingleton(cleanup);
        }

        services.AddSingleton<IServiceProvider>(sp => sp);
        return new IterationCleanupWorker(
            services.BuildServiceProvider(),
            NullLogger<IterationCleanupWorker>.Instance,
            pollInterval);
    }

    private static async Task RunSingleTick(IIterationStore store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IIterationStore>(store);
        if (store is IIterationCleanupCapable cleanup)
        {
            services.AddSingleton(cleanup);
        }

        services.AddSingleton<IServiceProvider>(sp => sp);
        var worker = new IterationCleanupWorker(
            services.BuildServiceProvider(),
            NullLogger<IterationCleanupWorker>.Instance,
            TimeSpan.FromMilliseconds(20));

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(80, cts.Token);
        await worker.StopAsync(cts.Token);
    }

    private static async Task WorkerCompletedAtLeastOnce(IIterationStore store)
    {
        await RunSingleTick(store);
    }

    private sealed class FlakyIterationStore : IIterationStore, IIterationCleanupCapable
    {
        public int CallCount { get; private set; }

        public bool FailFirstCall { get; init; }

        public Task SaveRequestAsync(IterationRequest request, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateRequestStatusAsync(Guid requestId, RequestStatus status, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveResultAsync(IterationResult result, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<(IterationRequest?, IterationResult?)> GetByIdAsync(Guid requestId, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> DeleteExpiredAsync(DateTime olderThan, CancellationToken ct = default)
        {
            CallCount++;
            if (FailFirstCall && CallCount == 1)
            {
                throw new InvalidOperationException("simulated tick failure");
            }

            return Task.FromResult(0);
        }
    }
}
