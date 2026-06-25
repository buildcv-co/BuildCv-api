using System.Security.Claims;
using BuildCv.Application.Features.Credits;

namespace BuildCv.Api.Filters;

public sealed class RequireCreditsFilter(int requiredCredits) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var user = ctx.HttpContext.User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId))
        {
            return Results.Unauthorized();
        }

        var service = ctx.HttpContext.RequestServices.GetRequiredService<ICreditConsumptionService>();
        var view = await service.GetBalanceAsync(parsedUserId, ctx.HttpContext.RequestAborted);

        if (view.Balance < requiredCredits)
        {
            ctx.HttpContext.Response.Headers["X-Credit-Balance"] = view.Balance.ToString();
            ctx.HttpContext.Response.Headers["Retry-After"] = "0";

            return Results.Json(
                new ProblemDetailsBody
                {
                    Type = "https://buildcv.com/errors/credit-insufficient",
                    Title = "INSUFFICIENT_CREDITS",
                    Status = StatusCodes.Status402PaymentRequired,
                    Detail = $"This action requires {requiredCredits} credit(s); you have {view.Balance}.",
                    Code = "CREDIT/INSUFFICIENT",
                    Balance = view.Balance,
                    Required = requiredCredits,
                },
                statusCode: StatusCodes.Status402PaymentRequired);
        }

        return await next(ctx);
    }
}

public static class EndpointConventionBuilderExtensions
{
    public static T RequireCredits<T>(this T builder, int credits) where T : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new RequireCreditsFilter(credits));
        return builder;
    }
}

internal sealed record ProblemDetailsBody
{
    public string Type { get; init; } = "";
    public string Title { get; init; } = "";
    public int Status { get; init; }
    public string Detail { get; init; } = "";
    public string Code { get; init; } = "";
    public int Balance { get; init; }
    public int Required { get; init; }
}
