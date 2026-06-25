using BuildCv.Application.Features.Subscriptions;
using BuildCv.Infrastructure.Payments;
using BuildCv.Infrastructure.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Infrastructure.Tests.Subscriptions;

public sealed class DisabledSubscriptionProviderTests
{
    [Fact]
    public void Implements_contract()
    {
        var provider = new DisabledSubscriptionProvider(NullLogger<DisabledSubscriptionProvider>.Instance);
        provider.Should().BeAssignableTo<ISubscriptionProvider>();
    }

    [Fact]
    public async Task CreateScheduledChargeAsync_throws_NotSupportedException()
    {
        var provider = new DisabledSubscriptionProvider(NullLogger<DisabledSubscriptionProvider>.Instance);

        var act = () => provider.CreateScheduledChargeAsync("ps_x", 30_000m, "COP", DateTime.UtcNow);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task CancelScheduledChargeAsync_returns_true()
    {
        var provider = new DisabledSubscriptionProvider(NullLogger<DisabledSubscriptionProvider>.Instance);

        var result = await provider.CancelScheduledChargeAsync("ch_x");

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyWebhookSignature_returns_false()
    {
        var provider = new DisabledSubscriptionProvider(NullLogger<DisabledSubscriptionProvider>.Instance);

        provider.VerifyWebhookSignature("payload", "sig").Should().BeFalse();
    }
}

public sealed class SubscriptionFeatureFlagTests
{
    [Fact]
    public void Implements_contract()
    {
        var flag = NewFlag();
        flag.Should().BeAssignableTo<ISubscriptionFeatureFlag>();
    }

    [Fact]
    public void IsEnabled_is_false_when_section_missing()
    {
        var flag = new SubscriptionFeatureFlag(new ConfigurationBuilder().Build());

        flag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_is_false_when_Enabled_is_false()
    {
        var flag = NewFlag(("SubscriptionRecurring:Enabled", "false"));

        flag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_is_true_when_Enabled_is_true()
    {
        var flag = NewFlag(("SubscriptionRecurring:Enabled", "true"));

        flag.IsEnabled.Should().BeTrue();
    }

    private static SubscriptionFeatureFlag NewFlag(params (string Key, string Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)));
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        return new SubscriptionFeatureFlag(config);
    }
}
