# Design: 013-credit-consumption

## Status

[Design] — ✅ SHIPPED (architecture matches shipped code: `EfCreditLedger` + `InMemoryCreditLedger`, `RequireCreditsFilter`, `CreditEndpoints`, OAuth welcome grant, ARCO anonymize branch)
## Architecture overview

The credit consumption system closes the gap left by 012-wompi: webhooks now credit the user's balance in the same transaction as the payment update and invoice creation, and the `POST /api/adapt` endpoint enforces a 1-credit gate via a `RequireCredits(1)` Minimal API filter.

**Data model**: denormalized `users.credit_balance` (O(1) read for badge) + append-only `credit_ledger_entries` (source of truth, `UNIQUE(user_id, reason, reference)` for idempotency). The DB enforces non-negativity via `CHECK (credit_balance >= 0)` and `CHECK (balance_after >= 0)`; the `xmin` system column (Postgres) provides optimistic concurrency, proven in 012-wompi.

**Failure modes**:
1. Webhook arrives before `Credits:Enabled=true` → no grant (background reconciliation can re-grant)
2. Ledger grant fails after payment approved → webhook returns 200 (Wompi stops retrying), reconciliation retries
3. Adapt consumes credit but LLM fails pre-first-token → refund issued in same transaction
4. ARCO delete with paid invoices → user anonymized, ledger cascade-deleted, payments kept (DIAN legal hold)

**Feature flag**: `Credits:Enabled` (default `false` in production, `true` in dev) — same pattern as `Wompi:Enabled`. When off: payment approval + invoice still work, but no ledger entries are written and `users.credit_balance` is unchanged.

## Domain model (final)

### User (modified) — `BuildCv-api/src/BuildCv.Domain/Auth/User.cs`
```csharp
public sealed record User
{
    // ... existing fields
    public int CreditBalance { get; init; } = 0;  // NEW: denormalized cache
}
```

### CreditLedgerEntry (new) — `BuildCv-api/src/BuildCv.Domain/Credits/CreditLedgerEntry.cs`
```csharp
namespace BuildCv.Domain.Credits;

public sealed record CreditLedgerEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public CreditLedgerReason Reason { get; init; }
    public string Reference { get; init; } = "";
    public int Delta { get; init; }
    public int BalanceAfter { get; init; }
    public string? Metadata { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public enum CreditLedgerReason
{
    Welcome = 1,
    Purchase = 2,
    Consumption = 3,
    Refund = 4,
    ManualAdjustment = 5,
}
```

**Constraints**:
- `UNIQUE(UserId, Reason, Reference)` — idempotency
- `INDEX(UserId, CreatedAt DESC)` — history query
- `CHECK (Delta != 0)` — no zero-delta
- `CHECK (BalanceAfter >= 0)` — defense-in-depth

## Application layer

### Ports — `BuildCv-api/src/BuildCv.Application/Features/Credits/`

```csharp
public interface ICreditLedger
{
    Task<CreditLedgerEntry> AccreditAsync(
        Guid userId,
        CreditLedgerReason reason,
        string reference,
        int delta,
        string? metadata,
        CancellationToken ct);

    Task<CreditLedgerEntry?> FindByReferenceAsync(
        Guid userId,
        CreditLedgerReason reason,
        string reference,
        CancellationToken ct);
}

public interface ICreditConsumptionService
{
    Task<CreditConsumeResult> ConsumeForAdaptAsync(Guid userId, Guid adaptRequestId, CancellationToken ct);
    Task RefundConsumptionAsync(Guid userId, Guid adaptRequestId, CancellationToken ct);
    Task<CreditBalanceView> GetBalanceAsync(Guid userId, CancellationToken ct);
    Task<CreditHistoryPage> GetHistoryAsync(Guid userId, int limit, string? cursor, CancellationToken ct);
}

public sealed record CreditConsumeResult(bool Success, int BalanceAfter, string? ErrorCode);
public sealed record CreditBalanceView(int Balance, int RecentConsumption);
public sealed record CreditHistoryPage(IReadOnlyList<CreditLedgerEntry> Entries, string? NextCursor);

public interface ICreditsFeatureFlag
{
    bool IsEnabled { get; }
}
```

### Handlers (7)
- `AccreditPurchaseHandler` — webhook APPROVED → `Reason=Purchase`, ref `payment:{paymentId}`
- `AccreditWelcomeHandler` — signup → `Reason=Welcome`, ref `welcome:{userId}`, delta=+3
- `ConsumeForAdaptHandler` — adapt request → `Reason=Consumption`, ref `adapt:{adaptRequestId}`, delta=-1
- `RefundConsumptionHandler` — LLM failure pre-first-token → `Reason=Refund`, ref `adapt:{adaptRequestId}:refund`, delta=+1
- `GetCreditBalanceHandler` — query balance + recentConsumption
- `GetCreditHistoryHandler` — paginated history
- `GrantManualCreditHandler` — admin gift → `Reason=ManualAdjustment`, ref `admin:{adminId}:{ticks}`

