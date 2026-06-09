using System.Diagnostics;
using BuildCv.Api.Contracts;
using BuildCv.Api.Security;
using BuildCv.Application.Features.Import;
using Microsoft.AspNetCore.Http.Features;

namespace BuildCv.Api.Endpoints;

public static class ImportEndpoints
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const long MaxRequestBodyBytes = 6 * 1024 * 1024;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/import", async Task<IResult> (
            HttpRequest httpRequest,
            ImportCvHandler handler,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("ImportEndpoints");
            var traceId = httpRequest.HttpContext.TraceIdentifier;

            if (!httpRequest.HasFormContentType)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Solicitud inválida",
                    detail: "Se requiere multipart/form-data con un campo 'file'.",
                    extensions: BuildCodeExtension(ImportErrorCodes.Validation, traceId));
            }

            var form = await httpRequest.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Archivo requerido",
                    detail: "El campo 'file' es obligatorio y no puede estar vacío.",
                    extensions: BuildCodeExtension(ImportErrorCodes.Validation, traceId));
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                    title: "Archivo demasiado grande",
                    detail: $"El archivo supera el límite de {MaxFileSizeBytes / (1024 * 1024)} MB.",
                    extensions: BuildCodeExtension(ImportErrorCodes.TooLarge, traceId,
                        new KeyValuePair<string, object?>("sizeBytes", file.Length),
                        new KeyValuePair<string, object?>("maxBytes", MaxFileSizeBytes)));
            }

            var declaredMime = file.ContentType ?? string.Empty;
            if (!AllowedMimeTypes.Contains(declaredMime))
            {
                return Problem(statusCode: StatusCodes.Status415UnsupportedMediaType,
                    title: "Tipo de archivo no soportado",
                    detail: "Tipo de archivo no soportado. Sube un PDF o DOCX.",
                    extensions: BuildCodeExtension(ImportErrorCodes.UnsupportedMedia, traceId,
                        new KeyValuePair<string, object?>("mimeDeclared", declaredMime)));
            }

            await using var memoryStream = new MemoryStream(capacity: (int)file.Length);
            await using (var source = file.OpenReadStream())
            {
                await source.CopyToAsync(memoryStream, ct);
            }
            var bytes = memoryStream.ToArray();

            var command = new ImportCvCommand(
                FileBytes: bytes,
                MimeType: declaredMime,
                OriginalFileName: string.IsNullOrWhiteSpace(file.FileName) ? "uploaded" : file.FileName,
                TraceId: traceId);

            var stopwatch = Stopwatch.StartNew();
            var result = await handler.HandleAsync(command, ct);
            stopwatch.Stop();

            if (result.IsFailure)
            {
                LogFailure(logger, file.Length, declaredMime, result.Error.Code, stopwatch.ElapsedMilliseconds, traceId);
                return MapError(result.Error.Code, result.Error.Message, traceId);
            }

            logger.LogInformation(
                "Import request (fileSize={FileSize}, mimeDeclared={MimeDeclared}, parseTimeMs={ParseMs}, sections={SectionCount}, warnings={WarningCount}, engineVersion={EngineVersion}, traceId={TraceId})",
                file.Length,
                declaredMime,
                stopwatch.ElapsedMilliseconds,
                result.Value.Sections.Count,
                result.Value.Warnings.Count,
                result.Value.EngineVersion,
                traceId);

            return Results.Ok(ImportResponseMapper.Map(result.Value));
        })
        .RequireRateLimiting(RateLimiting.ImportPolicy)
        .WithName("ImportCv")
        .WithSummary("Importa un CV desde PDF o DOCX. Rate-limited 30/h por IP.")
        .WithDescription("Constitution Art. III (sin persistencia), Art. V (texto como DATO), Art. VI (puerto ICvParser), Art. VII (rate-limit 'import' 30/h).");

        return app;
    }

    private static IResult MapError(string code, string message, string traceId)
    {
        return code switch
        {
            ImportErrorCodes.PdfEncrypted => Problem(StatusCodes.Status422UnprocessableEntity,
                "PDF protegido",
                "Este PDF está protegido con contraseña. Quítale la contraseña y vuelve a subirlo.",
                BuildCodeExtension(code, traceId)),

            ImportErrorCodes.ScannedPdf => Problem(StatusCodes.Status422UnprocessableEntity,
                "PDF escaneado",
                "Este PDF parece un escaneo. No podemos extraer texto. Pega el contenido manualmente o usa un PDF con texto seleccionable.",
                BuildCodeExtension(code, traceId)),

            ImportErrorCodes.DocxProtected => Problem(StatusCodes.Status422UnprocessableEntity,
                "DOCX protegido",
                "Este archivo de Word está protegido. Quítale la contraseña y vuelve a subirlo.",
                BuildCodeExtension(code, traceId)),

            ImportErrorCodes.DocxNoText => Problem(StatusCodes.Status422UnprocessableEntity,
                "DOCX sin texto",
                "Este archivo de Word no contiene texto extraíble.",
                BuildCodeExtension(code, traceId)),

            ImportErrorCodes.TooManyPages => Problem(StatusCodes.Status422UnprocessableEntity,
                "Demasiadas páginas",
                "El documento tiene más de 100 páginas. Sube un CV más conciso.",
                BuildCodeExtension(code, traceId)),

            ImportErrorCodes.EmptyFile => Problem(StatusCodes.Status422UnprocessableEntity,
                "Archivo vacío",
                "El archivo está vacío.",
                BuildCodeExtension(code, traceId)),

            ImportErrorCodes.UnsupportedMedia => Problem(StatusCodes.Status415UnsupportedMediaType,
                "Tipo de archivo no soportado",
                "Tipo de archivo no soportado. Sube un PDF o DOCX.",
                BuildCodeExtension(code, traceId)),

            ImportErrorCodes.EngineError => Problem(StatusCodes.Status503ServiceUnavailable,
                "Motor de import no disponible",
                "El servicio de import no está disponible temporalmente. Intenta de nuevo en unos minutos.",
                BuildCodeExtension(code, traceId)),

            _ => Problem(StatusCodes.Status400BadRequest,
                "Error de import",
                message,
                BuildCodeExtension(code, traceId)),
        };
    }

    private static IResult Problem(int statusCode, string title, string detail, IDictionary<string, object?> extensions)
    {
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = statusCode switch
            {
                400 => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                413 => "https://tools.ietf.org/html/rfc9110#section-15.5.14",
                415 => "https://tools.ietf.org/html/rfc9110#section-15.5.16",
                422 => "https://tools.ietf.org/html/rfc4918#section-11.2",
                503 => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            },
        };

        foreach (var kv in extensions)
        {
            problem.Extensions[kv.Key] = kv.Value;
        }

        return Results.Json(problem, statusCode: statusCode, contentType: "application/problem+json");
    }

    private static IDictionary<string, object?> BuildCodeExtension(string code, string traceId, params KeyValuePair<string, object?>[] extra)
    {
        var dict = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["traceId"] = traceId,
        };
        foreach (var kv in extra)
        {
            dict[kv.Key] = kv.Value;
        }
        return dict;
    }

    private static void LogFailure(ILogger logger, long fileSize, string mime, string code, long parseMs, string traceId)
    {
        logger.LogInformation(
            "Import failed (fileSize={FileSize}, mimeDeclared={MimeDeclared}, parseTimeMs={ParseMs}, code={Code}, traceId={TraceId})",
            fileSize,
            mime,
            parseMs,
            code,
            traceId);
    }
}
