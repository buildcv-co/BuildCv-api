using BuildCv.Application.Common;
using BuildCv.Application.Features.FeatureFlags;
using BuildCv.Application.Tests.Common;
using BuildCv.Domain.FeatureFlags;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.FeatureFlags;

public sealed class GetFeatureFlagHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_flag_when_exists()
    {
        var flags = new TestFeatureFlag();
        flags.Seed(FeatureFlag.Create("wompi-enabled", defaultValue: true));
        var handler = new GetFeatureFlagHandler(flags);

        var result = await handler.HandleAsync("wompi-enabled");

        result.Should().NotBeNull();
        result!.Name.Should().Be("wompi-enabled");
        result.CurrentValue.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_not_found()
    {
        var flags = new TestFeatureFlag();
        var handler = new GetFeatureFlagHandler(flags);

        var result = await handler.HandleAsync("missing-flag");

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_propagates_cancellation()
    {
        var flags = new TestFeatureFlag();
        var handler = new GetFeatureFlagHandler(flags);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => handler.HandleAsync("wompi-enabled", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task HandleAsync_passes_name_to_port_unchanged()
    {
        var flags = new TestFeatureFlag();
        flags.Seed(FeatureFlag.Create("factus-enabled", defaultValue: false));
        var handler = new GetFeatureFlagHandler(flags);

        var result = await handler.HandleAsync("factus-enabled");

        result.Should().NotBeNull();
        result!.Name.Should().Be("factus-enabled");
        result.CurrentValue.Should().BeFalse();
    }
}
