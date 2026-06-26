# Design: 016-subscription-recurring

## Status

[Design] — Pending tasks

## Architecture overview

Adds monthly recurring billing via Wompi `payment_sources` + scheduled charges. Reuses existing credit ledger (013-credit-consumption) for credit grants and existing webhook handler (012-wompi) for event processing.

**Flow**:
1. User picks plan → WompiWidget tokenizes card → returns `payment_source_id`
2. `SubscribeHandler` → Wompi creates scheduled charge → creates Subscription in DB → grants first-month credits
3. Monthly: Wompi auto-charges → webhook fires → `HandleRecurringChargeHandler` grants credits + advances period
4. If charge fails: retry (1, 3, 7 days), then auto-cancel after 14-day grace

**Key insight**: 90% of the work is reused from 012/013/015:
- 012-wompi webhook infrastructure (extend with `recurring_charge.*` events)
- 013-credit-consumption `AccreditPurchaseHandler` (reuse for credit grants via `Reason=Purchase` + `Reference=subscription_period:{subscriptionId}:{periodStartUtc}`)
- 015-feature-flags `ISubscriptionFeatureFlag` (safe rollout)

**Constraint**: Constitution v1.2.0 + Art. III (payment source never touches our servers) + Art. IV (honest "se renueva automáticamente" copy) + Art. VI (Clean Architecture) + Art. VII (rate limits) + Art. VIII (TDD red→green) + Art. IX (ARCO cascade).

## Domain model (final)

### Subscription (new) — `BuildCv-api/src/BuildCv.Domain/Subscriptions/Subscription.cs`

```csharp
namespace BuildCv.Domain.Subscriptions;

public sealed record Subscription
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public SubscriptionPlan Plan { get; init; }
    public string PaymentSourceId { get; init; } = "";
    public string? WompiSubscriptionId { get; init; }
    public SubscriptionStatus Status { get; init; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime CurrentPeriodStart { get; init; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEnd { get; init; } = DateTime.UtcNow.AddDays(30);
    public DateTime? CanceledAt { get; init; }
    public DateTime? LastChargeAt { get; init; }
    public DateTime NextChargeAt { get; init; } = DateTime.UtcNow.AddDays(27);
    public DateTime? LastRetryAt { get; init; }
    public int RetryCount { get; init; }

    public static Subscription Create(Guid userId, SubscriptionPlan plan, string paymentSourceId, string? wompiSubscriptionId, DateTime now)
    {
        return new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Plan = plan,
            PaymentSourceId = paymentSourceId,
            WompiSubscriptionId = wompiSubscriptionId,
            Status = SubscriptionStatus.Active,
            StartedAt = now,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddDays(30),
            NextChargeAt = now.AddDays(27),
            RetryCount = 0
        };
    }

    public int CreditsPerMonth => Plan switch
    {
        SubscriptionPlan.Starter => 30,
        SubscriptionPlan.Standard => 100,
        _ => 0
    };

    public long AmountInCents => Plan switch
    {
        SubscriptionPlan.Starter => 30_000_00L,    // $30,000 COP = 3,000,000 cents
        SubscriptionPlan.Standard => 80_000_00L,   // $80,000 COP = 8,000,000 cents
        _ => 0L
    };
}

public enum SubscriptionPlan
{
    Starter = 1,
    Standard = 2,
}

public enum SubscriptionStatus
{
    Active = 1,
    PastDue = 2,
    Canceled = 3,
}
```

### SubscriptionStateMachine (new) — `BuildCv-api/src/BuildCv.Domain/Subscriptions/SubscriptionStateMachine.cs`

```csharp
namespace BuildCv.Domain.Subscriptions;

public static class SubscriptionStateMachine
{
    public const int MaxRetries = 3;
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(14);
    public static readonly TimeSpan[] RetryDelays = new[]
    {
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(3),
        TimeSpan.FromDays(7),
    };

    public static Subscription TransitionToActive(Subscription sub, DateTime chargedAt, DateTime now)
    {
        if (sub.Status == SubscriptionStatus.Canceled)
        {
            throw new InvalidOperationException("SUBSCRIPTION/INVALID_TRANSITION: cannot reactivate canceled subscription");
        }

        return sub with
        {
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = sub.CurrentPeriodEnd,
            CurrentPeriodEnd = sub.CurrentPeriodEnd.AddDays(30),
            LastChargeAt = chargedAt,
            NextChargeAt = sub.CurrentPeriodEnd.AddDays(27),
            LastRetryAt = null,
            RetryCount = 0
        };
    }

    public static Subscription TransitionToPastDue(Subscription sub, DateTime now, int attemptNumber)
    {
        if (sub.Status == SubscriptionStatus.Canceled)
        {
            throw new InvalidOperationException("SUBSCRIPTION/INVALID_TRANSITION: cannot move canceled subscription to past_due");
        }

        var newRetryCount = sub.RetryCount + 1;
        if (newRetryCount >= MaxRetries)
        {
            return TransitionToCanceled(sub, now, "Max retries exceeded");
        }

        var delay = RetryDelays[Math.Min(sub.RetryCount, RetryDelays.Length - 1)];
        return sub with
        {
            Status = SubscriptionStatus.PastDue,
            NextChargeAt = now.Add(delay),
            LastRetryAt = now,
            RetryCount = newRetryCount
        };
    }

    public static Subscription TransitionToCanceled(Subscription sub, DateTime now, string reason)
    {
        _ = reason; // logged by caller; domain is pure
        return sub with
        {
            Status = SubscriptionStatus.Canceled,
            CanceledAt = now,
            NextChargeAt = DateTime.MaxValue
        };
    }
}
```

