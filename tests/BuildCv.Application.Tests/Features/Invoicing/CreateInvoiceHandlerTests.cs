using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Invoicing;

public sealed class CreateInvoiceHandlerTests
{
    private readonly TestInvoiceStore _store = new();
    private readonly CreateInvoiceHandler _handler;

    public CreateInvoiceHandlerTests()
    {
        _handler = new CreateInvoiceHandler(_store);
    }

    [Fact]
    public async Task HandleAsync_creates_invoice_with_reference_code()
    {
        var command = new CreateInvoiceCommand
        {
            UserId = Guid.NewGuid(),
            AmountInCents = 150000,
            CustomerName = "Juan Pérez",
            CustomerIdentification = "1234567890",
            CustomerEmail = "juan@example.com"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReferenceCode.Should().StartWith("BUILDCV-");
        result.Value.AmountInCents.Should().Be(150000);
        result.Value.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public async Task HandleAsync_stores_invoice_in_store()
    {
        var command = new CreateInvoiceCommand
        {
            UserId = Guid.NewGuid(),
            AmountInCents = 50000,
            CustomerName = "Test User",
            CustomerIdentification = "9876543210",
            CustomerEmail = "test@example.com"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        var stored = await _store.GetByIdAsync(result.Value.Id);
        stored.Should().NotBeNull();
        stored!.ReferenceCode.Should().Be(result.Value.ReferenceCode);
    }

    [Fact]
    public async Task HandleAsync_generates_unique_reference_codes()
    {
        var command1 = new CreateInvoiceCommand
        {
            UserId = Guid.NewGuid(),
            AmountInCents = 100000,
            CustomerName = "User 1",
            CustomerIdentification = "1111111111",
            CustomerEmail = "user1@example.com"
        };
        var command2 = new CreateInvoiceCommand
        {
            UserId = Guid.NewGuid(),
            AmountInCents = 200000,
            CustomerName = "User 2",
            CustomerIdentification = "2222222222",
            CustomerEmail = "user2@example.com"
        };

        var result1 = await _handler.HandleAsync(command1, CancellationToken.None);
        var result2 = await _handler.HandleAsync(command2, CancellationToken.None);

        result1.Value.ReferenceCode.Should().NotBe(result2.Value.ReferenceCode);
    }
}
