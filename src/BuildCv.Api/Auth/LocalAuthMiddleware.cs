using System.Security.Claims;
using BuildCv.Application.Common;
using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Auth;
using Microsoft.Extensions.Options;

namespace BuildCv.Api.Auth;

public sealed class LocalAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly LocalAuthOptions _options;
    private readonly ILogger<LocalAuthMiddleware> _logger;

    public LocalAuthMiddleware(
        RequestDelegate next,
        IOptions<LocalAuthOptions> options,
        ILogger<LocalAuthMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserDataStore userStore)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        await EnsureLocalUserAsync(userStore, context.RequestAborted);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _options.UserId.ToString()),
                    new Claim(ClaimTypes.Email, _options.Email),
                    new Claim(ClaimTypes.Name, _options.Name),
                    new Claim("sub", _options.UserId.ToString()),
                },
                "LocalAuth");
            context.User = new ClaimsPrincipal(identity);
        }

        await _next(context);
    }

    private async Task EnsureLocalUserAsync(IUserDataStore userStore, CancellationToken ct)
    {
        var existingResult = await userStore.GetByIdAsync(_options.UserId, ct);
        if (existingResult.IsSuccess)
        {
            var existing = existingResult.Value;
            if (existing.CreditBalance < _options.InitialCredits)
            {
                _logger.LogInformation(
                    "Refilling local user credits from {Current} to {Initial}",
                    existing.CreditBalance,
                    _options.InitialCredits);
                await userStore.UpsertAsync(
                    existing with { CreditBalance = _options.InitialCredits, LastLoginAt = DateTime.UtcNow },
                    ct);
            }
            else
            {
                await userStore.UpsertAsync(existing with { LastLoginAt = DateTime.UtcNow }, ct);
            }
            return;
        }

        _logger.LogInformation("Creating local user {UserId}", _options.UserId);
        await userStore.UpsertAsync(
            new User
            {
                Id = _options.UserId,
                Provider = "local",
                ProviderId = "local-dev",
                Email = _options.Email,
                Name = _options.Name,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                CreditBalance = _options.InitialCredits,
            },
            ct);
    }
}
