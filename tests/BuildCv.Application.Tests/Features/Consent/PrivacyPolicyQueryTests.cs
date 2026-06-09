using BuildCv.Application.Features.Consent;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Consent;

public sealed class PrivacyPolicyQueryTests
{
    [Fact]
    public async Task HandleAsync_returns_current_policy_when_no_version_requested()
    {
        var handler = new PrivacyPolicyQueryHandler();

        var result = await handler.HandleAsync(
            new PrivacyPolicyQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.Version.Should().BeGreaterThan(0);
        result.Content.Should().NotBeEmpty();
        result.EffectiveDate.Should().BeBefore(DateTime.UtcNow.AddDays(1));
    }

    [Fact]
    public async Task HandleAsync_returns_specific_version_when_requested()
    {
        var handler = new PrivacyPolicyQueryHandler();

        var result = await handler.HandleAsync(
            new PrivacyPolicyQuery(Version: 1), CancellationToken.None);

        result.Should().NotBeNull();
        result.Version.Should().Be(1);
        result.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_returns_failure_for_nonexistent_version()
    {
        var handler = new PrivacyPolicyQueryHandler();

        var act = () => handler.HandleAsync(
            new PrivacyPolicyQuery(Version: 999), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
