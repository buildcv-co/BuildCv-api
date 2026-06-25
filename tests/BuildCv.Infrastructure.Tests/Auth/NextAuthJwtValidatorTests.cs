using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BuildCv.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace BuildCv.Infrastructure.Tests.Auth;

public sealed class NextAuthJwtValidatorTests
{
    private const string SigningKey = "test-secret-key-that-is-long-enough-for-hmac-sha256-32bytes!";
    private const string Issuer = "buildcv-web-test";
    private const string Audience = "buildcv-api-test";

    private static readonly NextAuthJwtValidator Validator = new(SigningKey, Issuer, Audience);

    [Fact]
    public void Validate_ReturnsUserId_WhenJwtIsValid()
    {
        var userId = Guid.NewGuid();
        var jwt = CreateNextAuthJwt(userId, "user@test.com", Issuer, Audience, SigningKey, DateTime.UtcNow.AddMinutes(15));

        var result = Validator.TryExtractUserId(jwt);

        result.Should().Be(userId);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenJwtIsEmpty()
    {
        var result = Validator.TryExtractUserId(string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_WhenJwtIsExpired()
    {
        var userId = Guid.NewGuid();
        var jwt = CreateNextAuthJwt(userId, "user@test.com", Issuer, Audience, SigningKey, DateTime.UtcNow.AddMinutes(-15));

        var result = Validator.TryExtractUserId(jwt);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_WhenSignatureIsInvalid()
    {
        var userId = Guid.NewGuid();
        var jwt = CreateNextAuthJwt(userId, "user@test.com", Issuer, Audience, "different-secret-key-still-long-enough-for-hs256!", DateTime.UtcNow.AddMinutes(15));

        var result = Validator.TryExtractUserId(jwt);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_WhenIssuerIsWrong()
    {
        var userId = Guid.NewGuid();
        var jwt = CreateNextAuthJwt(userId, "user@test.com", "wrong-issuer", Audience, SigningKey, DateTime.UtcNow.AddMinutes(15));

        var result = Validator.TryExtractUserId(jwt);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_WhenAudienceIsWrong()
    {
        var userId = Guid.NewGuid();
        var jwt = CreateNextAuthJwt(userId, "user@test.com", Issuer, "wrong-audience", SigningKey, DateTime.UtcNow.AddMinutes(15));

        var result = Validator.TryExtractUserId(jwt);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_WhenSubClaimIsMissing()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Email, "user@test.com")],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        var result = Validator.TryExtractUserId(jwt);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_WhenSubClaimIsNotGuid()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid-value")],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        var result = Validator.TryExtractUserId(jwt);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_WhenJwtIsMalformed()
    {
        var result = Validator.TryExtractUserId("this-is-not.a.jwt");

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_AcceptsJwtWithinClockSkew()
    {
        var userId = Guid.NewGuid();
        var jwt = CreateNextAuthJwt(userId, "user@test.com", Issuer, Audience, SigningKey, DateTime.UtcNow.AddSeconds(-30));

        var result = Validator.TryExtractUserId(jwt);

        result.Should().Be(userId);
    }

    [Fact]
    public void Validate_ReturnsUserId_WhenValidatorIsReconstructedWithSameConfig()
    {
        var userId = Guid.NewGuid();
        var jwt = CreateNextAuthJwt(userId, "user@test.com", Issuer, Audience, SigningKey, DateTime.UtcNow.AddMinutes(15));

        var secondValidator = new NextAuthJwtValidator(SigningKey, Issuer, Audience);
        var result = secondValidator.TryExtractUserId(jwt);

        result.Should().Be(userId);
    }

    internal static string CreateNextAuthJwt(
        Guid userId,
        string email,
        string issuer,
        string audience,
        string signingKey,
        DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var notBefore = expiresAt.AddMinutes(-30);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore,
            expires: expiresAt,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
