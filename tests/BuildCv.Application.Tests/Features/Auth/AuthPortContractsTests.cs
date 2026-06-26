using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Auth;

public sealed class AuthPortContractsTests
{
    [Fact]
    public async Task IAuthenticationService_can_be_mocked_and_returns_OAuthUserInfo()
    {
        var userInfo = new OAuthUserInfo("google", "g-1", "a@b.com", "Alice");
        var mock = new MockAuthenticationService(Result.Success(userInfo));

        var result = await mock.ExchangeCodeAsync("google", "code", "http://redirect");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(userInfo);
    }

    [Fact]
    public async Task IAuthenticationService_can_return_failure()
    {
        var error = new Error("AUTH/OAUTH_FAILED", "Exchange failed");
        var mock = new MockAuthenticationService(Result.Failure<OAuthUserInfo>(error));

        var result = await mock.ExchangeCodeAsync("google", "bad", "http://redirect");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AUTH/OAUTH_FAILED");
    }

    [Fact]
    public async Task IConsentService_GrantAsync_returns_ConsentRecord()
    {
        var userId = Guid.NewGuid();
        var record = new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        };
        var mock = new MockConsentService(record);

        var result = await mock.GrantAsync(userId, "scoring");

        result.IsSuccess.Should().BeTrue();
        result.Value.Purpose.Should().Be("scoring");
        result.Value.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task IConsentService_HasActiveConsentAsync_returns_true_when_granted()
    {
        var mock = new MockConsentService(hasActiveConsent: true);

        var has = await mock.HasActiveConsentAsync(Guid.NewGuid(), "scoring");

        has.Should().BeTrue();
    }

    [Fact]
    public async Task IConsentService_HasActiveConsentAsync_returns_false_when_none()
    {
        var mock = new MockConsentService(hasActiveConsent: false);

        var has = await mock.HasActiveConsentAsync(Guid.NewGuid(), "scoring");

        has.Should().BeFalse();
    }

    [Fact]
    public async Task IUserDataService_GetOrCreateAsync_returns_user()
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
        var mock = new MockUserDataService(user);

        var result = await mock.GetOrCreateAsync("google", "g-1", "a@b.com", "Alice");

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("a@b.com");
    }

    [Fact]
    public async Task IUserDataService_DeleteAsync_succeeds()
    {
        var mock = new MockUserDataService(deleteResult: true);

        var result = await mock.DeleteAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task IRefreshTokenStore_CreateAsync_returns_token_string()
    {
        var mock = new MockRefreshTokenStore("test-refresh-token");

        var token = await mock.CreateAsync(Guid.NewGuid());

        token.Should().Be("test-refresh-token");
    }

    [Fact]
    public async Task IRefreshTokenStore_ValidateAsync_returns_user_id()
    {
        var userId = Guid.NewGuid();
        var mock = new MockRefreshTokenStore(userId: userId);

        var result = await mock.ValidateAsync("valid-token");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(userId);
    }

    [Fact]
    public async Task IRefreshTokenStore_ValidateAsync_fails_for_invalid_token()
    {
        var mock = new MockRefreshTokenStore(validToken: false);

        var result = await mock.ValidateAsync("bad-token");

        result.IsFailure.Should().BeTrue();
    }

    // --- Minimal mock implementations to verify interface contracts ---

    private sealed class MockAuthenticationService : IAuthenticationService
    {
        private readonly Result<OAuthUserInfo> _result;
        public MockAuthenticationService(Result<OAuthUserInfo> result) => _result = result;
        public Task<Result<OAuthUserInfo>> ExchangeCodeAsync(string provider, string code, string redirectUri, CancellationToken ct = default)
            => Task.FromResult(_result);
    }

    private sealed class MockConsentService : IConsentService
    {
        private readonly ConsentRecord? _record;
        private readonly bool _hasActiveConsent;
        public MockConsentService(ConsentRecord record) { _record = record; _hasActiveConsent = true; }
        public MockConsentService(bool hasActiveConsent) { _hasActiveConsent = hasActiveConsent; }

        public Task<Result<ConsentRecord>> GrantAsync(Guid userId, string purpose, CancellationToken ct = default)
            => Task.FromResult(Result.Success(_record!));
        public Task<Result> RevokeAsync(Guid userId, string purpose, CancellationToken ct = default)
            => Task.FromResult(Result.Success());
        public Task<bool> HasActiveConsentAsync(Guid userId, string purpose, CancellationToken ct = default)
            => Task.FromResult(_hasActiveConsent);
        public Task<IReadOnlyList<ConsentRecord>> GetConsentHistoryAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ConsentRecord>>(Array.Empty<ConsentRecord>());
    }

    private sealed class MockUserDataService : IUserDataService
    {
        private readonly User? _user;
        private readonly bool _deleteResult;
        public MockUserDataService(User user) { _user = user; _deleteResult = true; }
        public MockUserDataService(bool deleteResult) { _deleteResult = deleteResult; }

        public Task<Result<User>> GetOrCreateAsync(string provider, string providerId, string email, string name, CancellationToken ct = default)
            => Task.FromResult(Result.Success(_user!));
        public Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(Result.Success(_user!));
        public Task<Result<User>> UpdateAsync(Guid userId, string? email, string? name, CancellationToken ct = default)
            => Task.FromResult(Result.Success(_user!));
        public Task<Result> DeleteAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(_deleteResult ? Result.Success() : Result.Failure(new Error("DELETE", "Failed")));
        public Task<IReadOnlyList<DataTreatmentLog>> GetTreatmentLogsAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DataTreatmentLog>>(Array.Empty<DataTreatmentLog>());
    }

    private sealed class MockRefreshTokenStore : IRefreshTokenStore
    {
        private readonly string _token;
        private readonly bool _validToken;
        private readonly Guid _userId;
        public MockRefreshTokenStore(string token) { _token = token; _validToken = true; _userId = Guid.NewGuid(); }
        public MockRefreshTokenStore(Guid userId) { _token = "tok"; _validToken = true; _userId = userId; }
        public MockRefreshTokenStore(bool validToken) { _token = "tok"; _validToken = validToken; _userId = Guid.NewGuid(); }

        public Task<string> CreateAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(_token);
        public Task<Result<Guid>> ValidateAsync(string token, CancellationToken ct = default)
            => Task.FromResult(_validToken ? Result.Success(_userId) : Result.Failure<Guid>(new Error("AUTH/REFRESH_REVOKED", "Revoked")));
        public Task RevokeAsync(string token, CancellationToken ct = default) => Task.CompletedTask;
        public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
