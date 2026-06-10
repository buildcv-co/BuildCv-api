using BuildCv.Domain.Invoicing;
using BuildCv.Infrastructure.Invoicing;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Invoicing;

public sealed class InMemoryInvoiceStoreTests
{
    private readonly InMemoryInvoiceStore _store = new();

    [Fact]
    public async Task AddAsync_stores_invoice()
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

        var result = await _store.GetByIdAsync(invoice.Id);
        result.Should().NotBeNull();
        result!.ReferenceCode.Should().Be("BUILDCV-12345678");
    }

    [Fact]
    public async Task GetByReferenceCodeAsync_returns_invoice()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferenceCode = "BUILDCV-87654321",
            AmountInCents = 200000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test",
            CustomerIdentification = "9876543210",
            CustomerEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(invoice);

        var result = await _store.GetByReferenceCodeAsync("BUILDCV-87654321");

        result.Should().NotBeNull();
        result!.Id.Should().Be(invoice.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_returns_user_invoices()
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

        var result = await _store.GetByUserIdAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_updates_invoice()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferenceCode = "BUILDCV-11111111",
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

        var updated = invoice with { Status = InvoiceStatus.Sent };
        await _store.UpdateAsync(updated);

        var result = await _store.GetByIdAsync(invoice.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(InvoiceStatus.Sent);
    }
}
