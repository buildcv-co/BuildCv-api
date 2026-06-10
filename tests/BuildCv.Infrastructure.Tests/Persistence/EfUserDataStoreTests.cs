using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Auth;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class EfUserDataStoreTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfUserDataStore _store;

    public EfUserDataStoreTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new BuildCvDbContext(options);
        _store = new EfUserDataStore(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task GetByIdAsync_returns_user_when_exists()
    {
        var user = CreateUser();
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var result = await _store.GetByIdAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be(user.Email);
        result.Value.Name.Should().Be(user.Name);
    }

    [Fact]
    public async Task GetByIdAsync_returns_failure_when_user_not_found()
    {
        var result = await _store.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ARCO/DATA_NOT_FOUND");
    }

    [Fact]
    public async Task GetByProviderAsync_returns_user_when_exists()
    {
        var user = CreateUser(provider: "google", providerId: "g-123");
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var result = await _store.GetByProviderAsync("google", "g-123");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByProviderAsync_returns_failure_when_not_found()
    {
        var result = await _store.GetByProviderAsync("google", "nonexistent");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ARCO/DATA_NOT_FOUND");
    }

    [Fact]
    public async Task UpsertAsync_inserts_new_user()
    {
        var user = CreateUser();

        await _store.UpsertAsync(user);

        var result = await _dbContext.Users.FindAsync(user.Id);
        result.Should().NotBeNull();
        result!.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task UpsertAsync_updates_existing_user()
    {
        var user = CreateUser();
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var updated = user with { Email = "new@email.com", Name = "New Name" };
        await _store.UpsertAsync(updated);

        var result = await _dbContext.Users.FindAsync(user.Id);
        result!.Email.Should().Be("new@email.com");
        result.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task DeleteAsync_removes_user()
    {
        var user = CreateUser();
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        await _store.DeleteAsync(user.Id);

        var result = await _dbContext.Users.FindAsync(user.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_cascades_to_consent_records()
    {
        var user = CreateUser();
        await _dbContext.Users.AddAsync(user);
        var consent = new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        };
        await _dbContext.ConsentRecords.AddAsync(consent);
        await _dbContext.SaveChangesAsync();

        await _store.DeleteAsync(user.Id);

        var result = await _dbContext.ConsentRecords.FindAsync(consent.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_cascades_to_data_treatment_logs()
    {
        var user = CreateUser();
        await _dbContext.Users.AddAsync(user);
        var log = new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DataType = "profile",
            Action = "access",
            Timestamp = DateTime.UtcNow,
            Reason = "test"
        };
        await _dbContext.DataTreatmentLogs.AddAsync(log);
        await _dbContext.SaveChangesAsync();

        await _store.DeleteAsync(user.Id);

        var result = await _dbContext.DataTreatmentLogs.FindAsync(log.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task AddTreatmentLogAsync_inserts_log()
    {
        var log = new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DataType = "profile",
            Action = "access",
            Timestamp = DateTime.UtcNow,
            Reason = "test"
        };

        await _store.AddTreatmentLogAsync(log);

        var result = await _dbContext.DataTreatmentLogs.FindAsync(log.Id);
        result.Should().NotBeNull();
        result!.Action.Should().Be("access");
    }

    [Fact]
    public async Task GetTreatmentLogsAsync_returns_logs_for_user()
    {
        var userId = Guid.NewGuid();
        var log1 = new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DataType = "profile",
            Action = "access",
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Reason = "first"
        };
        var log2 = new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DataType = "profile",
            Action = "rectify",
            Timestamp = DateTime.UtcNow,
            Reason = "second"
        };
        await _dbContext.DataTreatmentLogs.AddRangeAsync(log1, log2);
        await _dbContext.SaveChangesAsync();

        var logs = await _store.GetTreatmentLogsAsync(userId);

        logs.Should().HaveCount(2);
        logs.Should().Contain(l => l.Action == "access");
        logs.Should().Contain(l => l.Action == "rectify");
    }

    [Fact]
    public async Task GetTreatmentLogsAsync_returns_empty_for_user_with_no_logs()
    {
        var logs = await _store.GetTreatmentLogsAsync(Guid.NewGuid());

        logs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTreatmentLogsAsync_does_not_return_other_users_logs()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var myLog = new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DataType = "profile",
            Action = "access",
            Timestamp = DateTime.UtcNow,
            Reason = "mine"
        };
        var otherLog = new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            DataType = "profile",
            Action = "access",
            Timestamp = DateTime.UtcNow,
            Reason = "other"
        };
        await _dbContext.DataTreatmentLogs.AddRangeAsync(myLog, otherLog);
        await _dbContext.SaveChangesAsync();

        var logs = await _store.GetTreatmentLogsAsync(userId);

        logs.Should().HaveCount(1);
        logs[0].Reason.Should().Be("mine");
    }

    private static User CreateUser(
        string provider = "google",
        string providerId = "g-1",
        string email = "test@email.com",
        string name = "Test User")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            ProviderId = providerId,
            Email = email,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
    }
}
