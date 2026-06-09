using BuildCv.Application.Features.Consent;

namespace BuildCv.Api.Endpoints;

public static class PrivacyEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/privacy-policy", async (
            PrivacyPolicyQueryHandler handler,
            int? version,
            CancellationToken ct) =>
        {
            var query = new PrivacyPolicyQuery(version);
            var result = await handler.HandleAsync(query, ct);

            return Results.Ok(new
            {
                result.Version,
                result.Content,
                result.EffectiveDate,
                result.DataCategories,
                result.Purposes,
            });
        })
        .WithName("GetPrivacyPolicy")
        .WithSummary("Returns the current or specific version of the privacy policy.");

        return app;
    }
}
