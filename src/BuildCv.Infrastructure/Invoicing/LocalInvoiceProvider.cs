using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Invoicing;

public sealed class LocalInvoiceProvider : IInvoiceProvider
{
    private readonly IInvoiceStore _invoiceStore;
    private readonly INumberingRangeStore _numberingRangeStore;
    private readonly ILogger<LocalInvoiceProvider> _logger;

    public LocalInvoiceProvider(IInvoiceStore invoiceStore, INumberingRangeStore numberingRangeStore, ILogger<LocalInvoiceProvider> logger)
    {
        _invoiceStore = invoiceStore;
        _numberingRangeStore = numberingRangeStore;
        _logger = logger;
    }

    public async Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken ct = default)
    {
        var nextNumber = await GetNextNumberAsync("BUILDCV", ct);
        var number = $"BUILDCV-{nextNumber:00000000}";

        var created = invoice with
        {
            Number = number,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _invoiceStore.AddAsync(created, ct);
        _logger.LogInformation("Local invoice created: {Number}", number);

        return created;
    }

    public async Task<Invoice?> GetInvoiceAsync(string number, CancellationToken ct = default)
    {
        return await _invoiceStore.GetByNumberAsync(number, ct);
    }

    public async Task<IReadOnlyList<Invoice>> ListInvoicesAsync(int page = 1, int perPage = 20, CancellationToken ct = default)
    {
        return await _invoiceStore.ListAsync(page, perPage, ct);
    }

    public async Task DeleteInvoiceAsync(string referenceCode, CancellationToken ct = default)
    {
        var invoice = await _invoiceStore.GetByReferenceCodeAsync(referenceCode, ct);
        if (invoice is not null)
        {
            await _invoiceStore.DeleteAsync(invoice.Id, ct);
            _logger.LogInformation("Local invoice deleted: {ReferenceCode}", referenceCode);
        }
    }

    public async Task<byte[]> DownloadPdfAsync(string number, CancellationToken ct = default)
    {
        var invoice = await _invoiceStore.GetByNumberAsync(number, ct);
        if (invoice is null)
        {
            throw new InvalidOperationException($"Invoice {number} not found");
        }

        // Generate a simple PDF for local invoices
        return GenerateLocalPdf(invoice);
    }

    public async Task<byte[]> DownloadXmlAsync(string number, CancellationToken ct = default)
    {
        var invoice = await _invoiceStore.GetByNumberAsync(number, ct);
        if (invoice is null)
        {
            throw new InvalidOperationException($"Invoice {number} not found");
        }

        // Generate a simple XML for local invoices
        return GenerateLocalXml(invoice);
    }

    public async Task<Invoice> CreateCreditNoteAsync(Invoice invoice, CancellationToken ct = default)
    {
        var nextNumber = await GetNextNumberAsync("NC", ct);
        var number = $"NC-{nextNumber:00000000}";

        var created = invoice with
        {
            Number = number,
            DocumentType = InvoiceType.CreditNote,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _invoiceStore.AddAsync(created, ct);
        _logger.LogInformation("Local credit note created: {Number}", number);

        return created;
    }

    public async Task<Invoice> CreateSupportDocumentAsync(Invoice invoice, CancellationToken ct = default)
    {
        var nextNumber = await GetNextNumberAsync("DS", ct);
        var number = $"DS-{nextNumber:00000000}";

        var created = invoice with
        {
            Number = number,
            DocumentType = InvoiceType.SupportDocument,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _invoiceStore.AddAsync(created, ct);
        _logger.LogInformation("Local support document created: {Number}", number);

        return created;
    }

    public async Task<IReadOnlyList<NumberingRange>> GetNumberingRangesAsync(CancellationToken ct = default)
    {
        return await _numberingRangeStore.GetAllAsync(ct);
    }

    public async Task<NumberingRange> CreateNumberingRangeAsync(NumberingRange range, CancellationToken ct = default)
    {
        var created = range with
        {
            Id = Guid.NewGuid(),
            Current = range.From,
            CreatedAt = DateTime.UtcNow
        };

        await _numberingRangeStore.AddAsync(created, ct);
        _logger.LogInformation("Local numbering range created: {Prefix}", created.Prefix);

        return created;
    }

    public async Task<CompanyInfo> GetCompanyAsync(CancellationToken ct = default)
    {
        // Return default company info for local invoices
        return new CompanyInfo
        {
            LegalOrganizationCode = "2",
            Company = "BuildCV Local",
            TradeName = "BuildCV",
            Email = "local@buildcv.com",
            Address = "Local Address",
            RegistrationCode = "LOCAL-000",
            EconomicActivity = "Software Development",
            Phone = "+57 300 000 0000",
            MunicipalityCode = "11001",
            TributeCode = "ZZ",
            Responsibilities = "R-99-PN"
        };
    }

    public async Task<CompanyInfo> UpdateCompanyAsync(CompanyInfo company, CancellationToken ct = default)
    {
        // For local invoices, we just return the updated company info
        _logger.LogInformation("Local company info updated");
        return company;
    }

    private async Task<int> GetNextNumberAsync(string prefix, CancellationToken ct)
    {
        var range = await _numberingRangeStore.GetByPrefixAsync(prefix, ct);
        if (range is null)
        {
            // Auto-create numbering range if it doesn't exist
            range = new NumberingRange
            {
                Id = Guid.NewGuid(),
                Prefix = prefix,
                From = 1,
                To = 99999999,
                Current = 0,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await _numberingRangeStore.AddAsync(range, ct);
        }

        var nextNumber = range.Current + 1;
        await _numberingRangeStore.UpdateAsync(range with { Current = nextNumber }, ct);
        return nextNumber;
    }

    private static byte[] GenerateLocalPdf(Invoice invoice)
    {
        // Simple PDF generation for local invoices
        // In production, this would use a proper PDF library
        var content = $"""
            INVOICE: {invoice.Number}
            Date: {invoice.CreatedAt:yyyy-MM-dd}
            Customer: {invoice.CustomerName}
            Email: {invoice.CustomerEmail}
            Amount: {invoice.AmountInCents:C} {invoice.Currency}
            Status: {invoice.Status}
            """;

        return System.Text.Encoding.UTF8.GetBytes(content);
    }

    private static byte[] GenerateLocalXml(Invoice invoice)
    {
        // Simple XML generation for local invoices
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Invoice>
                <Number>{invoice.Number}</Number>
                <Date>{invoice.CreatedAt:yyyy-MM-dd}</Date>
                <Customer>
                    <Name>{invoice.CustomerName}</Name>
                    <Email>{invoice.CustomerEmail}</Email>
                    <Identification>{invoice.CustomerIdentification}</Identification>
                </Customer>
                <Amount>{invoice.AmountInCents}</Amount>
                <Currency>{invoice.Currency}</Currency>
                <Status>{invoice.Status}</Status>
            </Invoice>
            """;

        return System.Text.Encoding.UTF8.GetBytes(xml);
    }
}
