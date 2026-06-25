using BuildCv.Domain.FeatureFlags;
using FluentAssertions;

namespace BuildCv.Domain.Tests.FeatureFlags;

public sealed class FeatureFlagNotFoundExceptionTests
{
    [Fact]
    public void Constructor_stores_flag_name_and_message_includes_it()
    {
        var ex = new FeatureFlagNotFoundException("reports-v2-enabled");

        ex.FlagName.Should().Be("reports-v2-enabled");
        ex.Message.Should().Contain("reports-v2-enabled");
    }
}
