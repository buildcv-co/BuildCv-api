using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BuildCv.Api.Errors;

/// <summary>
/// Convierte cualquier excepción no controlada en una respuesta ProblemDetails (RFC 9457),
/// sin filtrar detalles internos ni contenido del usuario.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Solo metadatos: nunca registramos el contenido del CV/vacante (NFR-002).
        logger.LogError(exception, "Excepción no controlada en {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "Ocurrió un error inesperado.",
                Status = StatusCodes.Status500InternalServerError,
            },
        });
    }
}
