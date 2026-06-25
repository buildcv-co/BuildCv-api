using System.Security.Claims;
using System.Text;
using BuildCv.Api.Auth;
using BuildCv.Api.Security;
using BuildCv.Application.Common;
using BuildCv.Application.Features.FeatureFlags;
using BuildCv.Domain.FeatureFlags;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Api.Endpoints;

public static class FeatureFlagAdminEndpoints
{
    public static IEndpointRouteBuilder MapFeatureFlagAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/feature-flags")
            .RequireAuthorization(AuthPolicies.Admin)
            .RequireRateLimiting(RateLimiting.AdminPolicy)
            .WithTags("FeatureFlagAdmin");

        group.MapGet("/", async (IFeatureFlag flags, CancellationToken ct) =>
            {
                var list = await flags.ListAsync(ct);
                return Results.Ok(new ListFeatureFlagsResponse(
                    list.Select(FeatureFlagDto.FromDomain).ToList()));
            })
            .WithName("ListFeatureFlags")
            .WithSummary("Lista todos los feature flags registrados (admin only).");

        group.MapGet("/{name}", async (string name, IFeatureFlag flags, CancellationToken ct) =>
            {
                var flag = await flags.GetAsync(name, ct);
                return flag is null
                    ? Results.NotFound(new { error = "FEATURE_FLAG/NOT_FOUND", message = $"Flag '{name}' no encontrado." })
                    : Results.Ok(FeatureFlagDto.FromDomain(flag));
            })
            .WithName("GetFeatureFlag")
            .WithSummary("Obtiene un feature flag por nombre (admin only).");

        group.MapPut("/{name}", async (
            string name,
            UpdateFeatureFlagRequest body,
            ClaimsPrincipal user,
            UpdateFeatureFlagHandler handler,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                var updated = await handler.HandleAsync(name, body.Value, userId.Value, body.Reason, ct);
                return Results.Ok(FeatureFlagDto.FromDomain(updated));
            }
            catch (FeatureFlagNotFoundException)
            {
                return Results.NotFound(new { error = "FEATURE_FLAG/NOT_FOUND", message = $"Flag '{name}' no encontrado." });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { error = "FEATURE_FLAG/CONFLICT", message = "Conflicto de concurrencia. Recarga y reintenta." });
            }
        })
        .WithName("UpdateFeatureFlag")
        .WithSummary("Actualiza un feature flag + escribe audit log + invalida cache (admin only).");

        group.MapGet("/{name}/audit-log", async (
            string name,
            GetFeatureFlagAuditLogHandler handler,
            int? limit,
            string? cursor,
            CancellationToken ct) =>
        {
            var page = await handler.HandleAsync(name, limit, cursor, ct);
            return Results.Ok(new AuditLogResponse(
                page.Entries.Select(AuditLogDto.FromDomain).ToList(),
                page.NextCursor));
        })
        .WithName("GetFeatureFlagAuditLog")
        .WithSummary("Audit log paginado del flag (admin only, keyset pagination).");

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return userId is not null && Guid.TryParse(userId, out var id) ? id : null;
    }
}

public sealed record ListFeatureFlagsResponse(List<FeatureFlagDto> Flags);

public sealed record FeatureFlagDto(
    string Name,
    bool DefaultValue,
    bool CurrentValue,
    DateTime UpdatedAt,
    Guid? UpdatedBy)
{
    public static FeatureFlagDto FromDomain(FeatureFlag f) =>
        new(f.Name, f.DefaultValue, f.CurrentValue, f.UpdatedAt, f.UpdatedBy);
}

public sealed record UpdateFeatureFlagRequest
{
    public bool Value { get; init; }
    public string? Reason { get; init; }
}

public sealed record AuditLogResponse(List<AuditLogDto> Entries, string? NextCursor);

public sealed record AuditLogDto(
    Guid Id,
    string FlagName,
    bool? OldValue,
    bool NewValue,
    Guid ChangedBy,
    DateTime ChangedAt,
    string? Reason)
{
    public static AuditLogDto FromDomain(FeatureFlagAuditLog l) =>
        new(l.Id, l.FlagName, l.OldValue, l.NewValue, l.ChangedBy, l.ChangedAt, l.Reason);
}