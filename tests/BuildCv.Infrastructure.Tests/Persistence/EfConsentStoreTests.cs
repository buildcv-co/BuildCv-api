using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Auth;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Tests.Persistence;

public sealed class EfConsentStoreTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfConsentStore _store;

    public EfConsentStoreTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new BuildCvDbContext(options);
        _store = new EfConsentStore(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task AddAsync_inserts_record()
    {
        var userId = Guid.NewGuid();
        var record = CreateConsentRecord(userId, "scoring");

        await _store.AddAsync(record);

        var result = await _dbContext.ConsentRecords.FindAsync(record.Id);
        result.Should().NotBeNull();
        result!.Purpose.Should().Be("scoring");
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetActiveAsync_returns_active_record()
    {
        var userId = Guid.NewGuid();
        var record = CreateConsentRecord(userId, "scoring");
        await _store.AddAsync(record);

        var active = await _store.GetActiveAsync(userId, "scoring");

        active.Should().NotBeNull();
        active!.Purpose.Should().Be("scoring");
        active.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetActiveAsync_returns_null_when_revoked()
    {
        var userId = Guid.NewGuid();
        var record = CreateConsentRecord(userId, "scoring");
        await _store.AddAsync(record);

        await _store.RevokeAllAsync(userId, DateTime.UtcNow);

        var active = await _store.GetActiveAsync(userId, "scoring");
        active.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveAsync_returns_null_for_wrong_purpose()
    {
        var userId = Guid.NewGuid();
        var record = CreateConsentRecord(userId, "scoring");
        await _store.AddAsync(record);

        var active = await _store.GetActiveAsync(userId, "analytics");

        active.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveAsync_returns_null_for_different_user()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var record = CreateConsentRecord(userId, "scoring");
        await _store.AddAsync(record);

        var active = await _store.GetActiveAsync(otherUserId, "scoring");

        active.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestAsync_returns_most_recent_record()
    {
        var userId = Guid.NewGuid();
        var older = CreateConsentRecord(userId, "scoring", policyVersion: 1, consentDate: DateTime.UtcNow.AddHours(-2));
        var newer = CreateConsentRecord(userId, "scoring", policyVersion: 2, consentDate: DateTime.UtcNow);
        await _store.AddAsync(older);
        await _store.AddAsync(newer);

        var latest = await _store.GetLatestAsync(userId, "scoring");

        latest.Should().NotBeNull();
        latest!.PolicyVersion.Should().Be(2);
    }

    [Fact]
    public async Task GetLatestAsync_returns_null_when_no_records()
    {
        var latest = await _store.GetLatestAsync(Guid.NewGuid(), "scoring");

        latest.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoryAsync_returns_all_records_ordered_by_date()
    {
        var userId = Guid.NewGuid();
        var first = CreateConsentRecord(userId, "scoring", policyVersion: 1, consentDate: DateTime.UtcNow.AddHours(-2));
        var second = CreateConsentRecord(userId, "scoring", policyVersion: 2, consentDate: DateTime.UtcNow.AddHours(-1));
        var third = CreateConsentRecord(userId, "analytics", policyVersion: 1, consentDate: DateTime.UtcNow);
        await _store.AddAsync(first);
        await _store.AddAsync(second);
        await _store.AddAsync(third);

        var history = await _store.GetHistoryAsync(userId);

        history.Should().HaveCount(3);
        history[0].ConsentDate.Should().BeAfter(history[1].ConsentDate);
        history[1].ConsentDate.Should().BeAfter(history[2].ConsentDate);
    }

    [Fact]
    public async Task GetHistoryAsync_returns_empty_for_user_with_no_records()
    {
        var history = await _store.GetHistoryAsync(Guid.NewGuid());

        history.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeAllAsync_sets_revoked_at_on_active_records()
    {
        var userId = Guid.NewGuid();
        var record1 = CreateConsentRecord(userId, "scoring");
        var record2 = CreateConsentRecord(userId, "analytics");
        await _store.AddAsync(record1);
        await _store.AddAsync(record2);
        var revokedAt = DateTime.UtcNow;

        await _store.RevokeAllAsync(userId, revokedAt);

        var all = await _dbContext.ConsentRecords.Where(c => c.UserId == userId).ToListAsync();
        all.Should().AllSatisfy(r => r.RevokedAt.Should().NotBeNull());
    }

    [Fact]
    public async Task RevokeAllAsync_does_not_affect_other_users()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var myRecord = CreateConsentRecord(userId, "scoring");
        var otherRecord = CreateConsentRecord(otherUserId, "scoring");
        await _store.AddAsync(myRecord);
        await _store.AddAsync(otherRecord);

        await _store.RevokeAllAsync(userId, DateTime.UtcNow);

        var other = await _store.GetActiveAsync(otherUserId, "scoring");
        other.Should().NotBeNull();
    }

    [Fact]
    public async Task GetActiveAsync_returns_revoked_record_as_active_when_its_the_latest()
    {
        var userId = Guid.NewGuid();
        var record = CreateConsentRecord(userId, "scoring");
        await _store.AddAsync(record);

        await _store.RevokeAllAsync(userId, DateTime.UtcNow);

        var latest = await _store.GetLatestAsync(userId, "scoring");
        latest.Should().NotBeNull();
        latest!.RevokedAt.Should().NotBeNull();
    }

    private static ConsentRecord CreateConsentRecord(
        Guid userId,
        string purpose,
        int policyVersion = 1,
        DateTime? consentDate = null)
    {
        return new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = policyVersion,
            ConsentDate = consentDate ?? DateTime.UtcNow,
            Purpose = purpose
        };
    }
}
