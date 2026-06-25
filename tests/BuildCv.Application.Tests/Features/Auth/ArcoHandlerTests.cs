using BuildCv.Application.Features.Auth;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Application.Tests.Features.Subscriptions;
using BuildCv.Domain.Auth;
using BuildCv.Domain.Common;
using BuildCv.Domain.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Application.Tests.Features.Auth;

public sealed class ArcoHandlerTests
{
    [Fact]
    public async Task GetUserDataHandler_returns_user_and_logs_access()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-1",
            Email = "a@b.com",
            Name = "Alice",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var consent = new InMemoryConsentStore();
        consent.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "data-access"
        });
        var userData = new InMemoryUserDataStore(user);
        var handler = new GetUserDataHandler(consent, userData);

        var result = await handler.HandleAsync(
            new GetUserDataQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("a@b.com");
        var logs = await userData.GetTreatmentLogsAsync(userId);
        logs.Should().Contain(l => l.Action == "access");
    }

    [Fact]
    public async Task GetUserDataHandler_fails_without_consent()
    {
        var userData = new InMemoryUserDataStore(new User { Id = Guid.NewGuid() });
        var consent = new InMemoryConsentStore();
        var handler = new GetUserDataHandler(consent, userData);

        var result = await handler.HandleAsync(
            new GetUserDataQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CONSENT/REQUIRED");
    }

    [Fact]
    public async Task RectifyUserDataHandler_updates_fields_and_logs()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-1",
            Email = "old@b.com",
            Name = "Old",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var consent = new InMemoryConsentStore();
        consent.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "rectification"
        });
        var userData = new InMemoryUserDataStore(user);
        var handler = new RectifyUserDataHandler(consent, userData);

        var result = await handler.HandleAsync(
            new RectifyUserDataCommand(userId, "new@b.com", "New"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("new@b.com");
        result.Value.Name.Should().Be("New");
        var logs = await userData.GetTreatmentLogsAsync(userId);
        logs.Should().Contain(l => l.Action == "rectify");
    }

    [Fact]
    public async Task RectifyUserDataHandler_fails_without_consent()
    {
        var userData = new InMemoryUserDataStore(new User { Id = Guid.NewGuid() });
        var consent = new InMemoryConsentStore();
        var handler = new RectifyUserDataHandler(consent, userData);

        var result = await handler.HandleAsync(
            new RectifyUserDataCommand(Guid.NewGuid(), "x@x.com", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CONSENT/REQUIRED");
    }

    [Fact]
    public async Task DeleteUserDataHandler_hard_deletes_when_no_payments()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-1",
            Email = "a@b.com",
            Name = "Alice",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var consent = new InMemoryConsentStore();
        consent.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "data-access"
        });
        var userData = new InMemoryUserDataStore(user);
        var handler = new DeleteUserDataHandler(
            consent,
            userData,
            new TestSubscriptionStore(),
            new TestSubscriptionProvider(),
            NullLogger<DeleteUserDataHandler>.Instance);

        var result = await handler.HandleAsync(
            new DeleteUserDataCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var userAfter = await userData.GetByIdAsync(userId);
        userAfter.IsFailure.Should().BeTrue();
        var activeConsent = await consent.GetActiveAsync(userId, "data-access");
        activeConsent.Should().BeNull();
        var logs = await userData.GetTreatmentLogsAsync(userId);
        logs.Should().Contain(l => l.Action == "delete");
    }

    [Fact]
    public async Task DeleteUserDataHandler_fails_without_consent()
    {
        var userData = new InMemoryUserDataStore(new User { Id = Guid.NewGuid() });
        var consent = new InMemoryConsentStore();
        var handler = new DeleteUserDataHandler(
            consent,
            userData,
            new TestSubscriptionStore(),
            new TestSubscriptionProvider(),
            NullLogger<DeleteUserDataHandler>.Instance);

        var result = await handler.HandleAsync(
            new DeleteUserDataCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CONSENT/REQUIRED");
    }

    [Fact]
    public async Task DeleteUserDataHandler_anonymizes_when_user_has_payments()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-1",
            Email = "paid@b.com",
            Name = "Paid",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var consent = new InMemoryConsentStore();
        consent.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "data-access"
        });
        var userData = new InMemoryUserDataStore(user);
        userData.SeedPayment(userId);
        var handler = new DeleteUserDataHandler(
            consent,
            userData,
            new TestSubscriptionStore(),
            new TestSubscriptionProvider(),
            NullLogger<DeleteUserDataHandler>.Instance);

        var result = await handler.HandleAsync(
            new DeleteUserDataCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var userAfter = await userData.GetByIdAsync(userId);
        userAfter.IsSuccess.Should().BeTrue();
        userAfter.Value.Email.Should().Be("[deleted]@anonymized");
        userAfter.Value.Name.Should().Be("[Deleted User]");
        userAfter.Value.ProviderId.Should().Be("redacted");
        var logs = await userData.GetTreatmentLogsAsync(userId);
        logs.Should().Contain(l => l.Action == "anonymize");
    }

    [Fact]
    public async Task DeleteUserDataHandler_preserves_payments_metadata_via_seeded_flag()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-2",
            Email = "paid2@b.com",
            Name = "Paid2",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var consent = new InMemoryConsentStore();
        consent.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "data-access"
        });
        var userData = new InMemoryUserDataStore(user);
        userData.SeedPayment(userId);
        var handler = new DeleteUserDataHandler(
            consent,
            userData,
            new TestSubscriptionStore(),
            new TestSubscriptionProvider(),
            NullLogger<DeleteUserDataHandler>.Instance);

        await handler.HandleAsync(new DeleteUserDataCommand(userId), CancellationToken.None);

        (await userData.HasPaymentsAsync(userId, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task GetUserDataHandler_via_interface_returns_user_and_logs()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-1",
            Email = "a@b.com",
            Name = "Alice",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        IConsentStore consent = new InMemoryConsentStore();
        await consent.AddAsync(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "data-access"
        });
        IUserDataStore userData = new InMemoryUserDataStore(user);
        var handler = new GetUserDataHandler(consent, userData);

        var result = await handler.HandleAsync(
            new GetUserDataQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("a@b.com");
        var logs = await userData.GetTreatmentLogsAsync(userId);
        logs.Should().Contain(l => l.Action == "access");
    }

    [Fact]
    public async Task RectifyUserDataHandler_via_interface_updates_and_logs()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-1",
            Email = "old@b.com",
            Name = "Old",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        IConsentStore consent = new InMemoryConsentStore();
        await consent.AddAsync(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "rectification"
        });
        IUserDataStore userData = new InMemoryUserDataStore(user);
        var handler = new RectifyUserDataHandler(consent, userData);

        var result = await handler.HandleAsync(
            new RectifyUserDataCommand(userId, "new@b.com", "New"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("new@b.com");
        result.Value.Name.Should().Be("New");
        var logs = await userData.GetTreatmentLogsAsync(userId);
        logs.Should().Contain(l => l.Action == "rectify");
    }

    [Fact]
    public async Task DeleteUserDataHandler_via_interface_anonymizes_with_payments()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-1",
            Email = "a@b.com",
            Name = "Alice",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        IConsentStore consent = new InMemoryConsentStore();
        await consent.AddAsync(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "data-access"
        });
        IUserDataStore userData = new InMemoryUserDataStore(user);
        ((InMemoryUserDataStore)userData).SeedPayment(userId);
        var handler = new DeleteUserDataHandler(
            consent,
            userData,
            new TestSubscriptionStore(),
            new TestSubscriptionProvider(),
            NullLogger<DeleteUserDataHandler>.Instance);

        var result = await handler.HandleAsync(
            new DeleteUserDataCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var userAfter = await userData.GetByIdAsync(userId);
        userAfter.IsSuccess.Should().BeTrue();
        userAfter.Value.Email.Should().Be("[deleted]@anonymized");
    }

    [Fact]
    public async Task DeleteUserDataHandler_Anonymize_PreCancelsWompiScheduledCharge_BeforeCascade()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-1",
            Email = "subscriber@b.com",
            Name = "Subscriber",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var consent = new InMemoryConsentStore();
        consent.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "data-access"
        });
        var userData = new InMemoryUserDataStore(user);
        userData.SeedPayment(userId);
        var subscriptionStore = new TestSubscriptionStore();
        var subscriptionProvider = new TestSubscriptionProvider();
        await subscriptionStore.UpsertAsync(
            Subscription.Create(userId, SubscriptionPlan.Standard, "ps_arc_cancel", DateTime.UtcNow));
        var handler = new DeleteUserDataHandler(
            consent,
            userData,
            subscriptionStore,
            subscriptionProvider,
            NullLogger<DeleteUserDataHandler>.Instance);

        var result = await handler.HandleAsync(
            new DeleteUserDataCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscriptionProvider.CancelledPaymentSources.Should().ContainSingle().Which.Should().Be(
            "ps_arc_cancel",
            "ARCO anonymize MUST pre-cancel the Wompi scheduled charge so Wompi stops retrying after the cascade");
        var userAfter = await userData.GetByIdAsync(userId);
        userAfter.IsSuccess.Should().BeTrue();
        userAfter.Value.Email.Should().Be("[deleted]@anonymized");
    }

    [Fact]
    public async Task DeleteUserDataHandler_Anonymize_WithoutSubscription_DoesNotCallProvider()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-1",
            Email = "nosub@b.com",
            Name = "NoSub",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var consent = new InMemoryConsentStore();
        consent.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "data-access"
        });
        var userData = new InMemoryUserDataStore(user);
        userData.SeedPayment(userId);
        var subscriptionStore = new TestSubscriptionStore();
        var subscriptionProvider = new TestSubscriptionProvider();
        var handler = new DeleteUserDataHandler(
            consent,
            userData,
            subscriptionStore,
            subscriptionProvider,
            NullLogger<DeleteUserDataHandler>.Instance);

        var result = await handler.HandleAsync(
            new DeleteUserDataCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscriptionProvider.CancelledPaymentSources.Should().BeEmpty(
            "users without an active subscription must not trigger Wompi cancel");
    }

    [Fact]
    public async Task DeleteUserDataHandler_Anonymize_ContinuesEvenIfWompiCancelFails()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Provider = "google",
            ProviderId = "g-1",
            Email = "wompifail@b.com",
            Name = "WompiFail",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        var consent = new InMemoryConsentStore();
        consent.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PolicyVersion = 1,
            ConsentDate = DateTime.UtcNow,
            Purpose = "data-access"
        });
        var userData = new InMemoryUserDataStore(user);
        userData.SeedPayment(userId);
        var subscriptionStore = new TestSubscriptionStore();
        var subscriptionProvider = new TestSubscriptionProvider
        {
            CancelChargeOverride = _ => throw new InvalidOperationException("Wompi unreachable"),
        };
        await subscriptionStore.UpsertAsync(
            Subscription.Create(userId, SubscriptionPlan.Standard, "ps_will_fail", DateTime.UtcNow));
        var handler = new DeleteUserDataHandler(
            consent,
            userData,
            subscriptionStore,
            subscriptionProvider,
            NullLogger<DeleteUserDataHandler>.Instance);

        var result = await handler.HandleAsync(
            new DeleteUserDataCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "ARCO must not fail if Wompi is unreachable — the FK cascade still removes the row, retries are Wompi's problem");
        var userAfter = await userData.GetByIdAsync(userId);
        userAfter.IsSuccess.Should().BeTrue();
        userAfter.Value.Email.Should().Be("[deleted]@anonymized");
    }
}
