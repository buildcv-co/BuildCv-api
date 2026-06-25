using BuildCv.Domain.FeatureFlags;
using FluentAssertions;

namespace BuildCv.Domain.Tests.FeatureFlags;

public sealed class FeatureFlagTests
{
    [Fact]
    public void Create_throws_when_name_is_null()
    {
        var act = () => FeatureFlag.Create(null!, defaultValue: true);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Name*");
    }

    [Fact]
    public void Create_throws_when_name_is_empty_or_whitespace()
    {
        var actEmpty = () => FeatureFlag.Create("", defaultValue: true);
        var actWhitespace = () => FeatureFlag.Create("   ", defaultValue: true);

        actEmpty.Should().Throw<ArgumentException>().WithMessage("*Name*");
        actWhitespace.Should().Throw<ArgumentException>().WithMessage("*Name*");
    }

    [Fact]
    public void Create_defaults_current_value_to_default_value()
    {
        var flagEnabled = FeatureFlag.Create("wompi-enabled", defaultValue: true);
        var flagDisabled = FeatureFlag.Create("factus-enabled", defaultValue: false);

        flagEnabled.CurrentValue.Should().BeTrue();
        flagEnabled.DefaultValue.Should().BeTrue();
        flagDisabled.CurrentValue.Should().BeFalse();
        flagDisabled.DefaultValue.Should().BeFalse();
    }

    [Fact]
    public void Create_sets_updated_at_to_recent_utc_now_and_keeps_updated_by_null()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var flag = FeatureFlag.Create("credits-enabled", defaultValue: true);
        var after = DateTime.UtcNow.AddSeconds(1);

        flag.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        flag.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
        flag.UpdatedBy.Should().BeNull();
    }
}