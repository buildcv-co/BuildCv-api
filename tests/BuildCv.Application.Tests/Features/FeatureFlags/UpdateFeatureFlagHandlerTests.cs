using BuildCv.Application.Common;
using BuildCv.Application.Features.FeatureFlags;
using BuildCv.Domain.FeatureFlags;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Application.Tests.Features.FeatureFlags;

public sealed class UpdateFeatureFlagHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_flag_when_admin_service_succeeds()
    {
        var admin = new FakeAdminService();
        admin.NextResult = new FeatureFlag
        {
            Name = "wompi-enabled",
            DefaultValue = true,
            CurrentValue = false,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = Guid.NewGuid()
        };
        var handler = new UpdateFeatureFlagHandler(admin);

        var result = await handler.HandleAsync("wompi-enabled", newValue: false, Guid.NewGuid(), "incident P1-273");

        result.CurrentValue.Should().BeFalse();
        result.Name.Should().Be("wompi-enabled");
    }

    [Fact]
    public async Task HandleAsync_passes_all_args_unchanged_to_admin_service()
    {
        var admin = new FakeAdminService();
        var handler = new UpdateFeatureFlagHandler(admin);
        var changedBy = Guid.NewGuid();

        await handler.HandleAsync("credits-enabled", newValue: true, changedBy, "production rollout");

        admin.LastName.Should().Be("credits-enabled");
        admin.LastNewValue.Should().BeTrue();
        admin.LastChangedBy.Should().Be(changedBy);
        admin.LastReason.Should().Be("production rollout");
    }

    [Fact]
    public async Task HandleAsync_propagates_FeatureFlagNotFoundException()
    {
        var admin = new FakeAdminService
        {
            ThrowOnUpdate = new FeatureFlagNotFoundException("reports-v2-enabled")
        };
        var handler = new UpdateFeatureFlagHandler(admin);

        var act = () => handler.HandleAsync("reports-v2-enabled", newValue: true, Guid.NewGuid(), null);

        await act.Should().ThrowAsync<FeatureFlagNotFoundException>()
            .Where(ex => ex.FlagName == "reports-v2-enabled");
    }

    [Fact]
    public async Task HandleAsync_propagates_DbUpdateConcurrencyException()
    {
        var concurrencyException = new DbUpdateConcurrencyException("xmin mismatch on feature_flags");
        var admin = new FakeAdminService
        {
            ThrowOnUpdate = concurrencyException
        };
        var handler = new UpdateFeatureFlagHandler(admin);

        var act = () => handler.HandleAsync("wompi-enabled", newValue: false, Guid.NewGuid(), "concurrent flip");

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task HandleAsync_propagates_cancellation()
    {
        var admin = new FakeAdminService();
        var handler = new UpdateFeatureFlagHandler(admin);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => handler.HandleAsync("wompi-enabled", newValue: true, Guid.NewGuid(), null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

internal sealed class FakeAdminService : IFeatureFlagAdminService
{
    public FeatureFlag? NextResult { get; set; }
    public Exception? ThrowOnUpdate { get; set; }
    public string? LastName { get; private set; }
    public bool LastNewValue { get; private set; }
    public Guid LastChangedBy { get; private set; }
    public string? LastReason { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<FeatureFlag> UpdateAsync(
        string name, bool newValue, Guid changedBy, string? reason, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        LastName = name;
        LastNewValue = newValue;
        LastChangedBy = changedBy;
        LastReason = reason;
        LastCancellationToken = ct;

        if (ThrowOnUpdate is not null)
        {
            throw ThrowOnUpdate;
        }

        return Task.FromResult(NextResult ?? FeatureFlag.Create(name, newValue));
    }
}