using System.ComponentModel.DataAnnotations;
using BuildCv.Api.Contracts;
using BuildCv.Api.Security;
using BuildCv.Application.Features.Export;

namespace BuildCv.Api.Endpoints;

public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/export", async Task<IResult> (
            ExportRequestDto request,
            ExportPdfHandler handler,
            CancellationToken ct) =>
        {
            // Validación ad-hoc (el DTO vive en Api.Contracts; el validator vive en Application).
            // Mantiene el desacople de capas (Application no puede depender de Api).
            var validationErrors = new Dictionary<string, string[]>();
            if (string.IsNullOrEmpty(request.AdaptedCv))
            {
                validationErrors["AdaptedCv"] = new[] { "The AdaptedCv field is required." };
            }
            else if (request.AdaptedCv.Length > 50_000)
            {
                validationErrors["AdaptedCv"] = new[] { "The length of AdaptedCv must be 50000 characters or fewer." };
            }
            if (request.CandidateName is { Length: > 100 })
            {
                validationErrors["CandidateName"] = new[] { "The length of CandidateName must be 100 characters or fewer." };
            }
            if (request.Validation is null)
            {
                validationErrors["Validation"] = new[] { "The Validation field is required." };
            }
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var command = ExportResponseMapper.ToCommand(request);
            var result = await handler.Handle(command, ct);

            if (result.IsFailure)
            {
                if (result.Error.Code == "EXPORT_BLOCKED_INVENTION")
                {
                    return Results.Problem(
                        detail: result.Error.Message,
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        title: "Export bloqueado por invención");
                }
                if (result.Error.Code == "PDF_UNAVAILABLE")
                {
                    return Results.Problem(
                        detail: result.Error.Message,
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Generación de PDF no disponible");
                }
                return Results.Problem(
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var export = result.Value;
            return Results.File(export.Pdf, "application/pdf", export.Filename);
        })
        .RequireRateLimiting(RateLimiting.ExportPolicy)
        .WithName("ExportPdf")
        .WithSummary("Genera PDF del CV adaptado. Rate-limited 20/h por IP.")
        .WithDescription("Bloquea export si el ValidationReport tiene invenciones Hard (Constitution Art. I).");

        return app;
    }
}
