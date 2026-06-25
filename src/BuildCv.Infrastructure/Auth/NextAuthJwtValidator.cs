using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BuildCv.Infrastructure.Auth;

public sealed class NextAuthJwtValidator
{
    private const string SubjectClaimType = "sub";
    private const string NameIdentifierClaimType = ClaimTypes.NameIdentifier;

    private readonly TokenValidationParameters _parameters;

    public NextAuthJwtValidator(string signingKey, string issuer, string audience)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new ArgumentException("Signing key is required.", nameof(signingKey));
        }

        if (signingKey.Length < 32)
        {
            throw new ArgumentException("Signing key must be at least 32 characters for HS256.", nameof(signingKey));
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new ArgumentException("Issuer is required.", nameof(issuer));
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new ArgumentException("Audience is required.", nameof(audience));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        _parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    }

    public Guid? TryExtractUserId(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(jwt, _parameters, out _);
            var subClaim = principal.FindFirst(SubjectClaimType)?.Value
                ?? principal.FindFirst(NameIdentifierClaimType)?.Value;
            return Guid.TryParse(subClaim, out var userId) ? userId : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