## Infrastructure layer

### Adapters — `BuildCv-api/src/BuildCv.Infrastructure/Credits/`

#### `EfCreditLedger.cs`
- Implements `ICreditLedger`
- Uses `BuildCvDbContext` (existing pattern from 012-wompi)
- Uses `IDbContextTransaction` (explicit transaction)
- `MaxRetry(3)` on transient EF exceptions (Postgres deadlock retry)
- **Idempotency**: catch `DbUpdateException` with `23505` (unique violation) → call `FindByReferenceAsync` → return existing entry

#### `EfCreditConsumptionService.cs`
- Implements `ICreditConsumptionService`
- Read balance from `users.credit_balance` (denormalized)
- Write consumption via `ICreditLedger.AccreditAsync` with `Reason=Consumption`
- `xmin` concurrency token on `User` (same as `PaymentConfiguration.cs`)
- Pagination cursor: base64(`{createdAt.Ticks}:{id}`)

### EF Core configuration

#### `UserConfiguration.cs` (modified)
```csharp
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // ... existing config
        builder.Property(u => u.CreditBalance)
            .HasColumnName("credit_balance")
            .HasDefaultValue(0)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_users_credit_balance_nonneg",
            "credit_balance >= 0"));
    }
}
```

#### `CreditLedgerEntryConfiguration.cs` (new)
```csharp
public sealed class CreditLedgerEntryConfiguration : IEntityTypeConfiguration<CreditLedgerEntry>
{
    public void Configure(EntityTypeBuilder<CreditLedgerEntry> builder)
    {
        builder.ToTable("credit_ledger_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.Reason).HasColumnName("reason").HasConversion<string>();
        builder.Property(e => e.Reference).HasColumnName("reference").HasMaxLength(200);
        builder.Property(e => e.Delta).HasColumnName("delta");
        builder.Property(e => e.BalanceAfter).HasColumnName("balance_after");
        builder.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.Reason, e.Reference })
            .IsUnique()
            .HasDatabaseName("ux_credit_ledger_user_reason_reference");

        builder.HasIndex(e => new { e.UserId, e.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_credit_ledger_user_created_at");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_credit_ledger_delta_nonzero", "delta != 0");
            t.HasCheckConstraint("ck_credit_ledger_balance_nonneg", "balance_after >= 0");
        });
    }
}
```

#### `BuildCvDbContext.cs` (modified)
```csharp
public sealed class BuildCvDbContext : DbContext
{
    // ... existing DbSets
    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ... existing
        modelBuilder.ApplyConfiguration(new CreditLedgerEntryConfiguration());
    }
}
```

### Migration — `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Migrations/20260624_AddCreditLedger.cs`
```csharp
public partial class AddCreditLedger : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.Sql(@"
            ALTER TABLE users
                ADD COLUMN credit_balance INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE users
                ADD CONSTRAINT ck_users_credit_balance_nonneg
                CHECK (credit_balance >= 0);

            CREATE TABLE credit_ledger_entries (
                id              UUID         PRIMARY KEY,
                user_id         UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                reason          TEXT         NOT NULL,
                reference       VARCHAR(200) NOT NULL,
                delta           INTEGER      NOT NULL,
                balance_after   INTEGER      NOT NULL,
                metadata        JSONB        NULL,
                created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                CONSTRAINT ck_credit_ledger_delta_nonzero CHECK (delta != 0),
                CONSTRAINT ck_credit_ledger_balance_nonneg CHECK (balance_after >= 0)
            );

            CREATE UNIQUE INDEX ux_credit_ledger_user_reason_reference
                ON credit_ledger_entries (user_id, reason, reference);
            CREATE INDEX ix_credit_ledger_user_created_at
                ON credit_ledger_entries (user_id, created_at DESC);
        ");
    }

    protected override void Down(MigrationBuilder mb)
    {
        mb.Sql(@"
            DROP TABLE IF EXISTS credit_ledger_entries;
            ALTER TABLE users DROP CONSTRAINT IF EXISTS ck_users_credit_balance_nonneg;
            ALTER TABLE users DROP COLUMN IF EXISTS credit_balance;
        ");
    }
}
```

## API layer

