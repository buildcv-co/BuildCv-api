namespace BuildCv.Domain.Invoicing;

public sealed record CompanyInfo
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
