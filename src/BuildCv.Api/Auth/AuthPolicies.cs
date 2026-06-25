using Microsoft.AspNetCore.Authorization;

namespace BuildCv.Api.Auth;

public static class AuthPolicies
{
    public const string Admin = "admin";
}

public static class AuthExtensions
{
    public static IServiceCollection AddAuthPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.Admin, policy =>
                policy.RequireAuthenticatedUser().RequireRole("admin"));
        });

        return services;
    }
}