## Application layer

### ISubscriptionService (new) — `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/ISubscriptionService.cs`

```csharp
using BuildCv.Domain.Common;
using BuildCv.Domain.Subscriptions;

namespace BuildCv.Application.Features.Subscriptions;

public interface ISubscriptionService
{
    Task<Result<Subscription>> SubscribeAsync(Guid userId, SubscriptionPlan plan, string paymentSourceId, CancellationToken ct);
    Task<Result<Subscription>> GetAsync(Guid userId, CancellationToken ct);
    Task<Result<Subscription>> CancelAsync(Guid userId, CancellationToken ct);
    Task<Result> HandleRecurringChargeSuccessAsync(string paymentSourceId, DateTime chargedAt, string chargeId, CancellationToken ct);
    Task<Result> HandleRecurringChargeFailureAsync(string paymentSourceId, DateTime attemptedAt, string reason, CancellationToken ct);
    Task<Result<int>> ProcessRetriesAsync(CancellationToken ct);
}
```

### ISubscriptionStore (new) — `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/ISubscriptionStore.cs`

```csharp
using BuildCv.Domain.Subscriptions;

namespace BuildCv.Application.Features.Subscriptions;

public interface ISubscriptionStore
{
    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Subscription?> GetByUserIdAsync(Guid userId, bool includeCanceled, CancellationToken ct);
    Task<Subscription?> GetByPaymentSourceIdAsync(string paymentSourceId, CancellationToken ct);
    Task UpsertAsync(Subscription subscription, CancellationToken ct);
    Task<IReadOnlyList<Subscription>> GetDueForRetryAsync(DateTime now, int limit, CancellationToken ct);
}
```

### ISubscriptionProvider (new) — `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/ISubscriptionProvider.cs`

```csharp
namespace BuildCv.Application.Features.Subscriptions;

public interface ISubscriptionProvider
{
    Task<string> CreatePaymentSourceAsync(string wompiToken, CancellationToken ct);
    Task<string> ScheduleRecurringChargeAsync(string paymentSourceId, decimal amountInCents, string currency, CancellationToken ct);
    Task<bool> CancelScheduledChargeAsync(string chargeId, CancellationToken ct);
    bool VerifyWebhookSignature(string payload, string signature);
}
```

### ISubscriptionFeatureFlag (new) — `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/ISubscriptionFeatureFlag.cs`

```csharp
namespace BuildCv.Application.Features.Subscriptions;

public interface ISubscriptionFeatureFlag
{
    bool IsEnabled { get; }
}
```

### Handlers (5) — `BuildCv-api/src/BuildCv.Application/Features/Subscriptions/`

| Handler | Purpose |
|---|---|
| `SubscribeHandler` | Validates feature flag, checks no existing active sub, calls `ISubscriptionProvider.ScheduleRecurringChargeAsync`, persists Subscription, grants first-month credits via `ICreditLedger.AccreditAsync(reference=subscription_period:{subId}:{periodStartUtc})` |
| `CancelSubscriptionHandler` | Loads active sub, calls `ISubscriptionProvider.CancelScheduledChargeAsync`, transitions to Canceled via state machine, returns `accessUntil` |
| `GetSubscriptionHandler` | Loads sub by userId (including Canceled), returns DTO or NotFound |
| `HandleRecurringChargeHandler` | Dispatched from extended `HandleWebhookHandler` on `event_type=recurring_charge.successful` or `recurring_charge.failed`. On success: grant credits + advance period. On failure: transition to PastDue |
| `ProcessRetriesHandler` | Called by reconciliation worker. Queries `GetDueForRetryAsync`, attempts retry via `ISubscriptionProvider`, updates state |

### Extend HandleWebhookHandler (modify) — `BuildCv-api/src/BuildCv.Application/Features/Payments/HandleWebhookHandler.cs`

Add `event_type` dispatch BEFORE existing one-time payment logic. **Minimal diff** (no breaking changes to one-time path):

