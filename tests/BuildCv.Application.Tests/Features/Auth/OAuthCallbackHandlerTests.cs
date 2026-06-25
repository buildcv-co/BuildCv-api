using BuildCv.Application.Common;
using BuildCv.Application.Features.Auth;
using BuildCv.Application.Features.Credits;
using BuildCv.Application.Tests.Credits;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;
using BuildCv.Domain.Credits;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Application.Tests.Features.Auth;

public sealed class OAuthCallbackHandlerTests
{
    private sealed class StubAuthService : IAuthenticationService
    {
        private readonly Result<OAuthUserInfo> _result;
        public StubAuthService(Result<OAuthUserInfo> result) => _result = result;
        public Task<Result<OAuthUserInfo>> ExchangeCodeAsync(string provider, string code, string redirectUri, CancellationToken ct = default)
            => Task.FromResult(_result);
    }

    private sealed class StubUserDataService : IUserDataService
    {
        private readonly User _user;
        public StubUserDataService(User user) => _user = user;

        public Task<Result<User>> GetOrCreateAsync(string provider, string providerId, string email, string name, CancellationToken ct = default)
            => Task.FromResult(Result.Success(_user));
        public Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(Result.Success(_user));
        public Task<Result<User>> UpdateAsync(Guid userId, string? email, string? name, CancellationToken ct = default) => Task.FromResult(Result.Success(_user));
        public Task<Result> DeleteAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(Result.Success());
        public Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DataTreatmentLog>>(Array.Empty<DataTreatmentLog>());
    }

