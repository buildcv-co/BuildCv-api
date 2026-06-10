using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Auth;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class EfRefreshTokenStoreTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfRefreshTokenStore _store;

    public EfRefreshTokenStoreTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new BuildCvDbContext(options);
        _store = new EfRefreshTokenStore(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task CreateAsync_returns_non_empty_token()
    {
        var token = await _store.CreateAsync(Guid.NewGuid());

        token.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_returns_unique_tokens()
    {
        var token1 = await _store.CreateAsync(Guid.NewGuid());
        var token2 = await _store.CreateAsync(Guid.NewGuid());

        token1.Should().NotBe(token2);
    }

    [Fact]
    public async Task CreateAsync_persists_token_in_database()
    {
        var userId = Guid.NewGuid();

        var token = await _store.CreateAsync(userId);

        var stored = await _dbContext.RefreshTokens.FindAsync(token);
        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(userId);
        stored.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task ValidateAsync_returns_userId_for_valid_token()
    {
        var userId = Guid.NewGuid();
        var token = await _store.CreateAsync(userId);

        var result = await _store.ValidateAsync(token);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(userId);
    }

    [Fact]
    public async Task ValidateAsync_returns_failure_for_invalid_token()
    {
        var result = await _store.ValidateAsync("nonexistent-token");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/REFRESH_REVOKED");
    }

    [Fact]
    public async Task ValidateAsync_returns_failure_for_expired_token()
    {
        var userId = Guid.NewGuid();
        var token = await _store.CreateAsync(userId);

        var refreshToken = await _dbContext.RefreshTokens.FindAsync(token);
        _dbContext.Entry(refreshToken!).Property(t => t.ExpiresAt).CurrentValue = DateTime.UtcNow.AddHours(-1);
        await _dbContext.SaveChangesAsync();

        var result = await _store.ValidateAsync(token);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/REFRESH_REVOKED");
    }

    [Fact]
    public async Task ValidateAsync_returns_failure_for_revoked_token()
    {
        var token = await _store.CreateAsync(Guid.NewGuid());

        await _store.RevokeAsync(token);
        var result = await _store.ValidateAsync(token);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/REFRESH_REVOKED");
    }

    [Fact]
    public async Task RevokeAsync_sets_revoked_at()
    {
        var token = await _store.CreateAsync(Guid.NewGuid());

        await _store.RevokeAsync(token);

        var stored = await _dbContext.RefreshTokens.FindAsync(token);
        stored!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeAsync_does_not_affect_other_tokens()
    {
        var token1 = await _store.CreateAsync(Guid.NewGuid());
        var token2 = await _store.CreateAsync(Guid.NewGuid());

        await _store.RevokeAsync(token1);

        var result = await _store.ValidateAsync(token2);
        result.IsSuccess.Should().BeTrue();
    }
}
