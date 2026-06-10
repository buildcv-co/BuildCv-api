using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Invoicing;

public sealed class ListInvoicesHandlerTests
{
    private readonly TestInvoiceStore _store = new();
    private readonly ListInvoicesHandler _handler;

    public ListInvoicesHandlerTests()
    {
        _handler = new ListInvoicesHandler(_store);
    }

    [Fact]
    public async Task HandleAsync_returns_invoices_for_user()
    {
        var userId = Guid.NewGuid();
        var invoice1 = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReferenceCode = "BUILDCV-001",
            AmountInCents = 100000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test 1",
            CustomerIdentification = "1234567890",
            CustomerEmail = "test1@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var invoice2 = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReferenceCode = "BUILDCV-002",
            AmountInCents = 200000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test 2",
            CustomerIdentification = "9876543210",
            CustomerEmail = "test2@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(invoice1);
        await _store.AddAsync(invoice2);

        var result = await _handler.HandleAsync(new ListInvoicesQuery { UserId = userId }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_returns_empty_when_no_invoices()
    {
        var result = await _handler.HandleAsync(new ListInvoicesQuery { UserId = Guid.NewGuid() }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