    private sealed class StubRefreshTokenStore : IRefreshTokenStore
    {
        public Task<string> CreateAsync(Guid userId, CancellationToken ct = default) => Task.FromResult("refresh-token-abc");
        public Task<Result<Guid>> ValidateAsync(string token, CancellationToken ct = default) => Task.FromResult(Result.Success(Guid.NewGuid()));
        public Task RevokeAsync(string token, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task GoogleOAuthCallbackHandler_exchanges_code_and_issues_tokens()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Provider = "google",
            ProviderId = "g-1",
            Email = "a@b.com",
            Name = "Alice",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var auth = new StubAuthService(Result.Success(new OAuthUserInfo("google", "g-1", "a@b.com", "Alice")));
        var userData = new StubUserDataService(user);
        var tokens = new StubRefreshTokenStore();
        var handler = new GoogleOAuthCallbackHandler(auth, userData, tokens);

        var result = await handler.HandleAsync(new GoogleOAuthCallbackCommand("auth-code", "http://redirect"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeEmpty();
        result.Value.RefreshToken.Should().Be("refresh-token-abc");
        result.Value.User.Email.Should().Be("a@b.com");
    }

    [Fact]
    public async Task GoogleOAuthCallbackHandler_fails_when_oauth_fails()
    {
        var auth = new StubAuthService(Result.Failure<OAuthUserInfo>(new Error("AUTH/OAUTH_FAILED", "Bad code")));
        var userData = new StubUserDataService(new User());
        var tokens = new StubRefreshTokenStore();
        var handler = new GoogleOAuthCallbackHandler(auth, userData, tokens);

        var result = await handler.HandleAsync(new GoogleOAuthCallbackCommand("bad-code", "http://redirect"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/OAUTH_FAILED");
    }

    [Fact]
    public async Task GoogleOAuthCallbackHandler_grants_welcome_credits_when_flag_enabled()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Provider = "google",
            ProviderId = "g-welcome",
            Email = "new@b.com",
            Name = "New",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var auth = new StubAuthService(Result.Success(new OAuthUserInfo("google", "g-welcome", "new@b.com", "New")));
        var userData = new StubUserDataService(user);
        var tokens = new StubRefreshTokenStore();
        var ledger = new TestCreditLedger();
        var welcomeHandler = new AccreditWelcomeHandler(ledger);
        var handler = new GoogleOAuthCallbackHandler(
            auth,
            userData,
            tokens,
            welcomeHandler: welcomeHandler,
            creditsFeature: new AlwaysEnabledFlag(),
            logger: NullLogger<GoogleOAuthCallbackHandler>.Instance);

        await handler.HandleAsync(new GoogleOAuthCallbackCommand("auth-code", "http://redirect"), CancellationToken.None);

        ledger.AllEntries.Should().HaveCount(1);
        ledger.AllEntries[0].Reason.Should().Be(CreditLedgerReason.Welcome);
        ledger.AllEntries[0].Delta.Should().Be(3);
        ledger.AllEntries[0].Reference.Should().Be($"welcome:{user.Id}");
    }

    [Fact]
    public async Task GoogleOAuthCallbackHandler_skips_welcome_credits_when_flag_disabled()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Provider = "google",
            ProviderId = "g-skip",
            Email = "skip@b.com",
            Name = "Skip",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var auth = new StubAuthService(Result.Success(new OAuthUserInfo("google", "g-skip", "skip@b.com", "Skip")));
        var userData = new StubUserDataService(user);
        var tokens = new StubRefreshTokenStore();
        var ledger = new TestCreditLedger();
        var welcomeHandler = new AccreditWelcomeHandler(ledger);
        var handler = new GoogleOAuthCallbackHandler(
            auth,
            userData,
            tokens,
            welcomeHandler: welcomeHandler,
            creditsFeature: new AlwaysDisabledFlag(),
            logger: NullLogger<GoogleOAuthCallbackHandler>.Instance);

        await handler.HandleAsync(new GoogleOAuthCallbackCommand("auth-code", "http://redirect"), CancellationToken.None);

        ledger.AllEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task GoogleOAuthCallbackHandler_does_not_double_grant_welcome_on_replay()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Provider = "google",
            ProviderId = "g-replay",
            Email = "replay@b.com",
            Name = "Replay",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var auth = new StubAuthService(Result.Success(new OAuthUserInfo("google", "g-replay", "replay@b.com", "Replay")));
        var userData = new StubUserDataService(user);
        var tokens = new StubRefreshTokenStore();
        var ledger = new TestCreditLedger();
        var welcomeHandler = new AccreditWelcomeHandler(ledger);
        var handler = new GoogleOAuthCallbackHandler(
            auth,
            userData,
            tokens,
            welcomeHandler: welcomeHandler,
            creditsFeature: new AlwaysEnabledFlag(),
            logger: NullLogger<GoogleOAuthCallbackHandler>.Instance);

        await handler.HandleAsync(new GoogleOAuthCallbackCommand("auth-code", "http://redirect"), CancellationToken.None);
        await handler.HandleAsync(new GoogleOAuthCallbackCommand("auth-code", "http://redirect"), CancellationToken.None);

        ledger.AllEntries.Should().HaveCount(1);
    }

    [Fact]
    public async Task LinkedInOAuthCallbackHandler_exchanges_code_and_issues_tokens()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Provider = "linkedin",
            ProviderId = "li-1",
            Email = "b@b.com",
            Name = "Bob",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var auth = new StubAuthService(Result.Success(new OAuthUserInfo("linkedin", "li-1", "b@b.com", "Bob")));
        var userData = new StubUserDataService(user);
        var tokens = new StubRefreshTokenStore();
        var handler = new LinkedInOAuthCallbackHandler(auth, userData, tokens);

        var result = await handler.HandleAsync(new LinkedInOAuthCallbackCommand("auth-code", "http://redirect"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Provider.Should().Be("linkedin");
        result.Value.User.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task LinkedInOAuthCallbackHandler_grants_welcome_credits_when_flag_enabled()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Provider = "linkedin",
            ProviderId = "li-welcome",
            Email = "li-new@b.com",
            Name = "LiNew",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var auth = new StubAuthService(Result.Success(new OAuthUserInfo("linkedin", "li-welcome", "li-new@b.com", "LiNew")));
        var userData = new StubUserDataService(user);
        var tokens = new StubRefreshTokenStore();
        var ledger = new TestCreditLedger();
        var welcomeHandler = new AccreditWelcomeHandler(ledger);
        var handler = new LinkedInOAuthCallbackHandler(
            auth,
            userData,
            tokens,
            welcomeHandler: welcomeHandler,
            creditsFeature: new AlwaysEnabledFlag(),
            logger: NullLogger<LinkedInOAuthCallbackHandler>.Instance);

        await handler.HandleAsync(new LinkedInOAuthCallbackCommand("auth-code", "http://redirect"), CancellationToken.None);

        ledger.AllEntries.Should().HaveCount(1);
        ledger.AllEntries[0].Reason.Should().Be(CreditLedgerReason.Welcome);
    }

    [Fact]
    public async Task RefreshTokenHandler_rotates_tokens()
    {
        var userData = new StubUserDataService(new User { Id = Guid.NewGuid() });
        var tokens = new StubRefreshTokenStore();
        var handler = new RefreshTokenHandler(userData, tokens);

        var result = await handler.HandleAsync(new RefreshTokenCommand("old-refresh-token"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeEmpty();
        result.Value.RefreshToken.Should().Be("refresh-token-abc");
    }

    [Fact]
    public async Task RefreshTokenHandler_fails_when_token_invalid()
    {
        var userData = new StubUserDataService(new User { Id = Guid.NewGuid() });
        var invalidTokens = new StubRevokedRefreshTokenStore();
        var handler = new RefreshTokenHandler(userData, invalidTokens);

        var result = await handler.HandleAsync(new RefreshTokenCommand("revoked-token"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/REFRESH_REVOKED");
    }

    [Fact]
    public async Task LogoutHandler_revokes_token()
    {
        var revoked = new List<string>();
        var tokens = new RevocableRefreshTokenStore(revoked);
        var handler = new LogoutHandler(tokens);

        var result = await handler.HandleAsync(new LogoutCommand("refresh-token-xyz"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        revoked.Should().Contain("refresh-token-xyz");
    }

    // --- Stubs for handler tests ---

    private sealed class StubRevokedRefreshTokenStore : IRefreshTokenStore
    {
        public Task<string> CreateAsync(Guid userId, CancellationToken ct = default) => Task.FromResult("new-token");
        public Task<Result<Guid>> ValidateAsync(string token, CancellationToken ct = default) => Task.FromResult(Result.Failure<Guid>(new Error("AUTH/REFRESH_REVOKED", "Revoked")));
        public Task RevokeAsync(string token, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class RevocableRefreshTokenStore : IRefreshTokenStore
    {
        private readonly List<string> _revoked;
        public RevocableRefreshTokenStore(List<string> revoked) => _revoked = revoked;
        public Task<string> CreateAsync(Guid userId, CancellationToken ct = default) => Task.FromResult("new-token");
        public Task<Result<Guid>> ValidateAsync(string token, CancellationToken ct = default) => Task.FromResult(Result.Success(Guid.NewGuid()));
        public Task RevokeAsync(string token, CancellationToken ct = default) { _revoked.Add(token); return Task.CompletedTask; }
    }

    private sealed class AlwaysEnabledFlag : ICreditsFeatureFlag
    {
        public bool IsEnabled => true;
    }

    private sealed class AlwaysDisabledFlag : ICreditsFeatureFlag
    {
        public bool IsEnabled => false;
    }
}