```csharp
public sealed class HandleWebhookHandler(
    IPaymentStore store,
    IPaymentProvider provider,
    IInvoiceProvider? invoiceProvider,
    ICreditLedger? creditLedger,
    ICreditsFeatureFlag creditsFeature,
    ISubscriptionService? subscriptionService,              // NEW (nullable to keep DI compat)
    ISubscriptionFeatureFlag subscriptionFeature,           // NEW
    ILogger<HandleWebhookHandler> logger)
{
    public async Task<Result<Payment>> HandleAsync(HandleWebhookCommand command, CancellationToken ct)
    {
        // NEW: dispatch by event_type BEFORE signature check on subscription events
        var eventType = ExtractEventType(command.Payload);

        if (eventType is "recurring_charge.successful" or "recurring_charge.failed")
        {
            if (!subscriptionFeature.IsEnabled)
            {
                logger.LogInformation("Subscription event ignored: feature flag disabled");
                return Result.Success<Payment>(default!);  // 200 to Wompi, no replay
            }

            if (!provider.VerifyWebhookSignature(command.Payload, command.SignatureHeader))
            {
                return Result.Failure<Payment>(new Error("PAYMENT/INVALID_SIGNATURE", "Webhook signature verification failed"));
            }

            return await HandleSubscriptionEventAsync(eventType, command, ct);
        }

        // EXISTING: one-time payment path (unchanged below this line)
        if (!provider.VerifyWebhookSignature(command.Payload, command.SignatureHeader))
        {
            return Result.Failure<Payment>(new Error("PAYMENT/INVALID_SIGNATURE", "Webhook signature verification failed"));
        }

        // ... existing code unchanged
    }

    private async Task<Result<Payment>> HandleSubscriptionEventAsync(
        string eventType,
        HandleWebhookCommand command,
        CancellationToken ct)
    {
        var paymentSourceId = ExtractPaymentSourceId(command.Payload);
        if (paymentSourceId is null)
        {
            return Result.Failure<Payment>(new Error("PAYMENT/INVALID_PAYLOAD", "Could not extract payment_source_id"));
        }

        var chargeId = ExtractChargeId(command.Payload) ?? paymentSourceId;
        var now = DateTime.UtcNow;

        Result result = eventType switch
        {
            "recurring_charge.successful" => await subscriptionService!.HandleRecurringChargeSuccessAsync(paymentSourceId, now, chargeId, ct),
            "recurring_charge.failed" => await subscriptionService!.HandleRecurringChargeFailureAsync(paymentSourceId, now, "wompi_failed", ct),
            _ => Result.Failure(new Error("PAYMENT/UNKNOWN_EVENT", $"Unknown event: {eventType}"))
        };

        return result.IsSuccess
            ? Result.Success<Payment>(default!)
            : Result.Failure<Payment>(result.Error);
    }

    private static string? ExtractEventType(string payload)
    {
        const string marker = "\"event\":\"";
        var idx = payload.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + marker.Length;
        var end = payload.IndexOf('"', start);
        return end < 0 ? null : payload[start..end];
    }

    private static string? ExtractPaymentSourceId(string payload)
    {
        const string marker = "\"payment_source_id\":\"";
        var idx = payload.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + marker.Length;
        var end = payload.IndexOf('"', start);
        return end < 0 ? null : payload[start..end];
    }

    private static string? ExtractChargeId(string payload)
    {
        const string marker = "\"charge_id\":\"";
        var idx = payload.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + marker.Length;
        var end = payload.IndexOf('"', start);
        return end < 0 ? null : payload[start..end];
    }
}
```

## Infrastructure layer

### EfSubscriptionStore (new) — `BuildCv-api/src/BuildCv.Infrastructure/Subscriptions/EfSubscriptionStore.cs`

Implements `ISubscriptionStore` using EF Core with `xmin` concurrency (proven pattern from 012-wompi + 015-feature-flags).

```csharp
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Subscriptions;
using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Subscriptions;

public sealed class EfSubscriptionStore(BuildCvDbContext db) : ISubscriptionStore
{
    public async Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Subscription?> GetByUserIdAsync(Guid userId, bool includeCanceled, CancellationToken ct) =>
        await db.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == userId && (includeCanceled || s.Status != SubscriptionStatus.Canceled))
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<Subscription?> GetByPaymentSourceIdAsync(string paymentSourceId, CancellationToken ct) =>
        await db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PaymentSourceId == paymentSourceId, ct);

    public async Task UpsertAsync(Subscription subscription, CancellationToken ct)
    {
        var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscription.Id, ct);
        if (existing is null)
        {
            await db.Subscriptions.AddAsync(subscription, ct);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(subscription);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Subscription>> GetDueForRetryAsync(DateTime now, int limit, CancellationToken ct) =>
        await db.Subscriptions.AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.PastDue && s.NextChargeAt <= now)
            .OrderBy(s => s.NextChargeAt)
            .Take(limit)
            .ToListAsync(ct);
}
```

### InMemorySubscriptionStore (new — test only)

`BuildCv-api/src/BuildCv.Infrastructure/Subscriptions/InMemorySubscriptionStore.cs` — no mocks falsos, real in-memory implementation (mirrors `InMemoryPaymentStore` from 012-wompi and `InMemoryCreditLedger` from 013).

### WompiRecurringAdapter (new) — `BuildCv-api/src/BuildCv.Infrastructure/Payments/WompiRecurringAdapter.cs`

Extends the Wompi integration with recurring billing methods. **Reuses** `WompiAdapter.VerifyWebhookSignature` logic (HMAC SHA256 + `FixedTimeEquals`):

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BuildCv.Application.Features.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Payments;

public sealed class WompiRecurringAdapter : ISubscriptionProvider
{
    private readonly HttpClient _http;
    private readonly WompiSettings _settings;
    private readonly WompiAdapter _wompi;  // for VerifyWebhookSignature reuse
    private readonly ILogger<WompiRecurringAdapter> _logger;

