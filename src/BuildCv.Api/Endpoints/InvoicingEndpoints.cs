using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Invoicing;

namespace BuildCv.Api.Endpoints;

public static class InvoicingEndpoints
{
    public static IEndpointRouteBuilder MapInvoicingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/invoices", async (
            CreateInvoiceHandler handler,
            CreateInvoiceRequest request,
            CancellationToken ct) =>
        {
            var command = new CreateInvoiceCommand
            {
                UserId = request.UserId,
                AmountInCents = request.AmountInCents,
                CustomerName = request.CustomerName,
                CustomerIdentification = request.CustomerIdentification,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                CustomerAddress = request.CustomerAddress
            };
            var result = await handler.HandleAsync(command, ct);
            if (result.IsFailure)
            {
                return Results.BadRequest(new { error = result.Error.Code, result.Error.Message });
            }

            return Results.Created($"/api/v1/invoices/{result.Value.Id}", result.Value);
        })
        .WithName("CreateInvoice")
        .WithSummary("Create a new draft invoice");

        app.MapGet("/api/v1/invoices/{id:guid}", async (
            GetInvoiceHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetInvoiceQuery { InvoiceId = id }, ct);
            if (result.IsFailure)
            {
                return Results.NotFound(new { error = result.Error.Code, result.Error.Message });
            }

            return Results.Ok(result.Value);
        })
        .WithName("GetInvoice")
        .WithSummary("Get an invoice by ID");

        app.MapGet("/api/v1/invoices", async (
            ListInvoicesHandler handler,
            Guid userId,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ListInvoicesQuery { UserId = userId }, ct);
            return Results.Ok(result.Value);
        })
        .WithName("ListInvoices")
        .WithSummary("List invoices for a user");

        app.MapDelete("/api/v1/invoices/{referenceCode}", async (
            IInvoiceProvider provider,
            string referenceCode,
            CancellationToken ct) =>
        {
            await provider.DeleteInvoiceAsync(referenceCode, ct);
            return Results.NoContent();
        })
        .WithName("DeleteInvoice")
        .WithSummary("Delete an invoice by reference code");

        app.MapGet("/api/v1/invoices/{number}/pdf", async (
            IInvoiceProvider provider,
            string number,
            CancellationToken ct) =>
        {
            var pdf = await provider.DownloadPdfAsync(number, ct);
            return Results.File(pdf, "application/pdf", $"{number}.pdf");
        })
        .WithName("DownloadInvoicePdf")
        .WithSummary("Download invoice as PDF");

        app.MapGet("/api/v1/invoices/{number}/xml", async (
            IInvoiceProvider provider,
            string number,
            CancellationToken ct) =>
        {
            var xml = await provider.DownloadXmlAsync(number, ct);
            return Results.File(xml, "application/xml", $"{number}.xml");
        })
        .WithName("DownloadInvoiceXml")
        .WithSummary("Download invoice as XML");

        app.MapPost("/api/v1/credit-notes", async (
            CreateCreditNoteHandler handler,
            CreateCreditNoteRequest request,
            CancellationToken ct) =>
        {
            var command = new CreateCreditNoteCommand
            {
                UserId = request.UserId,
                AmountInCents = request.AmountInCents,
                CustomerName = request.CustomerName,
                CustomerIdentification = request.CustomerIdentification,
                CustomerEmail = request.CustomerEmail
            };
            var result = await handler.HandleAsync(command, ct);
            if (result.IsFailure)
            {
                return Results.BadRequest(new { error = result.Error.Code, result.Error.Message });
            }

            return Results.Created($"/api/v1/invoices/{result.Value.Id}", result.Value);
        })
        .WithName("CreateCreditNote")
        .WithSummary("Create a new credit note");

        app.MapPost("/api/v1/support-documents", async (
            CreateSupportDocumentHandler handler,
            CreateSupportDocumentRequest request,
            CancellationToken ct) =>
        {
            var command = new CreateSupportDocumentCommand
            {
                UserId = request.UserId,
                AmountInCents = request.AmountInCents,
                CustomerName = request.CustomerName,
                CustomerIdentification = request.CustomerIdentification,
                CustomerEmail = request.CustomerEmail
            };
            var result = await handler.HandleAsync(command, ct);
            if (result.IsFailure)
            {
                return Results.BadRequest(new { error = result.Error.Code, result.Error.Message });
            }

            return Results.Created($"/api/v1/invoices/{result.Value.Id}", result.Value);
        })
        .WithName("CreateSupportDocument")
        .WithSummary("Create a new support document");

        app.MapGet("/api/v1/numbering-ranges", async (
            GetNumberingRangesHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetNumberingRangesQuery(), ct);
            return Results.Ok(result.Value);
        })
        .WithName("GetNumberingRanges")
        .WithSummary("Get all numbering ranges");

        app.MapPost("/api/v1/numbering-ranges", async (
            IInvoiceProvider provider,
            CreateNumberingRangeRequest request,
            CancellationToken ct) =>
        {
            var range = new NumberingRange
            {
                Prefix = request.Prefix,
                From = request.From,
                To = request.To,
                Status = request.Status
            };
            var created = await provider.CreateNumberingRangeAsync(range, ct);
            return Results.Created($"/api/v1/numbering-ranges/{created.Id}", created);
        })
        .WithName("CreateNumberingRange")
        .WithSummary("Create a new numbering range");

        app.MapGet("/api/v1/company", async (
            GetCompanyHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetCompanyQuery(), ct);
            return Results.Ok(result.Value);
        })
        .WithName("GetCompany")
        .WithSummary("Get company information");

        app.MapPut("/api/v1/company", async (
            IInvoiceProvider provider,
            UpdateCompanyRequest request,
            CancellationToken ct) =>
        {
            var company = new CompanyInfo
            {
                LegalOrganizationCode = request.LegalOrganizationCode,
                Company = request.Company,
                TradeName = request.TradeName,
                Email = request.Email,
                Address = request.Address,
                RegistrationCode = request.RegistrationCode,
                EconomicActivity = request.EconomicActivity,
                Phone = request.Phone,
                MunicipalityCode = request.MunicipalityCode,
                TributeCode = request.TributeCode,
                Responsibilities = request.Responsibilities
            };
            var updated = await provider.UpdateCompanyAsync(company, ct);
            return Results.Ok(updated);
        })
        .WithName("UpdateCompany")
        .WithSummary("Update company information");

        return app;
    }
}

public sealed record CreateInvoiceRequest
{
    public Guid UserId { get; init; }
    public long AmountInCents { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerIdentification { get; init; } = "";
    public string CustomerEmail { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public string CustomerAddress { get; init; } = "";
}

public sealed record CreateCreditNoteRequest
{
    public Guid UserId { get; init; }
    public long AmountInCents { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerIdentification { get; init; } = "";
    public string CustomerEmail { get; init; } = "";
}

public sealed record CreateSupportDocumentRequest
{
    public Guid UserId { get; init; }
    public long AmountInCents { get; init; }
    public string CustomerName { get; init; } = "";
    public string CustomerIdentification { get; init; } = "";
    public string CustomerEmail { get; init; } = "";
}

public sealed record CreateNumberingRangeRequest
{
    public string Prefix { get; init; } = "";
    public int From { get; init; }
    public int To { get; init; }
    public string Status { get; init; } = "Active";
}

public sealed record UpdateCompanyRequest
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
