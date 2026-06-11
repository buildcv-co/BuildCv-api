using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;

namespace BuildCv.Application.Tests.Features.Payments;

internal sealed class TestInvoiceProvider : IInvoiceProvider
{
    public List<Invoice> CreatedInvoices { get; } = [];

    public Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken ct = default)
    {
        var created = invoice with
        {
            Id = Guid.NewGuid(),
            Number = $"TEST-{CreatedInvoices.Count + 1:00000000}",
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        CreatedInvoices.Add(created);
        return Task.FromResult(created);
    }

    public Task<Invoice?> GetInvoiceAsync(string number, CancellationToken ct = default) =>
        Task.FromResult<Invoice?>(null);

    public Task<IReadOnlyList<Invoice>> ListInvoicesAsync(int page = 1, int perPage = 20, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Invoice>>([]);

    public Task DeleteInvoiceAsync(string referenceCode, CancellationToken ct = default) => Task.CompletedTask;

    public Task<byte[]> DownloadPdfAsync(string number, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());

    public Task<byte[]> DownloadXmlAsync(string number, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());

    public Task<Invoice> CreateCreditNoteAsync(Invoice invoice, CancellationToken ct = default) =>
        Task.FromResult(invoice);

    public Task<Invoice> CreateSupportDocumentAsync(Invoice invoice, CancellationToken ct = default) =>
        Task.FromResult(invoice);

    public Task<IReadOnlyList<NumberingRange>> GetNumberingRangesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<NumberingRange>>([]);

    public Task<NumberingRange> CreateNumberingRangeAsync(NumberingRange range, CancellationToken ct = default) =>
        Task.FromResult(range);

    public Task<CompanyInfo> GetCompanyAsync(CancellationToken ct = default) =>
        Task.FromResult(new CompanyInfo());

    public Task<CompanyInfo> UpdateCompanyAsync(CompanyInfo company, CancellationToken ct = default) =>
        Task.FromResult(company);
}