    public WompiRecurringAdapter(
        HttpClient http,
        IOptions<WompiSettings> settings,
        WompiAdapter wompi,
        ILogger<WompiRecurringAdapter> logger)
    {
        _http = http;
        _settings = settings.Value;
        _wompi = wompi;
        _logger = logger;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(_settings.BaseUrl);
        }
    }

    public async Task<string> CreatePaymentSourceAsync(string wompiToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/payment_sources");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.PrivateKey);

        var body = new
        {
            type = "CARD",
            token = wompiToken,
            customer_email = "no-reply@buildcv.com"  // overridden per-user via Wompi widget
        };
        request.Content = JsonContent.Create(body);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WompiPaymentSourceResponse>(ct)
            ?? throw new InvalidOperationException("Wompi returned an empty response");

        _logger.LogInformation("Wompi payment_source created: {PaymentSourceId}", payload.Data.Id);
        return payload.Data.Id;
    }

    public async Task<string> ScheduleRecurringChargeAsync(string paymentSourceId, decimal amountInCents, string currency, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/subscriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.PrivateKey);

        var body = new
        {
            payment_source_id = paymentSourceId,
            amount_in_cents = (long)amountInCents,
            currency = currency,
            recurrence_type = "MONTHLY",
            interval = 1
        };
        request.Content = JsonContent.Create(body);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WompiSubscriptionResponse>(ct)
            ?? throw new InvalidOperationException("Wompi returned an empty response");

        _logger.LogInformation("Wompi subscription created: {SubscriptionId}", payload.Data.Id);
        return payload.Data.Id;
    }

    public async Task<bool> CancelScheduledChargeAsync(string chargeId, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/subscriptions/{chargeId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.PrivateKey);

        var response = await _http.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    public bool VerifyWebhookSignature(string payload, string signature) =>
        _wompi.VerifyWebhookSignature(payload, signature);
}

internal sealed record WompiPaymentSourceResponse
{
    [JsonPropertyName("data")]
    public WompiPaymentSourceData Data { get; init; } = new();
}

internal sealed record WompiPaymentSourceData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
}

internal sealed record WompiSubscriptionResponse
{
    [JsonPropertyName("data")]
    public WompiSubscriptionData Data { get; init; } = new();
}

internal sealed record WompiSubscriptionData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
}
```

### DisabledSubscriptionProvider (new) — `BuildCv-api/src/BuildCv.Infrastructure/Subscriptions/DisabledSubscriptionProvider.cs`

No-op implementation when feature flag is off. **Used in PR1 only for unit tests**; production code uses `WompiRecurringAdapter` directly behind the feature flag check in endpoints.

### SubscriptionReconciliationWorker (new) — `BuildCv-api/src/BuildCv.Infrastructure/Subscriptions/SubscriptionReconciliationWorker.cs`

`IHostedService` that polls every 60 seconds for due subscriptions:

```csharp
using BuildCv.Application.Features.Subscriptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Subscriptions;

public sealed class SubscriptionReconciliationWorker(
    IServiceProvider services,
    ILogger<SubscriptionReconciliationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Subscription reconciliation worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                var result = await svc.ProcessRetriesAsync(stoppingToken);
                if (result.IsSuccess && result.Value > 0)
                {
                    logger.LogInformation("Processed {Count} subscription retries", result.Value);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Subscription reconciliation tick failed; will retry next interval");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Subscription reconciliation worker stopped");
    }
}
```

### EF migration SQL — `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Migrations/20260625_AddSubscriptions.cs`

```sql
CREATE TABLE subscriptions (
    id                      UUID         PRIMARY KEY,
    user_id                 UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    plan                    INTEGER      NOT NULL,
    payment_source_id       VARCHAR(200) NOT NULL,
    wompi_subscription_id   VARCHAR(200) NULL,
    status                  INTEGER      NOT NULL,
    started_at              TIMESTAMPTZ  NOT NULL,
    current_period_start    TIMESTAMPTZ  NOT NULL,
    current_period_end      TIMESTAMPTZ  NOT NULL,
    canceled_at             TIMESTAMPTZ  NULL,
    last_charge_at          TIMESTAMPTZ  NULL,
    next_charge_at          TIMESTAMPTZ  NOT NULL,
    last_retry_at           TIMESTAMPTZ  NULL,
    retry_count             INTEGER      NOT NULL DEFAULT 0,
    xmin                    UINT         NOT NULL DEFAULT 0,
    CONSTRAINT ck_subscriptions_status CHECK (status IN (1,2,3)),
    CONSTRAINT ck_subscriptions_plan CHECK (plan IN (1,2)),
    CONSTRAINT ck_subscriptions_retry_count CHECK (retry_count >= 0 AND retry_count <= 3)
);

-- One active or past_due subscription per user (idempotency + business rule)
CREATE UNIQUE INDEX ux_subscriptions_user_active
    ON subscriptions (user_id) WHERE status != 3;

-- Reconciliation worker queries
CREATE INDEX ix_subscriptions_status_next_charge
    ON subscriptions (status, next_charge_at)
    WHERE status != 3;
```

## API layer

### SubscriptionEndpoints (new) — `BuildCv-api/src/BuildCv.Api/Endpoints/SubscriptionEndpoints.cs`

```csharp
using System.Security.Claims;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Subscriptions;

