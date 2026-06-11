using BuildCv.Domain.Payments;
using BuildCv.Infrastructure.Payments;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Tests.Payments;

public sealed class EfPaymentStoreTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfPaymentStore _store;

    public EfPaymentStoreTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new BuildCvDbContext(options);
        _store = new EfPaymentStore(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task AddAsync_inserts_payment()
    {
        var payment = NewPayment("idem-1", wompiTxId: null);

        await _store.AddAsync(payment);

        var result = await _dbContext.Payments.FindAsync(payment.Id);
        result.Should().NotBeNull();
        result!.IdempotencyKey.Should().Be("idem-1");
    }

    [Fact]
    public async Task GetByIdAsync_returns_payment()
    {
        var payment = NewPayment("idem-2", wompiTxId: null);
        await _store.AddAsync(payment);

        var result = await _store.GetByIdAsync(payment.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_returns_payment()
    {
        var payment = NewPayment("idem-3", wompiTxId: null);
        await _store.AddAsync(payment);

        var result = await _store.GetByIdempotencyKeyAsync("idem-3");

        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task GetByWompiTransactionIdAsync_returns_payment()
    {
        var payment = NewPayment("idem-4", wompiTxId: "tx-abc");
        await _store.AddAsync(payment);

        var result = await _store.GetByWompiTransactionIdAsync("tx-abc");

        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task ListByUserIdAsync_returns_user_payments_ordered_by_created_at_desc()
    {
        var userId = Guid.NewGuid();
        var older = NewPayment("idem-a", wompiTxId: null, userId: userId, createdAt: DateTime.UtcNow.AddHours(-2));
        var newer = NewPayment("idem-b", wompiTxId: null, userId: userId, createdAt: DateTime.UtcNow);
        await _store.AddAsync(older);
        await _store.AddAsync(newer);

        var result = await _store.ListByUserIdAsync(userId.ToString(), 1, 20);

        result.Should().HaveCount(2);
        result[0].IdempotencyKey.Should().Be("idem-b");
        result[1].IdempotencyKey.Should().Be("idem-a");
    }

    [Fact]
    public async Task ListByUserIdAsync_paginates_results()
    {
        var userId = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            var p = NewPayment($"idem-{i}", wompiTxId: null, userId: userId, createdAt: DateTime.UtcNow.AddSeconds(i));
            await _store.AddAsync(p);
        }

        var page1 = await _store.ListByUserIdAsync(userId.ToString(), 1, 2);
        var page2 = await _store.ListByUserIdAsync(userId.ToString(), 2, 2);

        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_persists_changes()
    {
        var payment = NewPayment("idem-u", wompiTxId: null);
        await _store.AddAsync(payment);

        var updated = payment with { Status = PaymentStatus.Approved, PaidAt = DateTime.UtcNow };
        await _store.UpdateAsync(updated);

        var result = await _store.GetByIdAsync(payment.Id);
        result!.Status.Should().Be(PaymentStatus.Approved);
        result.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_returns_null_when_not_found()
    {
        var result = await _store.GetByIdempotencyKeyAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByWompiTransactionIdAsync_returns_null_when_not_found()
    {
        var result = await _store.GetByWompiTransactionIdAsync("nonexistent");
        result.Should().BeNull();
    }

    private static Payment NewPayment(
        string idempotencyKey,
        string? wompiTxId,
        Guid? userId = null,
        DateTime? createdAt = null)
    {
        var now = createdAt ?? DateTime.UtcNow;
        return new Payment
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            PackageId = "starter",
            Credits = 10,
            AmountInCents = 1_500_000,
            Currency = "COP",
            Status = PaymentStatus.Pending,
            WompiTransactionId = wompiTxId,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
