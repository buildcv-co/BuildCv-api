using BuildCv.Infrastructure.Auth;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Auth;

public sealed class JwtTokenAdapterTests
{
    private static readonly JwtTokenAdapter Adapter = new(
        signingKey: "test-secret-key-that-is-long-enough-for-hmac-sha256-32bytes!",
        issuer: "buildcv-test",
        audience: "buildcv-test");

    [Fact]
    public void GenerateAccessToken_returns_non_empty_string()
    {
        var token = Adapter.GenerateAccessToken(Guid.NewGuid(), "user@test.com");

        token.Should().NotBeEmpty();
        token.Should().Contain("."); // JWT has 3 dot-separated parts
    }

    [Fact]
    public void GenerateAccessToken_contains_correct_claims()
    {
        var userId = Guid.NewGuid();
        var token = Adapter.GenerateAccessToken(userId, "user@test.com");

        var principal = Adapter.ValidateToken(token);

        principal.Should().NotBeNull();
        principal!.Claims.Should().Contain(c => c.Value == userId.ToString());
        principal.Claims.Should().Contain(c => c.Value == "user@test.com");
        principal.FindFirst("iss")!.Value.Should().Be("buildcv-test");
        principal.FindFirst("aud")!.Value.Should().Be("buildcv-test");
    }

    [Fact]
    public void ValidateToken_throws_for_invalid_token()
    {
        var act = () => Adapter.ValidateToken("invalid.token.here");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ValidateToken_throws_for_tampered_token()
    {
        var token = Adapter.GenerateAccessToken(Guid.NewGuid(), "a@b.com");
        var tampered = token.Substring(0, token.Length - 5) + "XXXXX";

        var act = () => Adapter.ValidateToken(tampered);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ValidateToken_throws_for_wrong_issuer()
    {
        var otherAdapter = new JwtTokenAdapter(
            signingKey: "test-secret-key-that-is-long-enough-for-hmac-sha256-32bytes!",
            issuer: "wrong-issuer",
            audience: "buildcv-test");
        var token = Adapter.GenerateAccessToken(Guid.NewGuid(), "a@b.com");

        var act = () => otherAdapter.ValidateToken(token);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void GenerateRefreshToken_returns_non_empty_string()
    {
        var token = Adapter.GenerateRefreshToken();

        token.Should().NotBeEmpty();
        token.Length.Should().BeGreaterThan(20);
    }

    [Fact]
    public void GenerateRefreshToken_returns_unique_values()
    {
        var token1 = Adapter.GenerateRefreshToken();
        var token2 = Adapter.GenerateRefreshToken();

        token1.Should().NotBe(token2);
    }
}
