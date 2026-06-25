using BuildCv.Application.Features.Credits;
using BuildCv.Domain.Credits;
using FluentAssertions;

namespace BuildCv.Application.Tests.Credits;

public sealed class CreditLedgerEntryDtoTests
{
    [Fact]
    public void From_assigns_all_fields()
    {
        var now = DateTime.UtcNow;
        var entry = CreditLedgerEntry.Create(
            userId: Guid.NewGuid(),
            reason: CreditLedgerReason.Purchase,
            reference: "payment:abc",
            delta: 10,
            balanceAfter: 10,
            metadata: "{\"x\":1}",
            createdAt: now);

        var dto = CreditLedgerEntryDto.From(entry);

        dto.Id.Should().Be(entry.Id);
        dto.UserId.Should().Be(entry.UserId);
        dto.Reason.Should().Be(CreditLedgerReason.Purchase);
        dto.Reference.Should().Be("payment:abc");
        dto.Delta.Should().Be(10);
        dto.BalanceAfter.Should().Be(10);
        dto.Metadata.Should().Be("{\"x\":1}");
        dto.CreatedAt.Should().Be(now);
    }
}

public sealed class CreditConsumeResultTests
{
    [Fact]
    public void Success_has_no_error_code()
    {
        var result = new CreditConsumeResult(true, 5, null);

        result.Success.Should().BeTrue();
        result.BalanceAfter.Should().Be(5);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Failure_carries_error_code()
    {
        var result = new CreditConsumeResult(false, 0, "CREDIT/INSUFFICIENT");

        result.Success.Should().BeFalse();
        result.BalanceAfter.Should().Be(0);
        result.ErrorCode.Should().Be("CREDIT/INSUFFICIENT");
    }
}

public sealed class CreditBalanceViewTests
{
    [Fact]
    public void Balance_must_be_non_negative()
    {
        var act = () => new CreditBalanceView(-1, 0);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Balance*");
    }

    [Fact]
    public void Recent_consumption_must_be_non_negative()
    {
        var act = () => new CreditBalanceView(0, -3);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*RecentConsumption*");
    }
}

public sealed class CreditHistoryPageTests
{
    [Fact]
    public void Empty_page_has_no_entries_and_no_cursor()
    {
        var page = new CreditHistoryPage(Array.Empty<CreditLedgerEntryDto>(), null);

        page.Entries.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public void Page_with_entries_stores_them_in_order()
    {
        var first = MakeDto("payment:a", DateTime.UtcNow);
        var second = MakeDto("adapt:x", DateTime.UtcNow.AddMinutes(-1));
        var page = new CreditHistoryPage(new[] { first, second }, "next");

        page.Entries.Should().HaveCount(2);
        page.Entries[0].Reference.Should().Be("payment:a");
        page.Entries[1].Reference.Should().Be("adapt:x");
        page.NextCursor.Should().Be("next");
    }

    private static CreditLedgerEntryDto MakeDto(string reference, DateTime when) =>
        CreditLedgerEntryDto.From(CreditLedgerEntry.Create(
            Guid.NewGuid(),
            CreditLedgerReason.Purchase,
            reference,
            1,
            1,
            createdAt: when));
}
