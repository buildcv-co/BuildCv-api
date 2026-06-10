using BuildCv.Domain.Invoicing;

namespace BuildCv.Application.Features.Invoicing;

public interface IInvoiceProvider
{
    Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice?> GetInvoiceAsync(string number, CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> ListInvoicesAsync(int page = 1, int perPage = 20, CancellationToken ct = default);
    Task DeleteInvoiceAsync(string referenceCode, CancellationToken ct = default);
    Task<byte[]> DownloadPdfAsync(string number, CancellationToken ct = default);
    Task<byte[]> DownloadXmlAsync(string number, CancellationToken ct = default);
    Task<Invoice> CreateCreditNoteAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice> CreateSupportDocumentAsync(Invoice invoice, CancellationToken ct = default);
    Task<IReadOnlyList<NumberingRange>> GetNumberingRangesAsync(CancellationToken ct = default);
    Task<NumberingRange> CreateNumberingRangeAsync(NumberingRange range, CancellationToken ct = default);
    Task<CompanyInfo> GetCompanyAsync(CancellationToken ct = default);
    Task<CompanyInfo> UpdateCompanyAsync(CompanyInfo company, CancellationToken ct = default);
}
