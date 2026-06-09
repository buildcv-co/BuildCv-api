using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Auth;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Auth;

public sealed class InMemoryStoresTests
{
    // --- Consent Store Tests ---

    [Fact]
    public async Task ConsentStore_add_and_get_active()
    {
        var store = new InMemoryConsentStore();
        var userId = Guid.NewGuid();
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });

        var active = await store.GetActiveAsync(userId, "scoring");

        active.Should().NotBeNull();
        active!.Purpose.Should().Be("scoring");
    }

    [Fact]
    public async Task ConsentStore_active_returns_null_when_revoked()
    {
        var store = new InMemoryConsentStore();
        var userId = Guid.NewGuid();
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });
        store.RevokeAll(userId, DateTime.UtcNow);

        var active = await store.GetActiveAsync(userId, "scoring");

        active.Should().BeNull();
    }

    [Fact]
    public async Task ConsentStore_history_returns_all_records()
    {
        var store = new InMemoryConsentStore();
        var userId = Guid.NewGuid();
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 2,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });

        var history = await store.GetHistoryAsync(userId);

        history.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConsentStore_active_returns_null_for_wrong_purpose()
    {
        var store = new InMemoryConsentStore();
        var userId = Guid.NewGuid();
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });

        var active = await store.GetActiveAsync(userId, "other");

        active.Should().BeNull();
    }

    // --- User Data Store Tests ---

    [Fact]
    public async Task UserDataStore_upsert_and_get()
    {
        var store = new InMemoryUserDataStore();
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
        store.Upsert(user);

        var result = await store.GetByIdAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("a@b.com");
    }

    [Fact]
    public async Task UserDataStore_get_returns_failure_for_missing_user()
    {
        var store = new InMemoryUserDataStore();

        var result = await store.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ARCO/DATA_NOT_FOUND");
    }

    [Fact]
    public async Task UserDataStore_delete_removes_user()
    {
        var store = new InMemoryUserDataStore();
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
        store.Upsert(user);

        store.Delete(user.Id);
        var result = await store.GetByIdAsync(user.Id);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UserDataStore_upsert_replaces_existing()
    {
        var store = new InMemoryUserDataStore();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Provider = "google",
            ProviderId = "g-1",
            Email = "old@b.com",
            Name = "Old",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        store.Upsert(user);

        var updated = user with { Email = "new@b.com", Name = "New" };
        store.Upsert(updated);

        var result = await store.GetByIdAsync(user.Id);
        result.Value.Email.Should().Be("new@b.com");
        result.Value.Name.Should().Be("New");
    }

    [Fact]
    public async Task UserDataStore_adds_treatment_logs()
    {
        var store = new InMemoryUserDataStore();
        var userId = Guid.NewGuid();
        store.AddLog(new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DataType = "profile",
            Action = "access",
            Timestamp = DateTime.UtcNow,
            Reason = "test"
        });

        var logs = await store.GetTreatmentLogsAsync(userId);

        logs.Should().HaveCount(1);
        logs[0].Action.Should().Be("access");
    }

    [Fact]
    public async Task UserDataStore_treatment_logs_is_append_only()
    {
        var store = new InMemoryUserDataStore();
        var userId = Guid.NewGuid();
        store.AddLog(new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DataType = "profile",
            Action = "access",
            Timestamp = DateTime.UtcNow,
            Reason = "first"
        });
        store.AddLog(new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DataType = "profile",
            Action = "rectify",
            Timestamp = DateTime.UtcNow,
            Reason = "second"
        });

        var logs = await store.GetTreatmentLogsAsync(userId);

        logs.Should().HaveCount(2);
        logs.Should().Contain(l => l.Action == "access");
        logs.Should().Contain(l => l.Action == "rectify");
    }

    [Fact]
    public async Task UserDataStore_initial_user_is_available()
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
        var store = new InMemoryUserDataStore(user);

        var result = await store.GetByIdAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Alice");
    }
}
