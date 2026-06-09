using BuildCv.Application.Features.Auth;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Auth;

public sealed class ConsentHandlerTests
{
    [Fact]
    public async Task GrantConsentHandler_creates_consent_when_no_active()
    {
        var store = new InMemoryConsentStore();
        var handler = new GrantConsentHandler(store);

        var result = await handler.HandleAsync(
            new GrantConsentCommand(Guid.NewGuid(), "scoring", 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Purpose.Should().Be("scoring");
        result.Value.PolicyVersion.Should().Be(1);
        result.Value.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GrantConsentHandler_fails_when_active_consent_exists()
    {
        var userId = Guid.NewGuid();
        var store = new InMemoryConsentStore();
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });
        var handler = new GrantConsentHandler(store);

        var result = await handler.HandleAsync(
            new GrantConsentCommand(userId, "scoring", 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CONSENT/ALREADY_GRANTED");
    }

    [Fact]
    public async Task GrantConsentHandler_allows_reconsent_on_newer_policy()
    {
        var userId = Guid.NewGuid();
        var store = new InMemoryConsentStore();
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });
        var handler = new GrantConsentHandler(store);

        var result = await handler.HandleAsync(
            new GrantConsentCommand(userId, "scoring", 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PolicyVersion.Should().Be(2);
    }

    [Fact]
    public async Task RevokeConsentHandler_revokes_active_consent()
    {
        var userId = Guid.NewGuid();
        var store = new InMemoryConsentStore();
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });
        var handler = new RevokeConsentHandler(store);

        var result = await handler.HandleAsync(
            new RevokeConsentCommand(userId, "scoring"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var history = await store.GetHistoryAsync(userId);
        history.Should().Contain(r => r.Purpose == "scoring" && !r.IsValid);
    }

    [Fact]
    public async Task HasActiveConsentQuery_returns_true_when_granted()
    {
        var userId = Guid.NewGuid();
        var store = new InMemoryConsentStore();
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });
        var handler = new HasActiveConsentHandler(store);

        var result = await handler.HandleAsync(
            new HasActiveConsentQuery(userId, "scoring"), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveConsentQuery_returns_false_when_none()
    {
        var store = new InMemoryConsentStore();
        var handler = new HasActiveConsentHandler(store);

        var result = await handler.HandleAsync(
            new HasActiveConsentQuery(Guid.NewGuid(), "scoring"), CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetConsentHistoryHandler_returns_all_records()
    {
        var userId = Guid.NewGuid();
        var store = new InMemoryConsentStore();
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });
        store.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 2,
            ConsentDate = DateTime.UtcNow,
            Purpose = "scoring"
        });
        var handler = new GetConsentHistoryHandler(store);

        var result = await handler.HandleAsync(
            new GetConsentHistoryQuery(userId), CancellationToken.None);

        result.Should().HaveCount(2);
    }
}
