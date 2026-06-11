using BuildCv.Application.Features.Payments;
using BuildCv.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Infrastructure.Tests.Payments;

public sealed class PaymentReconciliationWorkerTests
{
    [Fact]
    public async Task StartAsync_triggers_initial_reconciliation_after_delay()
    {
        var service = new TestPaymentReconciliationService { ReconcileResult = 0 };
        var worker = new PaymentReconciliationWorker(
            service,
            NullLogger<PaymentReconciliationWorker>.Instance,
            TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(200, cts.Token);
        await worker.StopAsync(cts.Token);

        service.ReconcileCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Worker_continues_after_reconciliation_exception()
    {
        var service = new TestPaymentReconciliationService { ThrowOnce = true };
        var worker = new PaymentReconciliationWorker(
            service,
            NullLogger<PaymentReconciliationWorker>.Instance,
            TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500, cts.Token);
        await worker.StopAsync(cts.Token);

        service.ReconcileCallCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Worker_implements_IHostedService()
    {
        var service = new TestPaymentReconciliationService();
        var worker = new PaymentReconciliationWorker(service, NullLogger<PaymentReconciliationWorker>.Instance);

        worker.Should().BeAssignableTo<IHostedService>();
    }

    [Fact]
    public async Task Hosted_service_can_be_resolved_from_di()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPaymentReconciliationService, TestPaymentReconciliationService>();
        services.AddHostedService<PaymentReconciliationWorker>();
        await using var provider = services.BuildServiceProvider();

        var hosted = provider.GetServices<IHostedService>().OfType<PaymentReconciliationWorker>().ToList();
        hosted.Should().HaveCount(1);
    }

    private sealed class TestPaymentReconciliationService : IPaymentReconciliationService
    {
        private int _callCount;
        private bool _threwOnce;

        public int ReconcileCallCount => _callCount;
        public int ReconcileResult { get; set; } = 0;
        public bool ThrowOnce { get; set; }

        public Task<int> ReconcileAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _callCount);
            if (ThrowOnce && !_threwOnce)
            {
                _threwOnce = true;
                throw new InvalidOperationException("simulated reconciliation failure");
            }

            return Task.FromResult(ReconcileResult);
        }
    }
}
