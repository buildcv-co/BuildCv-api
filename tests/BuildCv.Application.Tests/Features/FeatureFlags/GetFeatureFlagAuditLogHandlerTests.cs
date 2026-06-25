using BuildCv.Application.Features.FeatureFlags;
using BuildCv.Application.Tests.Common;
using BuildCv.Domain.FeatureFlags;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.FeatureFlags;

public sealed class GetFeatureFlagAuditLogHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_entries_for_flag()
    {
        var store = new TestFeatureFlagStore();
        await SeedEntryAsync(store, "wompi-enabled", DateTime.UtcNow.AddSeconds(-10));
        await SeedEntryAsync(store, "wompi-enabled", DateTime.UtcNow);
        await SeedEntryAsync(store, "factus-enabled", DateTime.UtcNow);

        var handler = new GetFeatureFlagAuditLogHandler(store);

        var (entries, nextCursor) = await handler.HandleAsync("wompi-enabled", limit: 10, cursor: null);

        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(e => e.FlagName == "wompi-enabled");
        nextCursor.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_defaults_limit_to_fifty_when_null()
    {
        var store = new TestFeatureFlagStore();
        for (var i = 0; i < 75; i++)
        {
            await SeedEntryAsync(store, "wompi-enabled", DateTime.UtcNow.AddSeconds(-i));
        }

        var handler = new GetFeatureFlagAuditLogHandler(store);

        var (entries, nextCursor) = await handler.HandleAsync("wompi-enabled", limit: null, cursor: null);

        entries.Should().HaveCount(50);
        nextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_clamps_limit_to_200_when_above()
    {
        var store = new TestFeatureFlagStore();
        for (var i = 0; i < 250; i++)
        {
            await SeedEntryAsync(store, "wompi-enabled", DateTime.UtcNow.AddSeconds(-i));
        }

        var handler = new GetFeatureFlagAuditLogHandler(store);

        var (entries, _) = await handler.HandleAsync("wompi-enabled", limit: 5000, cursor: null);

        entries.Should().HaveCount(200);
    }

    [Fact]
    public async Task HandleAsync_returns_next_cursor_when_results_equal_limit()
    {
        var store = new TestFeatureFlagStore();
        for (var i = 0; i < 3; i++)
        {
            await SeedEntryAsync(store, "wompi-enabled", DateTime.UtcNow.AddSeconds(-i));
        }

        var handler = new GetFeatureFlagAuditLogHandler(store);

        var (entries, nextCursor) = await handler.HandleAsync("wompi-enabled", limit: 3, cursor: null);

        entries.Should().HaveCount(3);
        nextCursor.Should().NotBeNullOrEmpty();
        var (ticks, id) = CursorCodec.Decode(nextCursor!);
        entries[^1].ChangedAt.Ticks.Should().Be(ticks);
        entries[^1].Id.Should().Be(id);
    }

    private static Task SeedEntryAsync(TestFeatureFlagStore store, string flagName, DateTime changedAt)
        => store.AppendAuditLogAsync(new FeatureFlagAuditLog
        {
            FlagName = flagName,
            OldValue = null,
            NewValue = true,
            ChangedBy = Guid.NewGuid(),
            ChangedAt = changedAt,
            Reason = $"seed-{flagName}"
        });
}
