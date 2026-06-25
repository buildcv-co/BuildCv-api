using BuildCv.Application.Common;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;

namespace BuildCv.Infrastructure.Invoicing;

public sealed class FeatureFlagInvoiceAdapter(
    IFeatureFlag flags,
    FactusAdapter factusAdapter,
    LocalInvoiceProvider localProvider) : IInvoiceProvider
{
    public async Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        return enabled
            ? await factusAdapter.CreateInvoiceAsync(invoice, ct)
            : await localProvider.CreateInvoiceAsync(invoice, ct);
    }

    public async Task<Invoice?> GetInvoiceAsync(string number, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        return enabled
            ? await factusAdapter.GetInvoiceAsync(number, ct)
            : await localProvider.GetInvoiceAsync(number, ct);
    }

    public async Task<IReadOnlyList<Invoice>> ListInvoicesAsync(int page = 1, int perPage = 20, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        return enabled
            ? await factusAdapter.ListInvoicesAsync(page, perPage, ct)
            : await localProvider.ListInvoicesAsync(page, perPage, ct);
    }

    public async Task DeleteInvoiceAsync(string referenceCode, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        if (enabled)
        {
            await factusAdapter.DeleteInvoiceAsync(referenceCode, ct);
            return;
        }

        await localProvider.DeleteInvoiceAsync(referenceCode, ct);
    }

    public Task<byte[]> DownloadPdfAsync(string number, CancellationToken ct = default)
        => ForwardAsync(number, factusAdapter.DownloadPdfAsync, localProvider.DownloadPdfAsync, ct);

    public Task<byte[]> DownloadXmlAsync(string number, CancellationToken ct = default)
        => ForwardAsync(number, factusAdapter.DownloadXmlAsync, localProvider.DownloadXmlAsync, ct);

    public Task<Invoice> CreateCreditNoteAsync(Invoice invoice, CancellationToken ct = default)
        => ForwardAsync(invoice, factusAdapter.CreateCreditNoteAsync, localProvider.CreateCreditNoteAsync, ct);

    public Task<Invoice> CreateSupportDocumentAsync(Invoice invoice, CancellationToken ct = default)
        => ForwardAsync(invoice, factusAdapter.CreateSupportDocumentAsync, localProvider.CreateSupportDocumentAsync, ct);

    public Task<IReadOnlyList<NumberingRange>> GetNumberingRangesAsync(CancellationToken ct = default)
        => ForwardAsync(ct, factusAdapter.GetNumberingRangesAsync, localProvider.GetNumberingRangesAsync);

    public Task<NumberingRange> CreateNumberingRangeAsync(NumberingRange range, CancellationToken ct = default)
        => ForwardAsync(range, factusAdapter.CreateNumberingRangeAsync, localProvider.CreateNumberingRangeAsync, ct);

    public Task<CompanyInfo> GetCompanyAsync(CancellationToken ct = default)
        => ForwardAsync(ct, factusAdapter.GetCompanyAsync, localProvider.GetCompanyAsync);

    public Task<CompanyInfo> UpdateCompanyAsync(CompanyInfo company, CancellationToken ct = default)
        => ForwardAsync(company, factusAdapter.UpdateCompanyAsync, localProvider.UpdateCompanyAsync, ct);

    private async Task<T> ForwardAsync<T>(
        string number,
        Func<string, CancellationToken, Task<T>> factusCall,
        Func<string, CancellationToken, Task<T>> localCall,
        CancellationToken ct)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        return enabled
            ? await factusCall(number, ct)
            : await localCall(number, ct);
    }

    private async Task<T> ForwardAsync<T>(
        T value,
        Func<T, CancellationToken, Task<T>> factusCall,
        Func<T, CancellationToken, Task<T>> localCall,
        CancellationToken ct)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        return enabled
            ? await factusCall(value, ct)
            : await localCall(value, ct);
    }

    private async Task<T> ForwardAsync<T>(
        CancellationToken ct,
        Func<CancellationToken, Task<T>> factusCall,
        Func<CancellationToken, Task<T>> localCall)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        return enabled
            ? await factusCall(ct)
            : await localCall(ct);
    }
}
