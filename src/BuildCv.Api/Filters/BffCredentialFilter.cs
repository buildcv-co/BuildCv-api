using System.Security.Cryptography;
using System.Text;

namespace BuildCv.Api.Filters;

public sealed class BffCredentialFilter(IConfiguration configuration) : IEndpointFilter
{
    public const string HeaderName = "X-BFF-Key";

    public const string ConfigKey = "Auth:BffApiKey";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configuredKey = configuration[ConfigKey];
        if (string.IsNullOrEmpty(configuredKey))
        {
            return Unauthorized("BFF_AUTH_NOT_CONFIGURED", "BFF credential is not configured on the server.");
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var suppliedValues) ||
            !ConstantTimeEqualsString(suppliedValues.ToString(), configuredKey))
        {
            return Unauthorized("BFF_AUTH_INVALID", "Invalid or missing BFF credential.");
        }

        return await next(context);
    }

    private static IResult Unauthorized(string title, string detail) => Results.Json(
        new { type = "https://buildcv.com/errors/bff-auth", title, status = 401, detail },
        statusCode: StatusCodes.Status401Unauthorized);

    private static bool ConstantTimeEqualsString(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