### `CreditEndpoints.cs` (new) — `BuildCv-api/src/BuildCv.Api/Endpoints/`
```csharp
public static class CreditEndpoints
{
    public static void MapCreditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/credits")
            .RequireAuthorization()
            .WithTags("Credits");

        group.MapGet("/balance", GetBalanceHandler)
            .WithName("GetCreditBalance")
            .Produces<CreditBalanceView>(200)
            .Produces(401);

        group.MapGet("/history", GetHistoryHandler)
            .WithName("GetCreditHistory")
            .Produces<CreditHistoryPage>(200)
            .Produces(401);

        group.MapPost("/gift", GiftHandler)
            .RequireAuthorization(p => p.RequireRole("admin"))
            .WithName("GiftCredits")
            .Produces(200)
            .Produces(401)
            .Produces(403);
    }

    private static async Task<Ok<CreditBalanceView>> GetBalanceHandler(
        [FromServices] ICreditConsumptionService svc,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var balance = await svc.GetBalanceAsync(userId, ct);
        return TypedResults.Ok(balance);
    }

    private static async Task<Ok<CreditHistoryPage>> GetHistoryHandler(
        [FromServices] ICreditConsumptionService svc,
        ClaimsPrincipal user,
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var page = await svc.GetHistoryAsync(userId, limit ?? 50, cursor, ct);
        return TypedResults.Ok(page);
    }

    private static async Task<Ok<object>> GiftHandler(
        [FromBody] GiftRequest body,
        [FromServices] ICreditLedger ledger,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var adminId = user.GetUserId();
        var entry = await ledger.AccreditAsync(
            body.UserId,
            CreditLedgerReason.ManualAdjustment,
            $"admin:{adminId}:{DateTime.UtcNow.Ticks}",
            body.Amount,
            JsonSerializer.Serialize(new { body.Reason }),
            ct);
        return TypedResults.Ok(new { entryId = entry.Id, newBalance = entry.BalanceAfter });
    }
}

public sealed record GiftRequest(Guid UserId, int Amount, string Reason);
```

### `RequireCreditsFilter.cs` (new) — `BuildCv-api/src/BuildCv.Api/Filters/`
```csharp
public sealed class RequireCreditsFilter(int requiredCredits) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var user = ctx.HttpContext.User;
        var userId = user.GetUserId();
        var svc = ctx.HttpContext.RequestServices.GetRequiredService<ICreditConsumptionService>();
        var balance = await svc.GetBalanceAsync(userId, ctx.HttpContext.RequestAborted);

        if (balance.Balance < requiredCredits)
        {
            ctx.HttpContext.Response.Headers["X-Credit-Balance"] = balance.Balance.ToString();
            ctx.HttpContext.Response.Headers["Retry-After"] = "0";
            return Results.Json(
                new { error = "CREDIT/INSUFFICIENT", balance = balance.Balance },
                statusCode: StatusCodes.Status402PaymentRequired);
        }

        return await next(ctx);
    }
}

public static class EndpointConventionBuilderExtensions
{
    public static T RequireCredits<T>(this T builder, int credits) where T : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new RequireCreditsFilter(credits));
        return builder;
    }
}
```

### `AdaptEndpoints.cs` (modified)
```csharp
public static void MapAdaptEndpoints(this IEndpointRouteBuilder app)
{
    app.MapPost("/api/adapt", AdaptHandler)
        .RequireAuthorization()        // NEW: auth gate
        .RequireCredits(1)             // NEW: credit gate
        .WithName("AdaptCv")
        .Produces(200)
        .Produces(401)
        .Produces(402)
        .Produces(502);
}
```

### `HandleWebhookHandler.cs` (modified) — `BuildCv-api/src/BuildCv.Application/Features/Payments/`
```csharp
public sealed class HandleWebhookHandler(
    IPaymentStore store,
    IPaymentProvider provider,
    IInvoiceProvider? invoiceProvider,
    ICreditLedger? creditLedger,             // NEW
    ICreditsFeatureFlag creditsFeature,      // NEW
    ILogger<HandleWebhookHandler> logger)
{
    public async Task<Result<Payment>> HandleAsync(HandleWebhookCommand command, CancellationToken ct)
    {
        // ... existing signature + extraction + payment lookup (unchanged)
        await store.UpdateAsync(updated, ct);

        if (updated.Status == PaymentStatus.Approved)
        {
            if (invoiceProvider is not null)
            {
                try { await CreateInvoiceForPaymentAsync(updated, ct); }
                catch (Exception ex) { logger.LogError(ex, "Invoice creation failed for payment {PaymentId}", updated.Id); }
            }

            // NEW: credit ledger grant (R8)
            if (creditsFeature.IsEnabled && creditLedger is not null)
            {
                try
                {
                    await creditLedger.AccreditAsync(
                        updated.UserId,
                        CreditLedgerReason.Purchase,
                        $"payment:{updated.Id}",
                        updated.Credits,
                        JsonSerializer.Serialize(new { updated.Id, updated.WompiTransactionId }),
                        ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Credit grant failed for payment {PaymentId}", updated.Id);
                }
            }
        }

        return Result.Success(updated);
    }
}
```

