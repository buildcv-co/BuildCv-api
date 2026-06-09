using BuildCv.Domain.Auth;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Auth;

public sealed class UserTests
{
    [Fact]
    public void User_initializes_with_all_properties()
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Provider = "google",
            ProviderId = "g-123",
            Email = "alice@example.com",
            Name = "Alice",
            CreatedAt = now,
            LastLoginAt = now
        };

        user.Provider.Should().Be("google");
        user.ProviderId.Should().Be("g-123");
        user.Email.Should().Be("alice@example.com");
        user.Name.Should().Be("Alice");
        user.CreatedAt.Should().Be(now);
        user.LastLoginAt.Should().Be(now);
    }

    [Fact]
    public void User_default_values_are_empty_strings()
    {
        var user = new User();

        user.Provider.Should().BeEmpty();
        user.ProviderId.Should().BeEmpty();
        user.Email.Should().BeEmpty();
        user.Name.Should().BeEmpty();
    }

    [Fact]
    public void ConsentRecord_IsValid_is_true_when_not_revoked()
    {
        var record = new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        };

        record.IsValid.Should().BeTrue();
        record.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void ConsentRecord_IsValid_is_false_when_revoked()
    {
        var revokedAt = DateTime.UtcNow;
        var record = new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            RevokedAt = revokedAt,
            Purpose = "scoring"
        };

        record.IsValid.Should().BeFalse();
        record.RevokedAt.Should().Be(revokedAt);
    }

    [Fact]
    public void ConsentRecord_default_values_are_empty()
    {
        var record = new ConsentRecord();

        record.Purpose.Should().BeEmpty();
        record.PolicyVersion.Should().Be(0);
    }

    [Fact]
    public void DataTreatmentLog_initializes_with_all_properties()
    {
        var now = DateTime.UtcNow;
        var log = new DataTreatmentLog
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DataType = "profile",
            Action = "access",
            Timestamp = now,
            Reason = "ARCO request"
        };

        log.DataType.Should().Be("profile");
        log.Action.Should().Be("access");
        log.Timestamp.Should().Be(now);
        log.Reason.Should().Be("ARCO request");
    }

    [Fact]
    public void DataTreatmentLog_default_values_are_empty()
    {
        var log = new DataTreatmentLog();

        log.DataType.Should().BeEmpty();
        log.Action.Should().BeEmpty();
        log.Reason.Should().BeEmpty();
    }
}
