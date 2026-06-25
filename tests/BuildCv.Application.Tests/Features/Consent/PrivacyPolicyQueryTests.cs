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

    [Fact]
    public async Task HandleAsync_returns_v2_policy_with_credit_balance_ledger_arc_and_dian_disclosure()
    {
        var handler = new PrivacyPolicyQueryHandler();

        var result = await handler.HandleAsync(
            new PrivacyPolicyQuery(Version: 2), CancellationToken.None);

        result.Should().NotBeNull();
        result.Version.Should().Be(2);
        result.Content.Should().Contain("credit balance");
        result.Content.Should().Contain("ARCO");
        result.Content.Should().Contain("DIAN");
        result.Content.Should().Contain("ledger");
    }

    [Fact]
    public async Task HandleAsync_returns_v3_policy_with_subscription_disclosure()
    {
        var handler = new PrivacyPolicyQueryHandler();

        var result = await handler.HandleAsync(
            new PrivacyPolicyQuery(Version: 3), CancellationToken.None);

        result.Should().NotBeNull();
        result.Version.Should().Be(3);
        result.Content.Should().Contain("Subscriptions",
            "v3 must add a Subscriptions section that discloses what subscription data we store");
        result.Content.Should().Contain("Wompi",
            "v3 must mention that payment source tokenization is Wompi-side");
        result.Content.Should().Contain("tokenized",
            "v3 must clarify that the card never touches our servers (Art. III)");
        result.Content.Should().Contain("ARCO",
            "v3 must disclose the ARCO pre-cancel + cascade-delete behavior (Art. IX)");
        result.Content.Should().Contain("non-refundable",
            "v3 must disclose the no-refund policy on cancellation (Art. IV)");
        result.DataCategories.Should().Contain(c => c.Contains("Subscription", StringComparison.OrdinalIgnoreCase));
        result.Purposes.Should().Contain(p => p.Contains("Subscription", StringComparison.OrdinalIgnoreCase)
                                              || p.Contains("Recurring", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HandleAsync_without_version_returns_latest_policy_v3()
    {
        var handler = new PrivacyPolicyQueryHandler();

        var result = await handler.HandleAsync(
            new PrivacyPolicyQuery(), CancellationToken.None);

        result.Version.Should().Be(3, "the highest published privacy policy version must be returned when no version is requested");
    }
}