## Frontend layer

### BFF routes

#### `BuildCv-web/app/api/credits/balance/route.ts`
```typescript
import { NextResponse } from 'next/server';
import { getServerSession } from 'next-auth';
import { authOptions } from '@/lib/auth';

const BACKEND = process.env.BACKEND_URL ?? 'http://localhost:5080';

export async function GET() {
  const session = await getServerSession(authOptions);
  if (!session) {
    return NextResponse.json({ error: 'AUTH/UNAUTHENTICATED' }, { status: 401 });
  }

  const res = await fetch(`${BACKEND}/api/credits/balance`, {
    headers: { Authorization: `Bearer ${session.accessToken}` },
  });

  if (!res.ok) {
    return NextResponse.json({ error: 'UPSTREAM_FAILURE' }, { status: res.status });
  }

  return NextResponse.json(await res.json());
}
```

#### `BuildCv-web/app/api/credits/history/route.ts`
```typescript
import { NextResponse } from 'next/server';
import { getServerSession } from 'next-auth';
import { authOptions } from '@/lib/auth';

const BACKEND = process.env.BACKEND_URL ?? 'http://localhost:5080';

export async function GET(request: Request) {
  const session = await getServerSession(authOptions);
  if (!session) {
    return NextResponse.json({ error: 'AUTH/UNAUTHENTICATED' }, { status: 401 });
  }

  const { searchParams } = new URL(request.url);
  const limit = searchParams.get('limit') ?? '50';
  const cursor = searchParams.get('cursor') ?? '';

  const url = new URL(`${BACKEND}/api/credits/history`);
  url.searchParams.set('limit', limit);
  if (cursor) url.searchParams.set('cursor', cursor);

  const res = await fetch(url, {
    headers: { Authorization: `Bearer ${session.accessToken}` },
  });

  if (!res.ok) {
    return NextResponse.json({ error: 'UPSTREAM_FAILURE' }, { status: res.status });
  }

  return NextResponse.json(await res.json());
}
```

### Components

#### `BuildCv-web/components/credits/credit-badge.tsx`
```typescript
'use client';

import { useEffect, useState } from 'react';
import { fetchBalance, type CreditBalance } from '@/lib/api/credits';

const LOW_THRESHOLD = Number(process.env.NEXT_PUBLIC_LOW_CREDIT_THRESHOLD ?? 2);

export function CreditBadge({ onBalanceChange }: { onBalanceChange?: (b: number) => void }) {
  const [balance, setBalance] = useState<CreditBalance | null>(null);

  useEffect(() => {
    void fetchBalance().then(setBalance);
    const id = setInterval(() => void fetchBalance().then(setBalance), 30000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    if (balance && onBalanceChange) onBalanceChange(balance.balance);
  }, [balance, onBalanceChange]);

  if (!balance) return <span aria-label="Cargando créditos">—</span>;

  const isLow = balance.balance <= LOW_THRESHOLD;
  const isZero = balance.balance === 0;

  return (
    <span
      aria-live="polite"
      className={isZero ? 'text-red-600 font-bold' : isLow ? 'text-amber-600' : 'text-gray-700'}
    >
      {balance.balance} crédito{balance.balance === 1 ? '' : 's'}
    </span>
  );
}
```

#### `BuildCv-web/components/credits/low-credit-banner.tsx`
```typescript
'use client';

import Link from 'next/link';

export function LowCreditBanner({ balance }: { balance: number }) {
  if (balance > 2) return null;
  return (
    <div role="alert" className="bg-amber-50 border border-amber-300 p-4 rounded">
      <p>Te quedan {balance} crédito{balance === 1 ? '' : 's'}.</p>
      <Link href="/dashboard/credits/buy" className="underline">
        Comprá más para seguir adaptando.
      </Link>
    </div>
  );
}
```

