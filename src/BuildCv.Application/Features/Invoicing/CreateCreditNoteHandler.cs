using BuildCv.Domain.Common;
using BuildCv.Domain.Invoicing;

namespace BuildCv.Application.Features.Invoicing;

public sealed class CreateCreditNoteHandler(IInvoiceStore store)
{
    public async Task<Result<Invoice>> HandleAsync(CreateCreditNoteCommand command, CancellationToken ct)
    {
        var referenceCode = $"NC-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        var note = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            DocumentType = InvoiceType.CreditNote,
            ReferenceCode = referenceCode,
            AmountInCents = command.AmountInCents,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = command.CustomerName,
            CustomerIdentification = command.CustomerIdentification,
            CustomerEmail = command.CustomerEmail,
            CustomerPhone = command.CustomerPhone,
            CustomerAddress = command.CustomerAddress,
            CustomerLegalOrganizationCode = command.CustomerLegalOrganizationCode,
            CustomerTributeCode = command.CustomerTributeCode,
            CustomerMunicipalityCode = command.CustomerMunicipalityCode,
            CustomerIdentificationDocumentCode = command.CustomerIdentificationDocumentCode,
            ItemsJson = command.ItemsJson,
            PaymentDetailsJson = command.PaymentDetailsJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await store.AddAsync(note, ct);

        return Result.Success(note);
    }
}

public sealed record CreateCreditNoteCommand
{
    public Guid UserId { get; init; }
    public long AmountInCents { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerIdentification { get; init; } = "";
    public string CustomerEmail { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public string CustomerAddress { get; init; } = "";
    public string CustomerLegalOrganizationCode { get; init; } = "2";
    public string CustomerTributeCode { get; init; } = "ZZ";
    public string CustomerMunicipalityCode { get; init; } = "";
    public string CustomerIdentificationDocumentCode { get; init; } = "13";
    public string ItemsJson { get; init; } = "[]";
    public string PaymentDetailsJson { get; init; } = "[]";
}
