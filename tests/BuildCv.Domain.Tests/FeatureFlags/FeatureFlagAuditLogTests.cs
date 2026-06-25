using BuildCv.Domain.FeatureFlags;
using FluentAssertions;

namespace BuildCv.Domain.Tests.FeatureFlags;

public sealed class FeatureFlagAuditLogTests
{
    [Fact]
    public void Default_id_is_non_empty_guid()
    {
        var log = new FeatureFlagAuditLog
        {
            FlagName = "wompi-enabled",
            OldValue = true,
            NewValue = false,
            ChangedBy = Guid.NewGuid()
        };

        log.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Default_changed_at_is_recent_utc_now()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var log = new FeatureFlagAuditLog
        {
            FlagName = "wompi-enabled",
            OldValue = true,
            NewValue = false,
            ChangedBy = Guid.NewGuid()
        };
        var after = DateTime.UtcNow.AddSeconds(1);

        log.ChangedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        log.ChangedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}