#### `BuildCv-web/lib/api/credits.ts`
```typescript
export type CreditBalance = { balance: number; recentConsumption: number };
export type CreditLedgerEntry = {
  id: string;
  reason: 'Welcome' | 'Purchase' | 'Consumption' | 'Refund' | 'ManualAdjustment';
  delta: number;
  balanceAfter: number;
  reference: string;
  metadata: string | null;
  createdAt: string;
};
export type CreditHistoryPage = { entries: CreditLedgerEntry[]; nextCursor: string | null };

export async function fetchBalance(): Promise<CreditBalance> {
  const res = await fetch('/api/credits/balance', { cache: 'no-store' });
  if (!res.ok) throw new Error(`fetchBalance: ${res.status}`);
  return res.json();
}

export async function fetchHistory(limit = 50, cursor?: string): Promise<CreditHistoryPage> {
  const url = new URL('/api/credits/history', window.location.origin);
  url.searchParams.set('limit', String(limit));
  if (cursor) url.searchParams.set('cursor', cursor);
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error(`fetchHistory: ${res.status}`);
  return res.json();
}
```

## Test strategy

### Unit tests (Domain — Art. VIII required)
- `User.CreditBalance` invariants
- `CreditLedgerEntry` invariants: Delta != 0, BalanceAfter >= 0
- `CreditLedgerReason` enum covers all 5 cases

### Unit tests (Application — 5+ per handler, ~35 total)
- `AccreditPurchaseHandler`: idempotency, first/replay, delta=0, concurrent accredits
- `ConsumeForAdaptHandler`: balance=1→0, balance=0→failure, idempotency
- `RefundConsumptionHandler`: refund after consume, refund with no prior consume, idempotency
- `GetCreditBalanceHandler`: returns balance + recentConsumption
- `GetCreditHistoryHandler`: pagination, cursor encoding

### Integration tests (Infrastructure — ~20)
- EF migration applies cleanly
- `EfCreditLedger` writes correctly
- `EfCreditConsumptionService` reads correctly
- Unique violation caught → returns existing (idempotency)
- `xmin` concurrency conflict → retry 3x
- Cascade delete: user → ledger gone, payments kept
- CHECK constraint violations caught

### End-to-end tests (Api — ~10)
- `POST /api/adapt` with 0 credits → 402
- `POST /api/adapt` with 1 credit → 200, balance=0
- Webhook APPROVED → ledger + balance
- Webhook APPROVED replayed → idempotent
- Webhook APPROVED with `Credits:Enabled=false` → no ledger
- Welcome grant on signup → entry + balance=3
- Welcome grant replayed → idempotent
- ARCO delete → user anonymized, ledger cascade, payments kept
- 402 filter: `X-Credit-Balance` header

### Web e2e tests (Playwright — ~25)
- Sign up → "3 créditos" badge
- Buy package → badge updates
- Adapt 3x → balance=0
- 4th adapt → 402 modal
- Click "Comprar más" → Wompi
- Low-credit banner at balance ≤ 2
- Banner hidden at balance > 2
- History page lists entries
- Pagination works

## Configuration

### `BuildCv-api/src/BuildCv.Api/appsettings.json` (modified)
```json
{
  "Credits": {
    "Enabled": true
  }
}
```

### `BuildCv-web/.env.local.example` (modified)
```
NEXT_PUBLIC_LOW_CREDIT_THRESHOLD=2
```

## DI registration

### `BuildCv-api/src/BuildCv.Infrastructure/DependencyInjection.cs` (modified)
```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
{
    // ... existing
    services.Configure<CreditsOptions>(config.GetSection("Credits"));
    services.AddScoped<ICreditLedger, EfCreditLedger>();
    services.AddScoped<ICreditConsumptionService, EfCreditConsumptionService>();
    services.AddSingleton<ICreditsFeatureFlag, CreditsFeatureFlag>();
    return services;
}
```

## Compliance
- Art. III (Privacy): ledger entries have no CV content, anonymize on ARCO
- Art. IV (Honest framing): "1 crédito = 1 adaptación", no "ilimitado"
- Art. VI (Clean Architecture): domain pure, ports keep IO out
- Art. VII (Rate limits): keep IP 5/h, credit gate is ORTHOGONAL
- Art. VIII (TDD): required for credit math
- Art. IX (Habeas Data): ARCO anonymize, refund pre-first-token, server-side confirmation

## Out of scope (deferred)
- Subscriptions / recurring billing
- User-requested refunds
- Multi-currency
- User-to-user gifting
- Credit expiration
- Migrating existing 012-wompi credits (none exist)

## Open questions (carry over from proposal)
1. ARCO anonymization — does user want lawyer review pre-PR1? (default: no, sufficient for v1)
2. Welcome amount: 3
3. Low-credit threshold: 2
4. 402 UX: modal

## Next
`sdd-tasks` → forecast 400-line budget, recommend 3 chained PRs, lock work-unit commits per PR.
