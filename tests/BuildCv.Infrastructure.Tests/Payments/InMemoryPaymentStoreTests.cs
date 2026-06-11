using BuildCv.Domain.Payments;
using BuildCv.Infrastructure.Payments;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Payments;

public sealed class InMemoryPaymentStoreTests
{
    private readonly InMemoryPaymentStore _store = new();

    [Fact]
    public async Task AddAsync_stores_payment()
    {
        var payment = NewPayment("idem-1", wompiTxId: null);

        await _store.AddAsync(payment);

        var result = await _store.GetByIdAsync(payment.Id);
        result.Should().NotBeNull();
        result!.IdempotencyKey.Should().Be("idem-1");
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_returns_payment()
    {
        var payment = NewPayment("idem-2", wompiTxId: null);
        await _store.AddAsync(payment);

        var result = await _store.GetByIdempotencyKeyAsync("idem-2");

        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task GetByWompiTransactionIdAsync_returns_payment()
    {
        var payment = NewPayment("idem-3", wompiTxId: "tx-abc");
        await _store.AddAsync(payment);

        var result = await _store.GetByWompiTransactionIdAsync("tx-abc");

        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task ListByUserIdAsync_returns_user_payments_ordered_by_created_at_desc()
    {
        var userId = Guid.NewGuid().ToString();
        var older = NewPayment("idem-a", wompiTxId: null, userId: userId, createdAt: DateTime.UtcNow.AddHours(-2));
        var newer = NewPayment("idem-b", wompiTxId: null, userId: userId, createdAt: DateTime.UtcNow);
        await _store.AddAsync(older);
        await _store.AddAsync(newer);

        var result = await _store.ListByUserIdAsync(userId, 1, 20);

        result.Should().HaveCount(2);
        result[0].IdempotencyKey.Should().Be("idem-b");
        result[1].IdempotencyKey.Should().Be("idem-a");
    }

    [Fact]
    public async Task ListByUserIdAsync_paginates_results()
    {
        var userId = Guid.NewGuid().ToString();
        for (var i = 0; i < 5; i++)
        {
            var p = NewPayment($"idem-{i}", wompiTxId: null, userId: userId, createdAt: DateTime.UtcNow.AddSeconds(i));
            await _store.AddAsync(p);
        }

        var page1 = await _store.ListByUserIdAsync(userId, 1, 2);
        var page2 = await _store.ListByUserIdAsync(userId, 2, 2);

        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_replaces_payment_and_keeps_indexes_consistent()
    {
        var payment = NewPayment("idem-u", wompiTxId: null);
        await _store.AddAsync(payment);

        var updated = payment with { Status = PaymentStatus.Approved, PaidAt = DateTime.UtcNow };
        await _store.UpdateAsync(updated);

        var byId = await _store.GetByIdAsync(payment.Id);
        var byKey = await _store.GetByIdempotencyKeyAsync("idem-u");
        byId!.Status.Should().Be(PaymentStatus.Approved);
        byKey!.Status.Should().Be(PaymentStatus.Approved);
    }

    [Fact]
    public async Task UpdateAsync_indexes_new_wompi_transaction_id()
    {
        var payment = NewPayment("idem-w", wompiTxId: null);
        await _store.AddAsync(payment);

        var updated = payment with { WompiTransactionId = "tx-new" };
        await _store.UpdateAsync(updated);

        var result = await _store.GetByWompiTransactionIdAsync("tx-new");
        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
    }

    private static Payment NewPayment(
        string idempotencyKey,
        string? wompiTxId,
        string? userId = null,
        DateTime? createdAt = null)
    {
        var now = createdAt ?? DateTime.UtcNow;
        return new Payment
        {
            Id = Guid.NewGuid(),
            UserId = userId is null ? Guid.NewGuid() : Guid.Parse(userId),
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