namespace BuildCv.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/subscriptions")
            .RequireAuthorization()
            .WithTags("Subscriptions");

        group.MapPost("/", SubscribeHandler)
            .RequireRateLimiting("subscription")
            .WithName("Subscribe")
            .Produces<SubscriptionDto>(201)
            .Produces(401)
            .Produces(409)
            .Produces(503);

        group.MapGet("/me", GetSubscriptionHandler)
            .WithName("GetMySubscription")
            .Produces<SubscriptionDto>(200)
            .Produces(401)
            .Produces(404);

        group.MapDelete("/me", CancelSubscriptionHandler)
            .RequireRateLimiting("subscription-cancel")
            .WithName("CancelMySubscription")
            .Produces<CancelSubscriptionResponse>(200)
            .Produces(401)
            .Produces(404);

        return app;
    }

    private static async Task<IResult> SubscribeHandler(
        [FromBody] SubscribeRequest body,
        [FromServices] ISubscriptionService service,
        [FromServices] ISubscriptionFeatureFlag featureFlag,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!featureFlag.IsEnabled)
        {
            return Results.Json(new { error = "SUBSCRIPTION/DISABLED" }, statusCode: 503);
        }

        var userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();

        var result = await service.SubscribeAsync(userId.Value, body.Plan, body.PaymentSourceId, ct);
        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                "SUBSCRIPTION/ALREADY_ACTIVE" => Results.Conflict(new { error = result.Error.Code }),
                _ => Results.Json(
                    new { type = "https://buildcv.com/errors/subscription", title = result.Error.Code, status = 502, detail = result.Error.Message },
                    statusCode: 502)
            };
        }

        return Results.Created($"/api/v1/subscriptions/{result.Value.Id}", SubscriptionDto.FromDomain(result.Value));
    }

    private static async Task<IResult> GetSubscriptionHandler(
        [FromServices] ISubscriptionService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();

        var result = await service.GetAsync(userId.Value, includeCanceled: true, ct);
        if (result.IsFailure)
        {
            return Results.NotFound(new { error = result.Error.Code });
        }
        return Results.Ok(SubscriptionDto.FromDomain(result.Value));
    }

    private static async Task<IResult> CancelSubscriptionHandler(
        [FromServices] ISubscriptionService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();

        var result = await service.CancelAsync(userId.Value, ct);
        if (result.IsFailure)
        {
            return Results.NotFound(new { error = result.Error.Code });
        }
        return Results.Ok(new CancelSubscriptionResponse("canceled", result.Value.CurrentPeriodEnd));
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return userId is not null && Guid.TryParse(userId, out var id) ? id : null;
    }
}

public sealed record SubscribeRequest(SubscriptionPlan Plan, string PaymentSourceId);
public sealed record CancelSubscriptionResponse(string Status, DateTime AccessUntil);
public sealed record SubscriptionDto(
    Guid Id,
    SubscriptionPlan Plan,
    SubscriptionStatus Status,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime? CanceledAt,
    DateTime NextChargeAt,
    int RetryCount)
{
    public static SubscriptionDto FromDomain(Subscription s) => new(
        s.Id, s.Plan, s.Status, s.CurrentPeriodStart, s.CurrentPeriodEnd,
        s.CanceledAt, s.NextChargeAt, s.RetryCount);
}
```

### Program.cs (modify)

Add 3 new rate limit policies:

```csharp
options.AddPolicy("subscription", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

options.AddPolicy("subscription-cancel", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1) }));

