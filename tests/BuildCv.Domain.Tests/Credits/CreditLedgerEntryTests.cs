using BuildCv.Domain.Credits;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Credits;

public sealed class CreditLedgerEntryTests
{
    [Fact]
    public void Create_throws_when_delta_is_zero()
    {
        var act = () => CreditLedgerEntry.Create(
            userId: Guid.NewGuid(),
            reason: CreditLedgerReason.Purchase,
            reference: "payment:abc",
            delta: 0,
            balanceAfter: 10);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Delta*");
    }

    [Fact]
    public void Create_throws_when_balance_after_is_negative()
    {
        var act = () => CreditLedgerEntry.Create(
            userId: Guid.NewGuid(),
            reason: CreditLedgerReason.Consumption,
            reference: "adapt:abc",
            delta: -1,
            balanceAfter: -1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BalanceAfter*");
    }

    [Fact]
    public void Create_throws_when_reference_is_empty()
    {
        var act = () => CreditLedgerEntry.Create(
            userId: Guid.NewGuid(),
            reason: CreditLedgerReason.Welcome,
            reference: "",
            delta: 3,
            balanceAfter: 3);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Reference*");
    }

    [Fact]
    public void Create_defaults_created_at_to_recent_utc_now()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var entry = CreditLedgerEntry.Create(
            userId: Guid.NewGuid(),
            reason: CreditLedgerReason.Welcome,
            reference: "welcome:abc",
            delta: 3,
            balanceAfter: 3);
        var after = DateTime.UtcNow.AddSeconds(1);

        entry.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        entry.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Create_assigns_new_guid_and_stores_all_fields()
    {
        var userId = Guid.NewGuid();
        var entry = CreditLedgerEntry.Create(
            userId: userId,
            reason: CreditLedgerReason.Purchase,
            reference: "payment:abc",
            delta: 10,
            balanceAfter: 10,
            metadata: "{\"id\":\"abc\"}");

        entry.Id.Should().NotBe(Guid.Empty);
        entry.UserId.Should().Be(userId);
        entry.Reason.Should().Be(CreditLedgerReason.Purchase);
        entry.Reference.Should().Be("payment:abc");
        entry.Delta.Should().Be(10);
        entry.BalanceAfter.Should().Be(10);
        entry.Metadata.Should().Be("{\"id\":\"abc\"}");
    }

    [Fact]
    public void Create_accepts_negative_delta_for_consumption_and_refund()
    {
        var consume = CreditLedgerEntry.Create(
            userId: Guid.NewGuid(),
            reason: CreditLedgerReason.Consumption,
            reference: "adapt:x",
            delta: -1,
            balanceAfter: 0);

        consume.Delta.Should().Be(-1);

        var refund = CreditLedgerEntry.Create(
            userId: Guid.NewGuid(),
            reason: CreditLedgerReason.Refund,
            reference: "adapt:x:refund",
            delta: 1,
            balanceAfter: 1);

        refund.Delta.Should().Be(1);
    }

    [Fact]
    public void CreditLedgerReason_has_all_five_values_with_expected_ordering()
    {
        ((int)CreditLedgerReason.Welcome).Should().Be(1);
        ((int)CreditLedgerReason.Purchase).Should().Be(2);
        ((int)CreditLedgerReason.Consumption).Should().Be(3);
        ((int)CreditLedgerReason.Refund).Should().Be(4);
        ((int)CreditLedgerReason.ManualAdjustment).Should().Be(5);
    }
}
