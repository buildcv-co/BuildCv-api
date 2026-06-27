using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Common;
using BuildCv.Infrastructure.Auth;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Auth;

public sealed class InMemoryRefreshTokenStoreTests
{
    [Fact]
    public async Task CreateAsync_returns_non_empty_token()
    {
        var store = new InMemoryRefreshTokenStore();

        var token = await store.CreateAsync(Guid.NewGuid());

        token.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_returns_unique_tokens()
    {
        var store = new InMemoryRefreshTokenStore();

        var token1 = await store.CreateAsync(Guid.NewGuid());
        var token2 = await store.CreateAsync(Guid.NewGuid());

        token1.Should().NotBe(token2);
    }

    [Fact]
    public async Task ValidateAsync_returns_userId_for_valid_token()
    {
        var store = new InMemoryRefreshTokenStore();
        var userId = Guid.NewGuid();
        var token = await store.CreateAsync(userId);

        var result = await store.ValidateAsync(token);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(userId);
    }

    [Fact]
    public async Task ValidateAsync_returns_failure_for_invalid_token()
    {
        var store = new InMemoryRefreshTokenStore();

        var result = await store.ValidateAsync("nonexistent-token");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/REFRESH_REVOKED");
    }

    [Fact]
    public async Task ValidateAsync_returns_failure_for_expired_token()
    {
        var store = new InMemoryRefreshTokenStore(expirySeconds: -1);
        var token = await store.CreateAsync(Guid.NewGuid());

        var result = await store.ValidateAsync(token);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/REFRESH_REVOKED");
    }

    [Fact]
    public async Task RevokeAsync_makes_token_invalid()
    {
        var store = new InMemoryRefreshTokenStore();
        var token = await store.CreateAsync(Guid.NewGuid());

        await store.RevokeAsync(token);
        var result = await store.ValidateAsync(token);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeAllForUserAsync_RemovesAllTokensForUser()
    {
        var store = new InMemoryRefreshTokenStore();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var token1 = await store.CreateAsync(userId);
        var token2 = await store.CreateAsync(userId);
        var token3 = await store.CreateAsync(userId);
        var otherToken = await store.CreateAsync(otherUserId);

        await store.RevokeAllForUserAsync(userId);

        (await store.ValidateAsync(token1)).IsFailure.Should().BeTrue();
        (await store.ValidateAsync(token2)).IsFailure.Should().BeTrue();
        (await store.ValidateAsync(token3)).IsFailure.Should().BeTrue();
        (await store.ValidateAsync(otherToken)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeAllForUserAsync_IsNoOp_ForUnknownUserId()
    {
        var store = new InMemoryRefreshTokenStore();
        var userId = Guid.NewGuid();
        var token = await store.CreateAsync(userId);

        await store.RevokeAllForUserAsync(Guid.NewGuid());

        (await store.ValidateAsync(token)).IsSuccess.Should().BeTrue();
    }
}
