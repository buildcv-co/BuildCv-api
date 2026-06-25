using BuildCv.Application.Common;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Invoicing;

public sealed class FeatureFlagInvoiceAdapter(
    IFeatureFlag flags,
    IInvoiceProvider inner,
    ILogger<FeatureFlagInvoiceAdapter> logger) : IInvoiceProvider
{
    public async Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        if (!enabled)
        {
            logger.LogInformation("Factus disabled by feature flag, using local invoice provider");
            return await LocalInvoiceProviderFallback(invoice, ct);
        }

        return await inner.CreateInvoiceAsync(invoice, ct);
    }

    public async Task<Invoice?> GetInvoiceAsync(string number, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        return enabled
            ? await inner.GetInvoiceAsync(number, ct)
            : await LocalInvoiceProviderFallbackGetAsync(number, ct);
    }

    public async Task<IReadOnlyList<Invoice>> ListInvoicesAsync(int page = 1, int perPage = 20, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        return enabled
            ? await inner.ListInvoicesAsync(page, perPage, ct)
            : await LocalInvoiceProviderFallbackListAsync(page, perPage, ct);
    }

    public async Task DeleteInvoiceAsync(string referenceCode, CancellationToken ct = default)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        if (enabled)
        {
            await inner.DeleteInvoiceAsync(referenceCode, ct);
            return;
        }

        logger.LogInformation("Factus disabled, skipping local delete for reference {Reference}", referenceCode);
    }

    public Task<byte[]> DownloadPdfAsync(string number, CancellationToken ct = default)
        => ForwardOrThrowAsync(number, "PDF", inner.DownloadPdfAsync, ct);

    public Task<byte[]> DownloadXmlAsync(string number, CancellationToken ct = default)
        => ForwardOrThrowAsync(number, "XML", inner.DownloadXmlAsync, ct);

    public Task<Invoice> CreateCreditNoteAsync(Invoice invoice, CancellationToken ct = default)
        => ForwardOrThrowAsync(invoice, "credit note", inner.CreateCreditNoteAsync, ct);

    public Task<Invoice> CreateSupportDocumentAsync(Invoice invoice, CancellationToken ct = default)
        => ForwardOrThrowAsync(invoice, "support document", inner.CreateSupportDocumentAsync, ct);

    public Task<IReadOnlyList<NumberingRange>> GetNumberingRangesAsync(CancellationToken ct = default)
        => ForwardOrThrowAsync(ct, "numbering ranges", inner.GetNumberingRangesAsync);

    public Task<NumberingRange> CreateNumberingRangeAsync(NumberingRange range, CancellationToken ct = default)
        => ForwardOrThrowAsync(range, "numbering range create", inner.CreateNumberingRangeAsync, ct);

    public Task<CompanyInfo> GetCompanyAsync(CancellationToken ct = default)
        => ForwardOrThrowAsync(ct, "company get", inner.GetCompanyAsync);

    public Task<CompanyInfo> UpdateCompanyAsync(CompanyInfo company, CancellationToken ct = default)
        => ForwardOrThrowAsync(company, "company update", inner.UpdateCompanyAsync, ct);

    private async Task<Invoice> LocalInvoiceProviderFallback(Invoice invoice, CancellationToken ct)
    {
        _ = invoice;
        _ = ct;
        throw new InvalidOperationException("Factus is disabled by feature flag and no local fallback is wired at the adapter level");
    }

    private async Task<Invoice?> LocalInvoiceProviderFallbackGetAsync(string number, CancellationToken ct)
    {
        _ = number;
        _ = ct;
        throw new InvalidOperationException("local fallback should have been used for invoice {Number}");
    }

    private async Task<IReadOnlyList<Invoice>> LocalInvoiceProviderFallbackListAsync(int page, int perPage, CancellationToken ct)
    {
        _ = page;
        _ = perPage;
        _ = ct;
        throw new InvalidOperationException("local fallback should have been used for invoice list");
    }

    private async Task<T> ForwardOrThrowAsync<T>(
        string number, string op, Func<string, CancellationToken, Task<T>> forward, CancellationToken ct)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        if (!enabled)
        {
            throw new InvalidOperationException(
                $"Factus is disabled by feature flag — cannot download {op} for {number} (local fallback should have been used)");
        }

        return await forward(number, ct);
    }

    private async Task<T> ForwardOrThrowAsync<T>(
        T value, string op, Func<T, CancellationToken, Task<T>> forward, CancellationToken ct)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        if (!enabled)
        {
            throw new InvalidOperationException(
                $"Factus is disabled by feature flag — cannot create {op} (local fallback should have been used)");
        }

        return await forward(value, ct);
    }

    private async Task<T> ForwardOrThrowAsync<T>(
        CancellationToken ct, string op, Func<CancellationToken, Task<T>> forward)
    {
        var enabled = await flags.IsEnabledAsync("factus-enabled", ct);
        if (!enabled)
        {
            throw new InvalidOperationException(
                $"Factus is disabled by feature flag — cannot read {op} (local fallback should have been used)");
        }

        return await forward(ct);
    }
}
