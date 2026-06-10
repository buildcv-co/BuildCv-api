using BuildCv.Domain.Invoicing;
using BuildCv.Infrastructure.Invoicing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Infrastructure.Tests.Invoicing;

public sealed class LocalInvoiceProviderTests
{
    private readonly InMemoryInvoiceStore _invoiceStore = new();
    private readonly InMemoryNumberingRangeStore _numberingRangeStore = new();
    private readonly LocalInvoiceProvider _provider;

    public LocalInvoiceProviderTests()
    {
        _provider = new LocalInvoiceProvider(
            _invoiceStore,
            _numberingRangeStore,
            NullLogger<LocalInvoiceProvider>.Instance);
    }

    [Fact]
    public async Task CreateInvoiceAsync_creates_invoice_with_number()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferenceCode = "REF-001",
            AmountInCents = 150000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test User",
            CustomerIdentification = "1234567890",
            CustomerEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _provider.CreateInvoiceAsync(invoice);

        result.Number.Should().StartWith("BUILDCV-");
        result.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public async Task GetInvoiceAsync_returns_invoice()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferenceCode = "REF-002",
            AmountInCents = 200000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test User",
            CustomerIdentification = "9876543210",
            CustomerEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _invoiceStore.AddAsync(invoice);

        var result = await _provider.GetInvoiceAsync(invoice.Number!);

        result.Should().NotBeNull();
        result!.ReferenceCode.Should().Be("REF-002");
    }

    [Fact]
    public async Task ListInvoicesAsync_returns_invoices()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferenceCode = "REF-003",
            AmountInCents = 300000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test User",
            CustomerIdentification = "5555555555",
            CustomerEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _invoiceStore.AddAsync(invoice);

        var result = await _provider.ListInvoicesAsync();

        result.Should().Contain(i => i.ReferenceCode == "REF-003");
    }

    [Fact]
    public async Task DeleteInvoiceAsync_removes_invoice()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferenceCode = "REF-004",
            AmountInCents = 400000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test User",
            CustomerIdentification = "6666666666",
            CustomerEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _invoiceStore.AddAsync(invoice);

        await _provider.DeleteInvoiceAsync("REF-004");

        var result = await _invoiceStore.GetByReferenceCodeAsync("REF-004");
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateCreditNoteAsync_creates_credit_note()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferenceCode = "REF-005",
            AmountInCents = 500000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test User",
            CustomerIdentification = "7777777777",
            CustomerEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _provider.CreateCreditNoteAsync(invoice);

        result.Number.Should().StartWith("NC-");
        result.DocumentType.Should().Be(InvoiceType.CreditNote);
    }

    [Fact]
    public async Task CreateSupportDocumentAsync_creates_support_document()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferenceCode = "REF-006",
            AmountInCents = 600000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test User",
            CustomerIdentification = "8888888888",
            CustomerEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _provider.CreateSupportDocumentAsync(invoice);

        result.Number.Should().StartWith("DS-");
        result.DocumentType.Should().Be(InvoiceType.SupportDocument);
    }

    [Fact]
    public async Task GetCompanyAsync_returns_default_company()
    {
        var result = await _provider.GetCompanyAsync();

        result.Company.Should().Be("BuildCV Local");
        result.Email.Should().Be("local@buildcv.com");
    }

    [Fact]
    public async Task GetNumberingRangesAsync_returns_ranges()
    {
        var range = new NumberingRange
        {
            Id = Guid.NewGuid(),
            Prefix = "BUILDCV",
            From = 1,
            To = 99999999,
            Current = 0,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        await _numberingRangeStore.AddAsync(range);

        var result = await _provider.GetNumberingRangesAsync();

        result.Should().Contain(r => r.Prefix == "BUILDCV");
    }
}
