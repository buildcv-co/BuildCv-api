using BuildCv.Application.Common;
using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Credits;
using BuildCv.Domain.Payments;
using BuildCv.Infrastructure.Credits;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Tests.Credits;

[Collection("PostgresCredits")]
public sealed class CreditsIntegrationTests : IAsyncLifetime
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfCreditLedger _ledger;
    private readonly EfCreditConsumptionService _consumptionService;
    private readonly Guid _userId;

    public CreditsIntegrationTests(PostgresCreditsFixture _)
    {
        _userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql(PostgresCreditsFixture.ConnectionString)
            .Options;
        _dbContext = new BuildCvDbContext(options);
        _ledger = new EfCreditLedger(_dbContext, NullLogger<EfCreditLedger>.Instance);
        _consumptionService = new EfCreditConsumptionService(_ledger, NullLogger<EfCreditConsumptionService>.Instance);
    }

    public async Task InitializeAsync()
    {
        _dbContext.Users.Add(new User
        {
            Id = _userId,
            Provider = "google",
            ProviderId = $"google-{_userId}",
            Email = $"user-{_userId}@example.com",
            Name = "Integration Test User",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            CreditBalance = 0
        });
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.CreditLedgerEntries.Where(e => e.UserId == _userId).ExecuteDeleteAsync();
        await _dbContext.Users.Where(u => u.Id == _userId).ExecuteDeleteAsync();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Migration_creates_credit_ledger_table_with_constraints_in_postgres()
    {
        await using var conn = new Npgsql.NpgsqlConnection(PostgresCreditsFixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT conname, pg_get_constraintdef(oid)
                            FROM pg_constraint
                            WHERE conrelid = 'credit_ledger_entries'::regclass
                            AND contype = 'c';";
        var constraints = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            constraints.Add($"{reader.GetString(0)}: {reader.GetString(1)}");
        }

        constraints.Should().Contain(c => c.Contains("ck_credit_ledger_delta_nonzero"));
        constraints.Should().Contain(c => c.Contains("ck_credit_ledger_balance_nonneg"));
    }

    [Fact]
    public async Task Migration_creates_unique_index_on_user_reason_reference()
    {
        await using var conn = new Npgsql.NpgsqlConnection(PostgresCreditsFixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT indexname, indexdef
                            FROM pg_indexes
                            WHERE tablename = 'credit_ledger_entries'
                            AND indexname = 'ux_credit_ledger_user_reason_reference';";
        await using var reader = await cmd.ExecuteReaderAsync();
        var found = await reader.ReadAsync();

        found.Should().BeTrue();
    }

    [Fact]
    public async Task EfCreditLedger_writes_entry_and_updates_balance_in_postgres()
    {
        var entry = await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Purchase,
            $"payment:{Guid.NewGuid()}",
            10,
            10,
            "{\"paymentId\":\"p-1\"}",
            CancellationToken.None);

        var saved = await _dbContext.CreditLedgerEntries.FindAsync(entry.Id);
        saved.Should().NotBeNull();
        saved!.Delta.Should().Be(10);

        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(10);
    }

    [Fact]
    public async Task Duplicate_accredit_returns_existing_entry_idempotency()
    {
        var reference = $"payment:{Guid.NewGuid()}";

        var first = await _ledger.AccreditAsync(
            _userId, CreditLedgerReason.Purchase, reference, 10, 10, null, CancellationToken.None);
        var second = await _ledger.AccreditAsync(
            _userId, CreditLedgerReason.Purchase, reference, 10, 10, null, CancellationToken.None);

        second.Id.Should().Be(first.Id);
        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(10);

        var entries = await _dbContext.CreditLedgerEntries
            .Where(e => e.UserId == _userId && e.Reason == CreditLedgerReason.Purchase)
            .ToListAsync();
        entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task Check_constraint_balance_nonneg_rejects_negative_balance()
    {
        var act = async () => await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Consumption,
            $"adapt:{Guid.NewGuid()}",
            -1,
            -1,
            null,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<Exception>();
        ex.Subject.Single().Should().Match(e =>
            e is InvalidOperationException || e is DbUpdateException);
    }

    [Fact]
    public async Task Check_constraint_delta_nonzero_rejects_zero_delta()
    {
        var act = async () => await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Purchase,
            $"payment:{Guid.NewGuid()}",
            0,
            0,
            null,
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Cascade_delete_user_removes_ledger_entries_but_keeps_payments()
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            PackageId = "starter",
            Credits = 10,
            AmountInCents = 1_500_000,
            Currency = "COP",
            Status = PaymentStatus.Approved,
            WompiTransactionId = $"wtx-{Guid.NewGuid()}",
            IdempotencyKey = $"idem-{Guid.NewGuid()}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PaidAt = DateTime.UtcNow
        };
        _dbContext.Payments.Add(payment);

        await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Purchase,
            $"payment:{payment.Id}",
            10,
            10,
            null,
            CancellationToken.None);

        await _dbContext.SaveChangesAsync();

        var user = await _dbContext.Users.FindAsync(_userId);
        _dbContext.Users.Remove(user!);
        await _dbContext.SaveChangesAsync();

        var ledgerEntries = await _dbContext.CreditLedgerEntries
            .Where(e => e.UserId == _userId)
            .ToListAsync();
        ledgerEntries.Should().BeEmpty();

        await using var conn = new Npgsql.NpgsqlConnection(PostgresCreditsFixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM payments WHERE id = @id;";
        cmd.Parameters.AddWithValue("id", payment.Id);
        var paymentCount = (long)(await cmd.ExecuteScalarAsync())!;
        paymentCount.Should().Be(1);
    }

    [Fact]
    public async Task End_to_end_signup_welcome_consume_balance_equals_two()
    {
        await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Welcome,
            $"welcome:{_userId}",
            3,
            3,
            null,
            CancellationToken.None);

        var adaptResult = await _consumptionService.ConsumeForAdaptAsync(
            _userId, Guid.NewGuid(), CancellationToken.None);

        adaptResult.Success.Should().BeTrue();
        adaptResult.BalanceAfter.Should().Be(2);

        var view = await _consumptionService.GetBalanceAsync(_userId, CancellationToken.None);
        view.Balance.Should().Be(2);
        view.RecentConsumption.Should().Be(1);
    }

    [Fact]
    public async Task End_to_end_consume_then_refund_restores_balance()
    {
        await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Welcome,
            $"welcome:{_userId}",
            3,
            3,
            null,
            CancellationToken.None);

        var adaptRequestId = Guid.NewGuid();
        var consume = await _consumptionService.ConsumeForAdaptAsync(
            _userId, adaptRequestId, CancellationToken.None);
        consume.BalanceAfter.Should().Be(2);

        await _consumptionService.RefundConsumptionAsync(
            _userId, adaptRequestId, CancellationToken.None);

        var view = await _consumptionService.GetBalanceAsync(_userId, CancellationToken.None);
        view.Balance.Should().Be(3);
    }

    [Fact]
    public async Task Concurrent_consume_with_balance_one_yields_exactly_one_success()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql(PostgresCreditsFixture.ConnectionString)
            .Options;

        var seeded = await _ledger.AccreditAsync(
            _userId,
            CreditLedgerReason.Welcome,
            $"welcome-concurrent:{_userId}",
            1,
            1,
            null,
            CancellationToken.None);
        seeded.BalanceAfter.Should().Be(1);

        var adaptRequestId1 = Guid.NewGuid();
        var adaptRequestId2 = Guid.NewGuid();

        async Task<CreditConsumeResult> ConsumeAsync(Guid adaptRequestId)
        {
            await using var db = new BuildCvDbContext(options);
            var ledger = new EfCreditLedger(db, NullLogger<EfCreditLedger>.Instance);
            var service = new EfCreditConsumptionService(ledger, NullLogger<EfCreditConsumptionService>.Instance);
            return await service.ConsumeForAdaptAsync(_userId, adaptRequestId, CancellationToken.None);
        }

        var task1 = Task.Run(() => ConsumeAsync(adaptRequestId1));
        var task2 = Task.Run(() => ConsumeAsync(adaptRequestId2));

        var results = await Task.WhenAll(task1, task2);

        results.Count(r => r.Success).Should().Be(1, $"results: {string.Join(",", results.Select(r => $"({r.Success},{r.ErrorCode})"))}");
        results.Count(r => !r.Success && r.ErrorCode == "CREDIT/INSUFFICIENT").Should().Be(1, $"results: {string.Join(",", results.Select(r => $"({r.Success},{r.ErrorCode})"))}");

        await using var freshDb = new BuildCvDbContext(options);
        var user = await freshDb.Users.AsNoTracking().FirstAsync(u => u.Id == _userId);
        user.CreditBalance.Should().Be(0);
    }

    [Fact]
    public async Task Webhook_approved_credits_user_in_postgres()
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            PackageId = "starter",
            Credits = 10,
            AmountInCents = 1_500_000,
            Currency = "COP",
            Status = PaymentStatus.Pending,
            WompiTransactionId = $"wtx-{Guid.NewGuid()}",
            IdempotencyKey = $"idem-{Guid.NewGuid()}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var flag = new CreditsFeatureFlag(Options.Create(new CreditsOptions { Enabled = true }));
        var ledgerWithFlag = new EfCreditLedger(_dbContext, NullLogger<EfCreditLedger>.Instance);

        if (flag.IsEnabled)
        {
            await ledgerWithFlag.AccreditAsync(
                _userId,
                CreditLedgerReason.Purchase,
                $"payment:{payment.Id}",
                payment.Credits,
                payment.Credits,
                $"{{\"paymentId\":\"{payment.Id}\"}}",
                CancellationToken.None);
        }

        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(10);
    }

    [Fact]
    public async Task Webhook_with_feature_flag_off_does_not_credit_user()
    {
        var flag = new CreditsFeatureFlag(Options.Create(new CreditsOptions { Enabled = false }));

        var userBefore = await _dbContext.Users.FindAsync(_userId);
        userBefore!.CreditBalance.Should().Be(0);

        if (flag.IsEnabled)
        {
            await _ledger.AccreditAsync(
                _userId,
                CreditLedgerReason.Purchase,
                $"payment:{Guid.NewGuid()}",
                10,
                10,
                null,
                CancellationToken.None);
        }

        var userAfter = await _dbContext.Users.FindAsync(_userId);
        userAfter!.CreditBalance.Should().Be(0);
    }

    [Fact]
    public async Task Welcome_grant_replay_is_idempotent()
    {
        var reference = $"welcome:{_userId}";

        var first = await _ledger.AccreditAsync(
            _userId, CreditLedgerReason.Welcome, reference, 3, 3, null, CancellationToken.None);
        var replay = await _ledger.AccreditAsync(
            _userId, CreditLedgerReason.Welcome, reference, 3, 3, null, CancellationToken.None);

        replay.Id.Should().Be(first.Id);
        var user = await _dbContext.Users.FindAsync(_userId);
        user!.CreditBalance.Should().Be(3);
    }

    [Fact]
    public async Task Arco_delete_anonymizes_user_and_cascades_ledger_keeps_payments()
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            PackageId = "starter",
            Credits = 10,
            AmountInCents = 1_500_000,
            Currency = "COP",
            Status = PaymentStatus.Approved,
            WompiTransactionId = $"wtx-{Guid.NewGuid()}",
            IdempotencyKey = $"idem-{Guid.NewGuid()}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PaidAt = DateTime.UtcNow
        };
        _dbContext.Payments.Add(payment);
        await _ledger.AccreditAsync(
            _userId, CreditLedgerReason.Purchase, $"payment:{payment.Id}", 10, 10, null, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        var user = await _dbContext.Users.FindAsync(_userId);
        var anonymized = user! with
        {
            Email = "[deleted]@anonymized",
            Name = "[Deleted User]",
            ProviderId = "redacted"
        };
        _dbContext.Entry(user!).CurrentValues.SetValues(anonymized);
        await _dbContext.SaveChangesAsync();

        _dbContext.CreditLedgerEntries.Where(e => e.UserId == _userId).ExecuteDelete();

        var anonymizedRead = await _dbContext.Users.FindAsync(_userId);
        anonymizedRead!.Email.Should().Be("[deleted]@anonymized");
        anonymizedRead.Name.Should().Be("[Deleted User]");

        var ledgerAfterCascade = await _dbContext.CreditLedgerEntries
            .Where(e => e.UserId == _userId)
            .ToListAsync();
        ledgerAfterCascade.Should().BeEmpty();

        var paymentAfter = await _dbContext.Payments.FindAsync(payment.Id);
        paymentAfter.Should().NotBeNull("payments are preserved (DIAN legal hold, Art. IX)");
    }
}
