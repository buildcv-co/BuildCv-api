using System.Collections.Concurrent;
using BuildCv.Application.Features.LlmFeedback;
using Microsoft.Extensions.Options;

namespace BuildCv.Api.Filters;

public sealed class LlmFeedbackRateLimitFilter(IOptions<LlmFeedbackOptions> options) : IEndpointFilter
{
    private static readonly ConcurrentDictionary<string, RateLimitBucket> Buckets = new(StringComparer.Ordinal);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var now = DateTimeOffset.UtcNow;
        var currentOptions = options.Value.RateLimit;
        var window = TimeSpan.FromSeconds(Math.Max(1, currentOptions.WindowSeconds));
        var limit = Math.Max(1, currentOptions.RequestsPerWindow);
        var key = ResolveKey(context.HttpContext);

        var bucket = Buckets.AddOrUpdate(
            key,
            _ => new RateLimitBucket(now, 1),
            (_, existing) => now - existing.WindowStartedAt >= window
                ? new RateLimitBucket(now, 1)
                : existing with { Count = existing.Count + 1 });

        if (bucket.Count > limit)
        {
            var retryAfter = Math.Max(1, (int)Math.Ceiling((bucket.WindowStartedAt + window - now).TotalSeconds));
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.Json(
                new { error = "rate_limited", detail = "LLM feedback rate limit exceeded." },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        return await next(context);
    }

    private static string ResolveKey(HttpContext context)
    {
        var user = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(user))
        {
            return "user:" + user;
        }

        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
        return "ip:" + (string.IsNullOrWhiteSpace(forwardedFor) ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown" : forwardedFor);
    }

    private sealed record RateLimitBucket(DateTimeOffset WindowStartedAt, int Count);
}