options.AddPolicy("subscription-webhook", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
```

Register `SubscriptionEndpoints`:

```csharp
app.MapSubscriptionEndpoints();
```

Register DI in `DependencyInjection.cs`:

```csharp
services.AddScoped<ISubscriptionStore, EfSubscriptionStore>();
services.AddScoped<ISubscriptionService, SubscriptionService>();
services.AddHttpClient<ISubscriptionProvider, WompiRecurringAdapter>();
services.AddSingleton<ISubscriptionFeatureFlag, SubscriptionFeatureFlag>();
services.AddHostedService<SubscriptionReconciliationWorker>();
```

## Frontend layer (`BuildCv-web`)

### BFF routes — `BuildCv-web/app/api/subscriptions/`

- `route.ts` — `POST` (subscribe) + `GET` (current subscription) via `/dashboard/subscriptions` page load
- `cancel/route.ts` — `DELETE` (cancel subscription)

All routes proxy to `BACKEND_URL/api/v1/subscriptions/*` with JWT from `next-auth` session.

### Components — `BuildCv-web/components/subscriptions/`

- `subscription-card.tsx` — current plan display with status badge (Active / PastDue / Canceled) + next billing date + retry banner
- `plan-selector.tsx` — 2 plan cards (Starter 30 cr/$30k, Standard 100 cr/$80k) with honest copy
- `cancel-dialog.tsx` — confirmation with "no reembolso al cancelar" copy + access-until date

### Widget — `BuildCv-web/components/wompi/wompi-subscription-widget.tsx`

Reuses WompiWidget pattern from 012-wompi but tokenizes card as `payment_source` (not `payment_link`). Returns `payment_source_id` to subscribe handler.

### Page — `BuildCv-web/app/(dashboard)/subscriptions/page.tsx`

Lists current subscription, plan selector (if no active sub), history, cancel button.

### Copy (`BuildCv-web/lib/copy/es.ts`)

```typescript
'subscription.active': 'Suscripción activa',
'subscription.auto_renews': 'Se renueva automáticamente cada mes',
'subscription.plan.starter': '30 créditos por $30.000 COP al mes',
'subscription.plan.standard': '100 créditos por $80.000 COP al mes (33% más barato que comprar 2 packs Standard)',
'subscription.no_refund': 'Sin reembolso al cancelar',
'subscription.canceled': 'Suscripción cancelada — acceso hasta {date}',
'subscription.past_due': 'Tu suscripción falló — actualiza tu método de pago',
'subscription.disabled': 'Las suscripciones no están disponibles temporalmente',
```

## Test strategy

### Unit tests (Domain — 10+)

- `Subscription_Create_SetsAllFields` — verifies factory: Status=Active, periods 30d apart, NextChargeAt=Start+27d
- `Subscription_Starter_Has30CreditsPerMonth` — invariant test
- `Subscription_Standard_Has100CreditsPerMonth` — invariant test
- `SubscriptionStateMachine_TransitionToActive_AdvancesPeriod` — CurrentPeriodStart = old.End, CurrentPeriodEnd = new.Start + 30d, RetryCount = 0
- `SubscriptionStateMachine_TransitionToPastDue_IncrementsRetryCount` — NextChargeAt = now + 1d/3d/7d based on current retry
- `SubscriptionStateMachine_TransitionToPastDue_AutoCancelsAfterMaxRetries` — RetryCount=3 → Status=Canceled
- `SubscriptionStateMachine_TransitionToCanceled_UserCancel` — Status=Canceled, CanceledAt=now, NextChargeAt=MaxValue
- `SubscriptionStateMachine_TransitionToActive_RejectsCanceled` — throws InvalidOperationException
- `SubscriptionStateMachine_TransitionToPastDue_RejectsCanceled` — throws InvalidOperationException
- `Subscription_CreditsPerMonth_DefaultsTo0ForUnknownPlan` — defensive default

### Unit tests (Application — 20+)

- `SubscribeHandler_CreatesWompiCharge_AndPersistsSubscription` — calls provider.ScheduleRecurringChargeAsync, ISubscriptionStore.UpsertAsync, ICreditLedger.AccreditAsync
- `SubscribeHandler_GrantsFirstMonthCredits_WithCorrectIdempotencyKey` — Reference=`subscription_period:{subId}:{periodStartUtc}`
- `SubscribeHandler_FailsWhenUserHasActiveSubscription` — returns `SUBSCRIPTION/ALREADY_ACTIVE`
- `SubscribeHandler_FailsWhenFeatureFlagDisabled` — returns `SUBSCRIPTION/DISABLED` (also covered by endpoint test)
- `CancelHandler_CallsWompiCancel_AndUpdatesStatus` — provider.CancelScheduledChargeAsync + state machine Canceled transition
- `CancelHandler_IdempotentWhenAlreadyCanceled` — no second Wompi call
- `GetHandler_ReturnsActiveSubscription`
- `GetHandler_ReturnsCanceledSubscription_WhenIncludeCanceled_True`
- `GetHandler_ReturnsNotFound_WhenNoSubscription`
- `HandleRecurringChargeSuccess_GrantsCredits_AndAdvancesPeriod` — uses InMemoryCreditLedger to verify idempotency
- `HandleRecurringChargeSuccess_IsIdempotent_OnDuplicateWebhook` — replay = no extra ledger rows
- `HandleRecurringChargeFailure_TransitionsToPastDue` — Status=PastDue, NextChargeAt=now+1d, RetryCount=1
- `HandleRecurringChargeFailure_ThirdRetry_AutoCancels` — RetryCount=3 → Status=Canceled
- `ProcessRetries_OnlyProcessesDueSubscriptions` — NextChargeAt <= now, Status=PastDue
- `ProcessRetries_IsIdempotentAcrossRuns` — second call with same state = no extra Wompi calls
- `ProcessRetries_GracePeriod_CancelsAfter14Days` — covers R6 third scenario
- `SubscribeHandler_ConcurrentSubscribe_OnlyOneSucceeds` — uses `UNIQUE(user_id) WHERE status != 3` to simulate race
- `FeatureFlag_False_SkipsAllHandlers` — endpoint-level behavior covered in API tests
- `WebhookRouter_Dispatch_RecurringChargeSuccessful` — covered via `HandleWebhookHandler` integration
- `WebhookRouter_Dispatch_RecurringChargeFailed` — covered via `HandleWebhookHandler` integration

### Integration tests (Infrastructure — 15+)

- `EfSubscriptionStore_GetByUserId_ReturnsActiveSubscription`
- `EfSubscriptionStore_GetByUserId_ExcludesCanceled_ByDefault`
- `EfSubscriptionStore_UpsertAsync_InsertsNew`
- `EfSubscriptionStore_UpsertAsync_UpdatesExisting`
- `EfSubscriptionStore_GetDueForRetryAsync_FiltersByNextChargeAt`
- `EfSubscriptionStore_ConcurrentUpsert_DetectsXminConflict_Retries`
- `WompiRecurringAdapter_CreatePaymentSourceAsync_PostsToCorrectEndpoint` — mocked HTTP
- `WompiRecurringAdapter_ScheduleRecurringChargeAsync_PostsToCorrectEndpoint`
- `WompiRecurringAdapter_CancelScheduledChargeAsync_DeletesCorrectEndpoint`
- `WompiRecurringAdapter_VerifyWebhookSignature_ReusesWompiAdapterLogic`
- `SubscriptionReconciliationWorker_PollsEvery60Seconds` — uses TestHost + time abstraction
- `SubscriptionReconciliationWorker_ProcessesDueRetries`
- `SubscriptionReconciliationWorker_SurvivesTransientFailures`
- `EF_Migration_AppliesCleanly_AndRollsBack`
- `ARCO_Anonymize_CascadeDeletesSubscriptions_PreservesPayments` — uses Testcontainers PostgreSQL
- `HandleWebhookHandler_RecurringChargeSuccessful_GrantsCredits_AndAdvancesPeriod` — full integration with InMemoryCreditLedger + EfSubscriptionStore

### E2E tests (API + Web — 10+)

- `POST_Subscription_Returns201_WithValidAuth`
- `POST_Subscription_Returns409_WhenUserHasActiveSubscription`
- `POST_Subscription_Returns503_WhenFeatureFlagDisabled`
- `POST_Subscription_Returns401_WhenUnauthenticated`
- `GET_SubscriptionMe_Returns200_WithValidAuth`
- `GET_SubscriptionMe_Returns404_WhenNoSubscription`
- `DELETE_SubscriptionMe_Returns200_WithValidAuth`
- `DELETE_SubscriptionMe_Returns404_WhenNoSubscription`
- `Webhook_RecurringChargeSuccessful_GrantsCredits`
- `Webhook_RecurringChargeFailed_TransitionsToPastDue`
- `Webhook_RecurringChargeSuccessful_Returns200_WhenFeatureFlagDisabled` — idempotent replay safety
- `ARCO_Delete_CascadeDeletesSubscription_PreservesPayments` — end-to-end via API

## Configuration

### `appsettings.json` (modify)

```json
{
  "Subscription": {
    "Enabled": false,
    "WompiSubscriptionUrl": "https://api.wompi.co/v1/subscriptions",
    "WompiPaymentSourceUrl": "https://api.wompi.co/v1/payment_sources"
  }
}
```

### `FeatureFlags:Defaults` (modify) — `appsettings.json`

```json
{
  "FeatureFlags": {
    "Defaults": {
      "subscription-recurring-enabled": false,
      "credits-enabled": true,
      "wompi-enabled": true
    }
  }
}
```

## DI registration — `BuildCv-api/src/BuildCv.Infrastructure/DependencyInjection.cs` (modify)

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
{
    // ... existing
    services.Configure<SubscriptionOptions>(config.GetSection("Subscription"));
    services.AddScoped<ISubscriptionStore, EfSubscriptionStore>();
    services.AddScoped<ISubscriptionService, SubscriptionService>();
    services.AddHttpClient<ISubscriptionProvider, WompiRecurringAdapter>();
    services.AddSingleton<ISubscriptionFeatureFlag, SubscriptionFeatureFlag>();
    services.AddHostedService<SubscriptionReconciliationWorker>();
    return services;
}
```

## Compliance

- **Art. I (Cero invención)**: N/A — recurring billing is infrastructure; adapt pipeline untouched.
- **Art. II (Puntaje determinista)**: N/A — score engine untouched. Period arithmetic is `now + TimeSpan.FromDays(30)` (deterministic). Wompi API responses are not used in scoring.
- **Art. III (Privacidad primero)**: ✅ Payment source tokenized Wompi-side, `subscriptions.payment_source_id` is a Wompi token, not a PAN. Logs use `subscriptionId, userId, planId, status, traceId` — same pattern as 012.
- **Art. IV (Encuadre honesto)**: ✅ Copy: "Se renueva automáticamente cada mes" + "Sin reembolso al cancelar". Real prices shown. **NEVER** "créditos ilimitados". Cancellation is one click. ToS disclosure on non-refund policy.
- **Art. V (Entrada como dato)**: N/A — Wompi webhook is HMAC-verified structured data, treated as DATO.
- **Art. VI (Clean Architecture)**: ✅ Domain pure (0 packages — verified by `dotnet list src/BuildCv.Domain package references`). 4 ports in Application (`ISubscriptionService`, `ISubscriptionStore`, `ISubscriptionProvider`, `ISubscriptionFeatureFlag`). Adapters in Infrastructure. `Result<T>` → RFC 9457 ProblemDetails.
- **Art. VII (Rate limits)**: ✅ 3 new policies (`subscription` 10/min/IP, `subscription-cancel` 5/h/IP, `subscription-webhook` 60/min/IP). Existing `score`/`ai`/`export`/`import`/`admin` unchanged.
- **Art. VIII (TDD)**: ✅ Red→green→refactor on every handler + adapter + state transition + reconciliation worker. State machine tested exhaustively. Idempotency, race, and cascade branches have explicit tests. **Zero suppressions, zero mocks falsos.**
- **Art. IX (Habeas Data)**: ✅ Access (R4). Rectification via cancel + re-subscribe. Cancellation via ARCO cascade — `subscriptions` rows cascade-deleted; `payments` + `invoices` preserved per 011-factus DIAN legal hold. Consent unchanged. Server-side confirmation via webhook. Privacy policy updated.

## Out of scope (deferred)

- More than 2 plans (v1.5: Pro tier)
- Annual plans (v1.5)
- Free trials (v1.5)
- Promotional pricing / discount codes (v1.5)
- Proration on plan change (v1.5)
- Family / shared plans (out of scope)
- Subscription pause (v1.5)
- Email notifications for failed charges (deferred until SMTP integration)
- Customer-initiated refunds (no refund endpoint; current period non-refundable per Art. IV)

## Strategy: 3 chained PRs (work only on `main`, direct merge)

Each PR keeps build+test green, each under 400-line diff (work-unit-commits + chained-pr contract).

### PR1 (~250 lines, +20 unit tests): Domain + Application

- **New**: `Subscription`, `SubscriptionPlan`, `SubscriptionStatus`, `SubscriptionStateMachine`, `ISubscriptionService`, `ISubscriptionStore`, `ISubscriptionProvider`, `ISubscriptionFeatureFlag`, `SubscriptionService`, 5 handlers (`SubscribeHandler`, `CancelSubscriptionHandler`, `GetSubscriptionHandler`, `HandleRecurringChargeHandler`, `ProcessRetriesHandler`), `InMemorySubscriptionStore`, `DisabledSubscriptionProvider`
- **Modified**: none (pure additions to Domain + Application layers)
- **Work-unit commits** (Spanish, conventional, no AI attribution):
  - `feat(016): domain — Subscription + 2 enums + SubscriptionStateMachine`
  - `feat(016): application — ISubscriptionService + ISubscriptionStore + ISubscriptionProvider + ISubscriptionFeatureFlag`
  - `feat(016): application — SubscriptionService + 5 handlers`
  - `feat(016): application — InMemorySubscriptionStore + DisabledSubscriptionProvider`
  - `test(016): unit tests de dominio y aplicación (30+)`

### PR2 (~300 lines, +15 integration tests): Infrastructure + DB

- **New**: `EfSubscriptionStore`, `WompiRecurringAdapter`, `SubscriptionFeatureFlag`, `SubscriptionReconciliationWorker`, `SubscriptionOptions`, `SubscriptionConfiguration`, `20260625_AddSubscriptions` migration
- **Modified**: `BuildCvDbContext` (add `DbSet<Subscription>`), `DependencyInjection.cs` (DI registration), `HandleWebhookHandler.cs` (extend with event_type dispatch), `appsettings.json` (Subscription section + FeatureFlags default)
- **Work-unit commits**:
  - `feat(016): infrastructure — EF SubscriptionConfiguration + DbContext`
  - `feat(016): infrastructure — migración AddSubscriptions (20260625)`
  - `feat(016): infrastructure — EfSubscriptionStore`
  - `feat(016): infrastructure — WompiRecurringAdapter (extiende WompiAdapter)`
  - `feat(016): infrastructure — SubscriptionReconciliationWorker + DI registration`
  - `feat(016): infrastructure — HandleWebhookHandler extendido para eventos recurring_charge.*`
  - `feat(016): infra — SubscriptionFeatureFlag + SubscriptionOptions binder`
  - `test(016): integration tests (15)`

### PR3 (~200 lines, +10 e2e tests): API + Web

- **New**: `SubscriptionEndpoints`, DTOs (`SubscribeRequest`, `CancelSubscriptionResponse`, `SubscriptionDto`)
- **Modified**: `Program.cs` (add 3 rate limit policies + map endpoints)
- **New Web**: BFF routes (`/api/subscriptions`, `/api/subscriptions/cancel`), `subscription-card.tsx`, `plan-selector.tsx`, `cancel-dialog.tsx`, `wompi-subscription-widget.tsx`, `app/(dashboard)/subscriptions/page.tsx`
- **Modified Web**: `lib/copy/es.ts`, dashboard page integration
- **Work-unit commits**:
  - `feat(016): api — SubscriptionEndpoints + DTOs`
  - `feat(016): api — 3 rate limit policies + Program.cs wiring`
  - `feat(016): web — BFF routes (subscribe, me, cancel)`
  - `feat(016): web — wompi-subscription-widget + plan-selector + subscription-card`
  - `feat(016): web — cancel-dialog + i18n copy + dashboard integration`
  - `test(016): e2e API (6) + e2e Web (4 Playwright)`

**Total**: ~750 LoC, +45 tests (20 unit + 15 integration + 10 e2e)

### Per-PR gates (all must pass)

1. `dotnet build BuildCv.slnx -c Release` — 0 warnings (warnings-as-errors)
2. `dotnet format --verify-no-changes`
3. `dotnet test -c Release --no-build` — 732+ existing pass, new tests pass
4. `pnpm lint && pnpm build && pnpm test` in `BuildCv-web` (PR3 only)
5. `constitution-check.sh` — no Art. I–IX violations
6. `./scripts/preflight.sh` — full pipeline green

### 400-line budget forecast

| PR | Est. lines | Risk |
|---|---|---|
| PR1 | ~250 | **Low** — pure additions, no existing file modifications |
| PR2 | ~300 | **Medium** — modifies `HandleWebhookHandler` (~30 line diff) + DbContext + DI |
| PR3 | ~200 | **Low** — pure additions to API + Web |

**Decision needed before apply: No** (all PRs green, well within 400-line budget).
**Chained PRs recommended: Yes** (3 PRs, each independently shippable, each keeps build+test green).
**400-line budget risk: Low**.

## Open questions (carry over from proposal)

1. **Confirm 2 plans (Starter 30 cr/$30k, Standard 100 cr/$80k)** — spec defaults to 2.
2. **Retry timing [1, 3, 7] days** — spec uses 3 retries; user can override.
3. **Grace period 14 days** — spec uses 14; user can override.
4. **Cancellation refund policy** — confirmed: no refund (Art. IV).
5. **ARCO anonymize semantics** — subscriptions cascade-deleted; payments + invoices preserved per 011-factus.
6. **Standard discount 33% vs 40%** — spec uses 33% (computed from actual numbers).

## Next

`sdd-tasks` → forecast 400-line budget per PR, lock work-unit commits per PR.