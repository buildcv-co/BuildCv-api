using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Invoicing;

public sealed class FactusAdapter : IInvoiceProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly FactusSettings _settings;
    private readonly ILogger<FactusAdapter> _logger;
    private string? _accessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    public FactusAdapter(HttpClient http, IOptions<FactusSettings> settings, ILogger<FactusAdapter> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresAt)
            return;

        var response = await _http.PostAsJsonAsync($"{_settings.BaseUrl}/api/v1/auth/access-token", new
        {
            grant_type = "password",
            client_id = _settings.ClientId,
            client_secret = _settings.ClientSecret,
            username = _settings.Email,
            password = _settings.Password
        }, ct);

        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
        _accessToken = data!.AccessToken;
        _tokenExpiresAt = DateTime.UtcNow.AddSeconds(data.ExpiresIn - 300);

        _logger.LogInformation("Factus token refreshed, expires at {ExpiresAt}", _tokenExpiresAt);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        await EnsureTokenAsync(ct);
        var response = await _http.GetAsync($"{_settings.BaseUrl}{path}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new InvalidOperationException("Null response");
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        await EnsureTokenAsync(ct);
        var response = await _http.PostAsJsonAsync($"{_settings.BaseUrl}{path}", body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new InvalidOperationException("Null response");
    }

    private async Task<T> PutAsync<T>(string path, object body, CancellationToken ct)
    {
        await EnsureTokenAsync(ct);
        var response = await _http.PutAsJsonAsync($"{_settings.BaseUrl}{path}", body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new InvalidOperationException("Null response");
    }

    private async Task DeleteAsync(string path, CancellationToken ct)
    {
        await EnsureTokenAsync(ct);
        var response = await _http.DeleteAsync($"{_settings.BaseUrl}{path}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken ct = default)
    {
        var response = await PostAsync<FactusInvoiceResponse>("/api/v1/bills", new
        {
            numbering_range_id = 1,
            reference_code = invoice.ReferenceCode,
            observation = "Invoice from BuildCV",
            payment_method_code = "10",
            customer = new
            {
                identification = invoice.CustomerIdentification,
                identification_document_code = invoice.CustomerIdentificationDocumentCode,
                company = invoice.CustomerCompany,
                trade_name = invoice.CustomerTradeName,
                email = invoice.CustomerEmail,
                phone = invoice.CustomerPhone,
                address = invoice.CustomerAddress,
                legal_organization_code = invoice.CustomerLegalOrganizationCode,
                tribute_code = invoice.CustomerTributeCode,
                municipality_code = invoice.CustomerMunicipalityCode
            },
            items = new[]
            {
                new
                {
                    code = "CV-001",
                    description = invoice.ItemsDescription,
                    quantity = 1,
                    unit_price = invoice.AmountInCents
                }
            }
        }, ct);

        return invoice with
        {
            Number = response.Data.Number,
            Uuid = response.Data.Uuid,
            Status = InvoiceStatus.Sent,
            SentAt = DateTime.UtcNow,
            FactusResponseJson = JsonSerializer.Serialize(response)
        };
    }

    public async Task<Invoice?> GetInvoiceAsync(string number, CancellationToken ct = default)
    {
        var response = await GetAsync<FactusBillResponse>($"/api/v1/bills/{number}", ct);
        if (response.Data is null) return null;

        return new Invoice
        {
            Number = response.Data.Number,
            Uuid = response.Data.Uuid,
            Status = InvoiceStatus.Accepted,
            FactusResponseJson = JsonSerializer.Serialize(response)
        };
    }

    public async Task<IReadOnlyList<Invoice>> ListInvoicesAsync(int page = 1, int perPage = 20, CancellationToken ct = default)
    {
        var response = await GetAsync<FactusBillsResponse>($"/api/v1/bills?page={page}&per_page={perPage}", ct);
        return response.Data.Select(b => new Invoice
        {
            Number = b.Number,
            Uuid = b.Uuid,
            Status = InvoiceStatus.Accepted,
            FactusResponseJson = JsonSerializer.Serialize(b)
        }).ToList();
    }

    public async Task DeleteInvoiceAsync(string referenceCode, CancellationToken ct = default)
    {
        await DeleteAsync($"/api/v1/bills/{referenceCode}", ct);
    }

    public async Task<byte[]> DownloadPdfAsync(string number, CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct);
        var response = await _http.GetAsync($"{_settings.BaseUrl}/api/v1/bills/{number}/pdf", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<byte[]> DownloadXmlAsync(string number, CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct);
        var response = await _http.GetAsync($"{_settings.BaseUrl}/api/v1/bills/{number}/xml", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<Invoice> CreateCreditNoteAsync(Invoice invoice, CancellationToken ct = default)
    {
        var response = await PostAsync<FactusInvoiceResponse>("/api/v1/credit-notes", new
        {
            numbering_range_id = 2,
            reference_code = invoice.ReferenceCode,
            observation = "Credit note from BuildCV",
            payment_method_code = "10",
            customer = new
            {
                identification = invoice.CustomerIdentification,
                identification_document_code = invoice.CustomerIdentificationDocumentCode,
                email = invoice.CustomerEmail,
                phone = invoice.CustomerPhone,
                address = invoice.CustomerAddress,
                legal_organization_code = invoice.CustomerLegalOrganizationCode,
                tribute_code = invoice.CustomerTributeCode,
                municipality_code = invoice.CustomerMunicipalityCode
            },
            items = new[]
            {
                new
                {
                    code = "CN-001",
                    description = invoice.ItemsDescription,
                    quantity = 1,
                    unit_price = invoice.AmountInCents
                }
            }
        }, ct);

        return invoice with
        {
            Number = response.Data.Number,
            Uuid = response.Data.Uuid,
            Status = InvoiceStatus.Sent,
            SentAt = DateTime.UtcNow,
            FactusResponseJson = JsonSerializer.Serialize(response)
        };
    }

    public async Task<Invoice> CreateSupportDocumentAsync(Invoice invoice, CancellationToken ct = default)
    {
        var response = await PostAsync<FactusInvoiceResponse>("/api/v1/support-documents", new
        {
            numbering_range_id = 3,
            reference_code = invoice.ReferenceCode,
            observation = "Support document from BuildCV",
            payment_method_code = "10",
            customer = new
            {
                identification = invoice.CustomerIdentification,
                identification_document_code = invoice.CustomerIdentificationDocumentCode,
                email = invoice.CustomerEmail,
                phone = invoice.CustomerPhone,
                address = invoice.CustomerAddress,
                legal_organization_code = invoice.CustomerLegalOrganizationCode,
                tribute_code = invoice.CustomerTributeCode,
                municipality_code = invoice.CustomerMunicipalityCode
            },
            items = new[]
            {
                new
                {
                    code = "SD-001",
                    description = invoice.ItemsDescription,
                    quantity = 1,
                    unit_price = invoice.AmountInCents
                }
            }
        }, ct);

        return invoice with
        {
            Number = response.Data.Number,
            Uuid = response.Data.Uuid,
            Status = InvoiceStatus.Sent,
            SentAt = DateTime.UtcNow,
            FactusResponseJson = JsonSerializer.Serialize(response)
        };
    }

    public async Task<IReadOnlyList<NumberingRange>> GetNumberingRangesAsync(CancellationToken ct = default)
    {
        var response = await GetAsync<FactusNumberingRangesResponse>("/api/v1/numbering-ranges", ct);
        return response.Data.Select(r => new NumberingRange
        {
            ProviderId = r.Id,
            Prefix = r.Prefix,
            From = r.From,
            To = r.To,
            Current = r.Current,
            Status = r.Status
        }).ToList();
    }

    public async Task<NumberingRange> CreateNumberingRangeAsync(NumberingRange range, CancellationToken ct = default)
    {
        var response = await PostAsync<FactusNumberingRangeResponse>("/api/v1/numbering-ranges", new
        {
            prefix = range.Prefix,
            from = range.From,
            to = range.To,
            status = range.Status
        }, ct);

        return range with
        {
            ProviderId = response.Data.Id,
            Current = response.Data.Current
        };
    }

    public async Task<CompanyInfo> GetCompanyAsync(CancellationToken ct = default)
    {
        var response = await GetAsync<FactusCompanyResponse>("/api/v1/company", ct);
        return new CompanyInfo
        {
            LegalOrganizationCode = response.Data.LegalOrganizationCode,
            Company = response.Data.Company,
            TradeName = response.Data.TradeName,
            Email = response.Data.Email,
            Address = response.Data.Address,
            RegistrationCode = response.Data.RegistrationCode,
            EconomicActivity = response.Data.EconomicActivity,
            Phone = response.Data.Phone,
            MunicipalityCode = response.Data.MunicipalityCode,
            TributeCode = response.Data.TributeCode,
            Responsibilities = response.Data.Responsibilities
        };
    }

    public async Task<CompanyInfo> UpdateCompanyAsync(CompanyInfo company, CancellationToken ct = default)
    {
        var response = await PutAsync<FactusCompanyResponse>("/api/v1/company", new
        {
            legal_organization_code = company.LegalOrganizationCode,
            company = company.Company,
            trade_name = company.TradeName,
            email = company.Email,
            address = company.Address,
            registration_code = company.RegistrationCode,
            economic_activity = company.EconomicActivity,
            phone = company.Phone,
            municipality_code = company.MunicipalityCode,
            tribute_code = company.TributeCode,
            responsibilities = company.Responsibilities
        }, ct);

        return company with
        {
            Company = response.Data.Company
        };
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}

// Factus API response models
internal sealed record AuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = "";
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}

internal sealed record FactusInvoiceResponse
{
    public FactusInvoiceData Data { get; init; } = new();
}

internal sealed record FactusBillResponse
{
    public FactusBillData? Data { get; init; }
}

internal sealed record FactusBillsResponse
{
    public List<FactusBillData> Data { get; init; } = new();
}

internal sealed record FactusInvoiceData
{
    public string Number { get; init; } = "";
    public string Uuid { get; init; } = "";
}

internal sealed record FactusBillData
{
    public string Number { get; init; } = "";
    public string Uuid { get; init; } = "";
}

internal sealed record FactusNumberingRangesResponse
{
    public List<FactusNumberingRangeData> Data { get; init; } = new();
}

internal sealed record FactusNumberingRangeResponse
{
    public FactusNumberingRangeData Data { get; init; } = new();
}

internal sealed record FactusNumberingRangeData
{
    public int Id { get; init; }
    public string Prefix { get; init; } = "";
    public int From { get; init; }
    public int To { get; init; }
    public int Current { get; init; }
    public string Status { get; init; } = "";
}

internal sealed record FactusCompanyResponse
{
    public FactusCompanyData Data { get; init; } = new();
}

internal sealed record FactusCompanyData
{
    public string LegalOrganizationCode { get; init; } = "";
    public string Company { get; init; } = "";
    public string? TradeName { get; init; }
    public string Email { get; init; } = "";
    public string Address { get; init; } = "";
    public string RegistrationCode { get; init; } = "";
    public string EconomicActivity { get; init; } = "";
    public string Phone { get; init; } = "";
    public string MunicipalityCode { get; init; } = "";
    public string TributeCode { get; init; } = "";
    public string Responsibilities { get; init; } = "";
}
