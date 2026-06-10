using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Invoicing;

public sealed class GetInvoiceHandlerTests
{
    private readonly TestInvoiceStore _store = new();
    private readonly GetInvoiceHandler _handler;

    public GetInvoiceHandlerTests()
    {
        _handler = new GetInvoiceHandler(_store);
    }

    [Fact]
    public async Task HandleAsync_returns_invoice_when_exists()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferenceCode = "BUILDCV-12345678",
            AmountInCents = 100000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test",
            CustomerIdentification = "1234567890",
            CustomerEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(invoice);

        var result = await _handler.HandleAsync(new GetInvoiceQuery { InvoiceId = invoice.Id }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReferenceCode.Should().Be("BUILDCV-12345678");
    }

    [Fact]
    public async Task HandleAsync_returns_failure_when_not_exists()
    {
        var result = await _handler.HandleAsync(new GetInvoiceQuery { InvoiceId = Guid.NewGuid() }, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVOICE/NOT_FOUND");
    }
}
