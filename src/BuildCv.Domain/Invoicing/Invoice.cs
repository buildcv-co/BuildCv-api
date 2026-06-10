namespace BuildCv.Domain.Invoicing;

public sealed record Invoice
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public InvoiceType DocumentType { get; init; }
    public string ReferenceCode { get; init; } = "";
    public string? Number { get; init; }
    public string? Cufe { get; init; }
    public string? Uuid { get; init; }
    public long AmountInCents { get; init; }
    public string Currency { get; init; } = "COP";
    public InvoiceStatus Status { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerIdentification { get; init; } = "";
    public string CustomerEmail { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public string CustomerAddress { get; init; } = "";
    public string CustomerCompany { get; init; } = "";
    public string? CustomerTradeName { get; init; }
    public string CustomerLegalOrganizationCode { get; init; } = "2";
    public string CustomerTributeCode { get; init; } = "ZZ";
    public string CustomerMunicipalityCode { get; init; } = "";
    public string CustomerIdentificationDocumentCode { get; init; } = "13";
    public string ItemsJson { get; init; } = "[]";
    public string ItemsDescription { get; init; } = "";
    public string PaymentDetailsJson { get; init; } = "[]";
    public string PaymentMethodCode { get; init; } = "10";
    public string? ProviderRaw { get; init; }
    public string? ProviderId { get; init; }
    public string? FactusResponseJson { get; init; }
    public string? ErrorJson { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? SentAt { get; init; }
}
