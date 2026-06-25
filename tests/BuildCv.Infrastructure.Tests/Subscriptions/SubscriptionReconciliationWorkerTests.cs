using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Credits;
using BuildCv.Domain.Subscriptions;
using BuildCv.Infrastructure.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Infrastructure.Tests.Subscriptions;

public sealed class SubscriptionReconciliationWorkerTests
{
    [Fact]
    public async Task StartAsync_invokes_tick_action_during_poll_cycle()
    {
        var counter = new TickCounter();
        var worker = CreateWorker(counter.TickAsync, pollInterval: TimeSpan.FromMilliseconds(20));

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(200, cts.Token);
        await worker.StopAsync(cts.Token);

        counter.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Worker_continues_after_tick_exception()
    {
        var counter = new TickCounter { ThrowOnce = true };
        var worker = CreateWorker(counter.TickAsync, pollInterval: TimeSpan.FromMilliseconds(20));

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500, cts.Token);
        await worker.StopAsync(cts.Token);

        counter.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Worker_implements_IHostedService()
    {
        var worker = CreateWorker((_, _) => Task.CompletedTask, pollInterval: TimeSpan.FromSeconds(60));

        worker.Should().BeAssignableTo<IHostedService>();
    }

    [Fact]
    public async Task Tick_action_receives_per_tick_scope_service_provider()
    {
        var capturedProviders = new List<IServiceProvider>();
        Func<IServiceProvider, CancellationToken, Task> tick = (sp, _) =>
        {
            capturedProviders.Add(sp);
            return Task.CompletedTask;
        };
        var worker = CreateWorker(tick, pollInterval: TimeSpan.FromMilliseconds(10));

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(150);
        await worker.StopAsync(cts.Token);

        capturedProviders.Should().NotBeEmpty();
        capturedProviders.Should().OnlyContain(p => p != null);
    }

    [Fact]
    public async Task Hosted_service_resolves_from_di()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISubscriptionStore, InMemorySubscriptionStore>();
        services.AddSingleton<ISubscriptionProvider, NoopSubscriptionProvider>();
        services.AddSingleton<ICreditLedger, NoopCreditLedger>();
        services.AddSingleton<AccreditPurchaseHandler>();
        services.AddSingleton<HandleRecurringChargeHandler>();
        services.AddSingleton<ProcessRetriesHandler>();
        Func<IServiceProvider, CancellationToken, Task> tick = (sp, ct) =>
            sp.GetRequiredService<ProcessRetriesHandler>().HandleAsync(ct);
        services.AddSingleton(tick);
        services.AddHostedService<SubscriptionReconciliationWorker>();
        await using var provider = services.BuildServiceProvider();

        var hosted = provider.GetServices<IHostedService>().OfType<SubscriptionReconciliationWorker>().ToList();

        hosted.Should().HaveCount(1);
    }

    [Fact]
    public async Task Process_retries_handler_invoked_through_worker_invokes_due_subscriptions()
    {
        var store = new InMemorySubscriptionStore();
        var provider = new NoopSubscriptionProvider();
        var ledger = new NoopCreditLedger();
        var accredit = new AccreditPurchaseHandler(ledger);
        var chargeHandler = new HandleRecurringChargeHandler(store, accredit, NullLogger<HandleRecurringChargeHandler>.Instance);
        var retriesHandler = new ProcessRetriesHandler(store, provider, chargeHandler, NullLogger<ProcessRetriesHandler>.Instance);

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await store.UpsertAsync(Subscription.Create(userId, SubscriptionPlan.Starter, "ps_due", now)
            with
        { Status = SubscriptionStatus.PastDue, NextChargeAt = now.AddMinutes(-5) });

        Func<IServiceProvider, CancellationToken, Task> tick = (_, ct) => retriesHandler.HandleAsync(ct);

        var services = new ServiceCollection();
        services.AddSingleton<IServiceProvider>(sp => sp);
        var worker = new SubscriptionReconciliationWorker(
            tick,
            services.BuildServiceProvider(),
            NullLogger<SubscriptionReconciliationWorker>.Instance,
            TimeSpan.FromMilliseconds(20));

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(150, cts.Token);
        await worker.StopAsync(cts.Token);

        var refreshed = await store.GetByPaymentSourceIdAsync("ps_due");
        refreshed.Should().NotBeNull();
        refreshed!.Status.Should().Be(SubscriptionStatus.Active);
        refreshed.RetryCount.Should().Be(0);
    }

    private static SubscriptionReconciliationWorker CreateWorker(
        Func<IServiceProvider, CancellationToken, Task> tick,
        TimeSpan pollInterval)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IServiceProvider>(sp => sp);
        return new SubscriptionReconciliationWorker(
            tick,
            services.BuildServiceProvider(),
            NullLogger<SubscriptionReconciliationWorker>.Instance,
            pollInterval);
    }

    private sealed class TickCounter
    {
        private int _count;
        private bool _threwOnce;

        public int Count => _count;
        public bool ThrowOnce { get; set; }

        public Task TickAsync(IServiceProvider _, CancellationToken __)
        {
            Interlocked.Increment(ref _count);
            if (ThrowOnce && !_threwOnce)
            {
                _threwOnce = true;
                throw new InvalidOperationException("simulated tick failure");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class NoopSubscriptionProvider : ISubscriptionProvider
    {
        public Task<string> CreateScheduledChargeAsync(string paymentSourceId, decimal amountCop, string currency, DateTime chargeDate, CancellationToken ct = default)
        {
            _ = paymentSourceId;
            _ = amountCop;
            _ = currency;
            _ = chargeDate;
            return Task.FromResult("ch_noop");
        }

        public Task<bool> CancelScheduledChargeAsync(string chargeId, CancellationToken ct = default)
        {
            _ = chargeId;
            return Task.FromResult(true);
        }

        public bool VerifyWebhookSignature(string payload, string signature)
        {
            _ = payload;
            _ = signature;
            return true;
        }
    }

    private sealed class NoopCreditLedger : ICreditLedger
    {
        public Task<CreditLedgerEntry> AccreditAsync(Guid userId, CreditLedgerReason reason, string reference, int delta, int balanceAfter, string? metadata, CancellationToken ct)
        {
            _ = userId;
            _ = reason;
            _ = reference;
            _ = delta;
            _ = balanceAfter;
            _ = metadata;
            return Task.FromResult(new CreditLedgerEntry());
        }

        public Task<CreditLedgerEntry?> FindByReferenceAsync(Guid userId, CreditLedgerReason reason, string reference, CancellationToken ct)
        {
            _ = userId;
            _ = reason;
            _ = reference;
            return Task.FromResult<CreditLedgerEntry?>(null);
        }

        public Task<int> GetBalanceAsync(Guid userId, CancellationToken ct)
        {
            _ = userId;
            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<CreditLedgerEntry>> GetHistoryAsync(Guid userId, int limit, CreditCursorPosition? before, CancellationToken ct)
        {
            _ = userId;
            _ = limit;
            _ = before;
            return Task.FromResult<IReadOnlyList<CreditLedgerEntry>>([]);
        }

        public Task<int> CountConsumptionsSinceAsync(Guid userId, DateTime since, CancellationToken ct)
        {
            _ = userId;
            _ = since;
            return Task.FromResult(0);
        }
    }
}
