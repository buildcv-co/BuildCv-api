using BuildCv.Application.Features.FeatureFlags;
using BuildCv.Application.Tests.Common;
using BuildCv.Domain.FeatureFlags;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.FeatureFlags;

public sealed class ListFeatureFlagsHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_all_flags_from_port_sorted()
    {
        var flags = new TestFeatureFlag();
        flags.Seed(FeatureFlag.Create("wompi-enabled", defaultValue: true));
        flags.Seed(FeatureFlag.Create("credits-enabled", defaultValue: false));
        flags.Seed(FeatureFlag.Create("factus-enabled", defaultValue: true));
        var handler = new ListFeatureFlagsHandler(flags);

        var result = await handler.HandleAsync();

        result.Should().HaveCount(3);
        result.Select(f => f.Name).Should().ContainInOrder("credits-enabled", "factus-enabled", "wompi-enabled");
    }

    [Fact]
    public async Task HandleAsync_returns_empty_list_when_port_is_empty()
    {
        var flags = new TestFeatureFlag();
        var handler = new ListFeatureFlagsHandler(flags);

        var result = await handler.HandleAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_propagates_cancellation()
    {
        var flags = new TestFeatureFlag();
        var handler = new ListFeatureFlagsHandler(flags);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => handler.HandleAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}