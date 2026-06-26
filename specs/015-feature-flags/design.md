# Design: 015-feature-flags

## Status

[Design] — Pending tasks

## Architecture overview

Centralized feature flag management with hybrid storage (appsettings defaults + DB overrides), in-memory caching, and admin API. Migrates 3 existing flags (011/012/013) to a unified `IFeatureFlag` service through thin backward-compat adapters — **zero breaking change** to shipped 011/012/013 public contracts.

**Flow (read path)**:

1. App reads flag: `IFeatureFlag.IsEnabledAsync("wompi-enabled")`
2. `CachingFeatureFlagDecorator` checks in-memory cache (60s TTL, configurable via `FeatureFlags:CacheTtlSeconds`)
3. Cache miss → `EfFeatureFlagStore.GetAsync(name)` queries `feature_flags` table
4. DB row found → return `CurrentValue`
5. DB row NOT found → fallback to `FeatureFlagsOptions.Defaults[name]` (appsettings.json)
6. No appsettings entry → throw `FeatureFlagNotFoundException`

**Flow (admin update path)**:

1. Admin calls `PUT /api/v1/admin/feature-flags/{name}` with `{ value, reason }`
2. `UpdateFeatureFlagHandler` validates (`admin` role policy from 009-auth JWT, flag exists in DB or appsettings)
3. `FeatureFlagAdminService.UpdateAsync` opens transaction: updates `feature_flags.current_value`, appends `feature_flag_audit_log` row
4. On commit, `CachingFeatureFlagDecorator.Invalidate(name)` removes cache entry
5. `DbUpdateConcurrencyException` (xmin mismatch) → HTTP 409 `FEATURE_FLAG/CONFLICT`
6. Returns 200 with new DTO

**Storage strategy**: `feature_flags.current_value` (DB) > `FeatureFlags:Defaults:{name}` (appsettings) > throw. Hybrid storage preserves backward compatibility — existing 011/012/013 deploys continue to work even if the DB migration has not run yet (cached defaults from appsettings).

**Performance**: 60s TTL on `IMemoryCache` with explicit `Invalidate(name)` on admin updates — admin changes propagate within milliseconds locally, bounded by TTL on other instances.

## Domain model (final)

### FeatureFlag (new) — `BuildCv-api/src/BuildCv.Domain/FeatureFlags/FeatureFlag.cs`

```csharp
namespace BuildCv.Domain.FeatureFlags;

public sealed record FeatureFlag
{
    public string Name { get; init; } = "";
    public bool DefaultValue { get; init; }
    public bool CurrentValue { get; init; }
    public string? Description { get; init; }
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; init; }

    public static FeatureFlag Create(string name, bool defaultValue, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));
        if (name.Length > 100)
            throw new ArgumentException("Name exceeds 100 chars", nameof(name));

        return new FeatureFlag
        {
            Name = name,
            DefaultValue = defaultValue,
            CurrentValue = defaultValue,
            Description = description,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}
```

### FeatureFlagAuditLog (new) — `BuildCv-api/src/BuildCv.Domain/FeatureFlags/FeatureFlagAuditLog.cs`

```csharp
namespace BuildCv.Domain.FeatureFlags;

public sealed record FeatureFlagAuditLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FlagName { get; init; } = "";
    public bool? OldValue { get; init; }
    public bool NewValue { get; init; }
    public Guid ChangedBy { get; init; }
    public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
    public string? Reason { get; init; }

    public static FeatureFlagAuditLog Create(
        string flagName, bool? oldValue, bool newValue, Guid changedBy, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(flagName))
            throw new ArgumentException("FlagName required", nameof(flagName));
        if (changedBy == Guid.Empty)
            throw new ArgumentException("ChangedBy required", nameof(changedBy));
        if (reason is { Length: > 500 })
            throw new ArgumentException("Reason exceeds 500 chars", nameof(reason));

        return new FeatureFlagAuditLog
        {
            Id = Guid.NewGuid(),
            FlagName = flagName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = changedBy,
            ChangedAt = DateTime.UtcNow,
            Reason = reason,
        };
    }
}
```

### FeatureFlagNotFoundException (new) — `BuildCv-api/src/BuildCv.Domain/FeatureFlags/FeatureFlagNotFoundException.cs`

```csharp
namespace BuildCv.Domain.FeatureFlags;

public sealed class FeatureFlagNotFoundException : Exception
{
    public string FlagName { get; }

    public FeatureFlagNotFoundException(string flagName)
        : base($"Feature flag '{flagName}' is not registered in DB or appsettings.")
    {
        FlagName = flagName;
    }
}
```

**Domain purity**: `dotnet list src/BuildCv.Domain package references` must return 0 packages — verified by CI (existing 010-persistence contract).

## Application layer

### IFeatureFlag (new) — `BuildCv-api/src/BuildCv.Application/Common/IFeatureFlag.cs`

```csharp
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Common;

public interface IFeatureFlag
{
    Task<bool> IsEnabledAsync(string name, CancellationToken ct = default);
    Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default);
}
```

### IFeatureFlagStore (new) — `BuildCv-api/src/BuildCv.Application/Common/IFeatureFlagStore.cs`

```csharp
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Common;

public interface IFeatureFlagStore
{
    Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default);
    Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default);
    Task AppendAuditLogAsync(FeatureFlagAuditLog log, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlagAuditLog>> GetAuditLogAsync(
        string flagName, int limit, string? cursor, CancellationToken ct = default);
}
```

### IFeatureFlagAdminService (new) — `BuildCv-api/src/BuildCv.Application/Common/IFeatureFlagAdminService.cs`

```csharp
using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Common;

public interface IFeatureFlagAdminService
{
    Task<FeatureFlag> UpdateAsync(
        string name, bool newValue, Guid changedBy, string? reason, CancellationToken ct = default);
}
```

### Configuration — `BuildCv-api/src/BuildCv.Application/Common/FeatureFlagsOptions.cs`

```csharp
namespace BuildCv.Application.Common;

public sealed class FeatureFlagsOptions
{
    public int CacheTtlSeconds { get; init; } = 60;
    public Dictionary<string, bool> Defaults { get; init; } = new();
}
```

### Handlers (4) — `BuildCv-api/src/BuildCv.Application/FeatureFlags/`

- `GetFeatureFlagHandler` — wraps `IFeatureFlag.GetAsync` (cache + DB + appsettings fallback)
- `ListFeatureFlagsHandler` — wraps `IFeatureFlag.ListAsync` (DB only, no appsettings auto-seed on read per R7)
- `UpdateFeatureFlagHandler` — calls `IFeatureFlagAdminService.UpdateAsync`, **invalidates cache after commit**, writes audit log
- `GetFeatureFlagAuditLogHandler` — reads `IFeatureFlagStore.GetAuditLogAsync` with keyset pagination (cursor = base64(`{changedAt.Ticks}:{id}`))

**Update flow in detail** (`UpdateFeatureFlagHandler`):

```csharp
public sealed class UpdateFeatureFlagHandler(
    IFeatureFlagAdminService adminService,
    CachingFeatureFlagDecorator cache,
    ILogger<UpdateFeatureFlagHandler> logger)
{
    public async Task<FeatureFlag> HandleAsync(
        string name, bool newValue, Guid changedBy, string? reason, CancellationToken ct = default)
    {
        var updated = await adminService.UpdateAsync(name, newValue, changedBy, reason, ct);
        cache.Invalidate(name);
        logger.LogInformation(
            "Feature flag updated (flagName={FlagName}, oldValue={OldValue}, newValue={NewValue}, changedBy={ChangedBy}, traceId={TraceId})",
            name, updated.DefaultValue != newValue, newValue, changedBy, Activity.Current?.Id);
        return updated;
    }
}
```

## Infrastructure layer

### EfFeatureFlagStore (new) — `BuildCv-api/src/BuildCv.Infrastructure/FeatureFlags/EfFeatureFlagStore.cs`

- Implements `IFeatureFlagStore`
- Uses `BuildCvDbContext` (existing pattern from 013-credit-consumption)
- `MaxRetry(3)` on transient EF exceptions (Postgres deadlock retry)
- `xmin` shadow property on `FeatureFlag` for optimistic concurrency (proven in 012-wompi `PaymentConfiguration.cs`)
- Audit log pagination: decodes base64 cursor → filters by `(ChangedAt < cursor.ChangedAt) OR (ChangedAt = cursor.ChangedAt AND Id < cursor.Id)` (keyset, newest-first)
- Audit log cursor encoded as `Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ticks}:{id}"))`

```csharp
public sealed class EfFeatureFlagStore(
    BuildCvDbContext db,
    ILogger<EfFeatureFlagStore> logger) : IFeatureFlagStore
{
    private static readonly ResiliencePipeline Pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3, BackoffType = DelayBackoffType.Exponential })
        .Build();

    public async Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
    {
        return await Pipeline.ExecuteAsync(async token =>
            await db.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(f => f.Name == name, token), ct);
    }

    public async Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        var existing = await db.FeatureFlags.FirstOrDefaultAsync(f => f.Name == flag.Name, ct);
        if (existing is null)
        {
            await db.FeatureFlags.AddAsync(flag, ct);
        }
        else
        {
            existing.CurrentValue = flag.CurrentValue;
            existing.DefaultValue = flag.DefaultValue;
            existing.Description = flag.Description;
            existing.UpdatedAt = flag.UpdatedAt;
            existing.UpdatedBy = flag.UpdatedBy;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task AppendAuditLogAsync(FeatureFlagAuditLog log, CancellationToken ct = default)
    {
        await db.FeatureFlagAuditLogs.AddAsync(log, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FeatureFlagAuditLog>> GetAuditLogAsync(
        string flagName, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = db.FeatureFlagAuditLogs
            .AsNoTracking()
            .Where(l => l.FlagName == flagName);

        if (TryDecodeCursor(cursor, out var cursorAt, out var cursorId))
        {
            query = query.Where(l => l.ChangedAt < cursorAt || (l.ChangedAt == cursorAt && l.Id.CompareTo(cursorId) < 0));
        }

        return await query
            .OrderByDescending(l => l.ChangedAt).ThenByDescending(l => l.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);
    }

    private static bool TryDecodeCursor(string? cursor, out DateTime at, out Guid id)
    {
        at = default; id = default;
        if (string.IsNullOrEmpty(cursor)) return false;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split(':');
            if (parts.Length != 2) return false;
            at = new DateTime(long.Parse(parts[0]), DateTimeKind.Utc);
            id = Guid.Parse(parts[1]);
            return true;
        }
        catch { return false; }
    }
}
```

### InMemoryFeatureFlagStore (new) — `BuildCv-api/src/BuildCv.Infrastructure/FeatureFlags/InMemoryFeatureFlagStore.cs`

For tests, mirrors 013-credit-consumption's `InMemoryCreditLedger` pattern:

```csharp
public sealed class InMemoryFeatureFlagStore : IFeatureFlagStore
{
    private readonly ConcurrentDictionary<string, FeatureFlag> _flags = new();
    private readonly ConcurrentBag<FeatureFlagAuditLog> _auditLog = new();

    public Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
        => Task.FromResult(_flags.TryGetValue(name, out var f) ? f : null);

    public Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FeatureFlag>>(_flags.Values.OrderBy(f => f.Name).ToList());

    public Task UpsertAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        _flags.AddOrUpdate(flag.Name, flag, (_, _) => flag);
        return Task.CompletedTask;
    }

    public Task AppendAuditLogAsync(FeatureFlagAuditLog log, CancellationToken ct = default)
    {
        _auditLog.Add(log);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FeatureFlagAuditLog>> GetAuditLogAsync(
        string flagName, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = _auditLog.Where(l => l.FlagName == flagName).OrderByDescending(l => l.ChangedAt);
        return Task.FromResult<IReadOnlyList<FeatureFlagAuditLog>>(query.Take(limit).ToList());
    }
}
```

### CachingFeatureFlagDecorator (new) — `BuildCv-api/src/BuildCv.Infrastructure/FeatureFlags/CachingFeatureFlagDecorator.cs`

```csharp
using BuildCv.Application.Common;
using BuildCv.Domain.FeatureFlags;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.FeatureFlags;

public sealed class CachingFeatureFlagDecorator : IFeatureFlag
{
    private readonly IFeatureFlagStore _store;
    private readonly FeatureFlagsOptions _options;
    private readonly ILogger<CachingFeatureFlagDecorator> _logger;
    private readonly IMemoryCache _cache;

    public CachingFeatureFlagDecorator(
        IFeatureFlagStore store,
        IOptions<FeatureFlagsOptions> options,
        ILogger<CachingFeatureFlagDecorator> logger)
        : this(store, options, logger, new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1024,
            ExpirationScanFrequency = TimeSpan.FromSeconds(30),
        }))
    {
    }

    internal CachingFeatureFlagDecorator(
        IFeatureFlagStore store,
        IOptions<FeatureFlagsOptions> options,
        ILogger<CachingFeatureFlagDecorator> logger,
        IMemoryCache cache)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string name, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(name);
        if (_cache.TryGetValue<bool>(cacheKey, out var cached))
            return cached;

        var flag = await _store.GetAsync(name, ct);
        var appsettingsDefault = _options.Defaults.TryGetValue(name, out var d) ? d : (bool?)null;

        var value = flag?.CurrentValue
                    ?? appsettingsDefault
                    ?? throw new FeatureFlagNotFoundException(name);

        _cache.Set(cacheKey, value, TimeSpan.FromSeconds(_options.CacheTtlSeconds));
        return value;
    }

    public Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
        => _store.GetAsync(name, ct);

    public Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default)
        => _store.ListAsync(ct);

    public void Invalidate(string name)
    {
        _cache.Remove(CacheKey(name));
        _logger.LogInformation("Cache invalidated for flag {FlagName}", name);
    }

    private static string CacheKey(string name) => $"feature-flag:{name}";
}
```

**Decorator caching policy**: `IsEnabledAsync` is cached (single boolean lookup per name), `GetAsync` and `ListAsync` are NOT cached (used by admin endpoints, low-frequency, always need fresh DB view).

### FeatureFlagAdminService (new) — `BuildCv-api/src/BuildCv.Infrastructure/FeatureFlags/FeatureFlagAdminService.cs`

```csharp
public sealed class FeatureFlagAdminService(
    IFeatureFlagStore store,
    IOptions<FeatureFlagsOptions> options,
    ILogger<FeatureFlagAdminService> logger) : IFeatureFlagAdminService
{
    public async Task<FeatureFlag> UpdateAsync(
        string name, bool newValue, Guid changedBy, string? reason, CancellationToken ct = default)
    {
        var existing = await store.GetAsync(name, ct)
            ?? throw new FeatureFlagNotFoundException(name);

        var auditLog = FeatureFlagAuditLog.Create(name, existing.CurrentValue, newValue, changedBy, reason);

        var updated = existing with
        {
            CurrentValue = newValue,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = changedBy,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await store.UpsertAsync(updated, ct);
            await store.AppendAuditLogAsync(auditLog, ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        logger.LogInformation(
            "Feature flag committed (flagName={FlagName}, oldValue={OldValue}, newValue={NewValue}, changedBy={ChangedBy}, auditLogId={AuditLogId}, traceId={TraceId})",
            name, existing.CurrentValue, newValue, changedBy, auditLog.Id, Activity.Current?.Id);

        return updated;
    }
}
```

### Backward-compat adapters (3)

These wrap existing flag patterns to use `IFeatureFlag` internally, providing a smooth migration path.

#### FeatureFlagInvoiceAdapter (new) — `BuildCv-api/src/BuildCv.Infrastructure/Invoicing/FeatureFlagInvoiceAdapter.cs`

```csharp
public sealed class FeatureFlagInvoiceAdapter : IInvoiceProvider
{
    private readonly IInvoiceProvider _inner;
    private readonly IFeatureFlag _flags;
    private readonly ILogger<FeatureFlagInvoiceAdapter> _logger;

    public FeatureFlagInvoiceAdapter(
        IFeatureFlag flags,
        IOptions<InvoicingOptions> invoicingOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<FeatureFlagInvoiceAdapter> logger)
    {
        _flags = flags;
        _logger = logger;
        _inner = invoicingOptions.Value.Enabled
            ? new FactusAdapter(httpClientFactory, invoicingOptions)
            : new LocalInvoiceProvider();
    }

    public async Task<Invoice> CreateInvoiceAsync(Invoice invoice, CancellationToken ct)
    {
        var enabled = await _flags.IsEnabledAsync("factus-enabled", ct);
        if (!enabled)
        {
            _logger.LogInformation("Factus disabled by feature flag, using local provider");
            return await new LocalInvoiceProvider().CreateInvoiceAsync(invoice, ct);
        }
        return await _inner.CreateInvoiceAsync(invoice, ct);
    }
}
```

#### FeatureFlagPaymentAdapter (new) — `BuildCv-api/src/BuildCv.Infrastructure/Payments/FeatureFlagPaymentAdapter.cs`

Mirrors the Invoice adapter but wraps `IPaymentProvider` and checks `"wompi-enabled"`. When disabled, returns `DisabledPaymentProvider` behavior (HTTP 404 from endpoints).

#### FeatureFlagCreditsAdapter (new) — `BuildCv-api/src/BuildCv.Infrastructure/Credits/FeatureFlagCreditsAdapter.cs`

Implements `ICreditsFeatureFlag` (the existing 013 port) by delegating to `IFeatureFlag.IsEnabledAsync("credits-enabled")`:

```csharp
public sealed class FeatureFlagCreditsAdapter(IFeatureFlag flags) : ICreditsFeatureFlag
{
    public bool IsEnabled => flags.IsEnabledAsync("credits-enabled").GetAwaiter().GetResult();
}
```

**Why adapter not delete?** The 3 features shipped with their existing public contracts. Removing `ICreditsFeatureFlag` would force 013 to change its public surface (breaking change for any external consumer). The adapter keeps 011/012/013 untouched at the consumer level.

### FeatureFlagMigrationService (new) — `BuildCv-api/src/BuildCv.Infrastructure/FeatureFlags/FeatureFlagMigrationService.cs`

`IHostedService` runs once on startup. Seeds `feature_flags` table from `appsettings.json` defaults (the 3 existing flags). Idempotent via `upsert` on `Name`. Failure logged, NOT fatal (app still works from appsettings fallback).

```csharp
public sealed class FeatureFlagMigrationService(
    IServiceProvider services,
    IOptions<FeatureFlagsOptions> options,
    ILogger<FeatureFlagMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFeatureFlagStore>();

        foreach (var (name, defaultValue) in options.Value.Defaults)
        {
            try
            {
                var existing = await store.GetAsync(name, ct);
                var seed = existing ?? FeatureFlag.Create(name, defaultValue);
                await store.UpsertAsync(seed, ct);
                logger.LogInformation("Feature flag seeded (flagName={FlagName}, defaultValue={DefaultValue})", name, defaultValue);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Feature flag seed failed (flagName={FlagName})", name);
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### EF Core configuration

#### FeatureFlagConfiguration (new) — `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Configurations/FeatureFlagConfiguration.cs`

```csharp
public sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("feature_flags");
        builder.HasKey(f => f.Name);
        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(f => f.DefaultValue).HasColumnName("default_value").IsRequired();
        builder.Property(f => f.CurrentValue).HasColumnName("current_value").IsRequired();
        builder.Property(f => f.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");

        builder.Property<uint>("xmin").IsRowVersion();

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_feature_flags_current_value_not_null",
            "current_value IS NOT NULL"));
    }
}
```

#### FeatureFlagAuditLogConfiguration (new) — `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Configurations/FeatureFlagAuditLogConfiguration.cs`

```csharp
public sealed class FeatureFlagAuditLogConfiguration : IEntityTypeConfiguration<FeatureFlagAuditLog>
{
    public void Configure(EntityTypeBuilder<FeatureFlagAuditLog> builder)
    {
        builder.ToTable("feature_flag_audit_log");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.FlagName).HasColumnName("flag_name").HasMaxLength(100).IsRequired();
        builder.Property(l => l.OldValue).HasColumnName("old_value");
        builder.Property(l => l.NewValue).HasColumnName("new_value").IsRequired();
        builder.Property(l => l.ChangedBy).HasColumnName("changed_by").IsRequired();
        builder.Property(l => l.ChangedAt).HasColumnName("changed_at").IsRequired();
        builder.Property(l => l.Reason).HasColumnName("reason").HasMaxLength(500);

        builder.HasIndex(l => new { l.FlagName, l.ChangedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_feature_flag_audit_log_flag_name_changed_at");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_feature_flag_audit_log_new_value_not_null",
            "new_value IS NOT NULL"));
    }
}
```

#### BuildCvDbContext (modify)

```csharp
public sealed class BuildCvDbContext : DbContext
{
    // ... existing DbSets
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<FeatureFlagAuditLog> FeatureFlagAuditLogs => Set<FeatureFlagAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ... existing
        modelBuilder.ApplyConfiguration(new FeatureFlagConfiguration());
        modelBuilder.ApplyConfiguration(new FeatureFlagAuditLogConfiguration());
    }
}
```

### Migration — `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Migrations/20260625_AddFeatureFlags.cs`

```csharp
public partial class AddFeatureFlags : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.Sql(@"
            CREATE TABLE feature_flags (
                name            VARCHAR(100) PRIMARY KEY,
                default_value   BOOLEAN      NOT NULL,
                current_value   BOOLEAN      NOT NULL,
                description     VARCHAR(500) NULL,
                updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                updated_by      UUID         NULL,
                CONSTRAINT ck_feature_flags_current_value_not_null CHECK (current_value IS NOT NULL)
            );

            CREATE TABLE feature_flag_audit_log (
                id              UUID         PRIMARY KEY,
                flag_name       VARCHAR(100) NOT NULL REFERENCES feature_flags(name) ON DELETE CASCADE,
                old_value       BOOLEAN      NULL,
                new_value       BOOLEAN      NOT NULL,
                changed_by      UUID         NOT NULL,
                changed_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                reason          VARCHAR(500) NULL,
                CONSTRAINT ck_feature_flag_audit_log_new_value_not_null CHECK (new_value IS NOT NULL)
            );

            CREATE INDEX ix_feature_flag_audit_log_flag_name_changed_at
                ON feature_flag_audit_log (flag_name, changed_at DESC);
        ");
    }

    protected override void Down(MigrationBuilder mb)
    {
        mb.Sql(@"
            DROP TABLE IF EXISTS feature_flag_audit_log;
            DROP TABLE IF EXISTS feature_flags;
        ");
    }
}
```

**Seed policy**: No inline INSERT in the migration. Seeding is the responsibility of `FeatureFlagMigrationService` (`IHostedService`) — keeps the migration idempotent across environments (no hardcoded flag names in DDL).

### DI Registration — `BuildCv-api/src/BuildCv.Infrastructure/DependencyInjection.cs` (modify)

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
{
    // ... existing
    services.Configure<FeatureFlagsOptions>(config.GetSection("FeatureFlags"));
    services.AddScoped<IFeatureFlagStore, EfFeatureFlagStore>();
    services.AddScoped<IFeatureFlag, CachingFeatureFlagDecorator>();
    services.AddScoped<IFeatureFlagAdminService, FeatureFlagAdminService>();
    services.AddHostedService<FeatureFlagMigrationService>();

    // Backward-compat adapters
    services.AddSingleton<ICreditsFeatureFlag, FeatureFlagCreditsAdapter>();

    return services;
}
```

## API layer

### RequireAdminPolicy registration — `BuildCv-api/src/BuildCv.Api/Auth/AuthPolicies.cs` (modify)

```csharp
public static class AuthPolicies
{
    public const string Admin = "admin";
    public const string CreditsRequired = "credits-required";
}

public static class AuthExtensions
{
    public static IServiceCollection AddAuthPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.Admin, policy =>
                policy.RequireAuthenticatedUser().RequireRole("admin"));
        });
        return services;
    }
}
```

### Rate limit policy — `BuildCv-api/src/BuildCv.Api/RateLimiting/RateLimitPolicies.cs` (modify)

```csharp
public static class RateLimitPolicies
{
    public const string Admin = "admin";

    public static IServiceCollection AddRateLimitPolicies(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Admin, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
        });
        return services;
    }
}
```

### FeatureFlagAdminEndpoints (new) — `BuildCv-api/src/BuildCv.Api/Endpoints/FeatureFlagAdminEndpoints.cs`

```csharp
public static class FeatureFlagAdminEndpoints
{
    public static void MapFeatureFlagAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/feature-flags")
            .RequireAuthorization(AuthPolicies.Admin)
            .RequireRateLimiting(RateLimitPolicies.Admin)
            .WithTags("FeatureFlagAdmin");

        group.MapGet("/", ListFeatureFlagsHandler)
            .WithName("ListFeatureFlags")
            .Produces<ListFeatureFlagsResponse>(200)
            .Produces(401)
            .Produces(403);

        group.MapGet("/{name}", GetFeatureFlagHandler)
            .WithName("GetFeatureFlag")
            .Produces<FeatureFlagDto>(200)
            .Produces(401)
            .Produces(403)
            .Produces(404);

        group.MapPut("/{name}", UpdateFeatureFlagHandler)
            .WithName("UpdateFeatureFlag")
            .Produces<FeatureFlagDto>(200)
            .Produces(401)
            .Produces(403)
            .Produces(404)
            .Produces(409);

        group.MapGet("/{name}/audit-log", GetAuditLogHandler)
            .WithName("GetFeatureFlagAuditLog")
            .Produces<AuditLogResponse>(200)
            .Produces(401)
            .Produces(403);
    }

    private static async Task<Results<Ok<ListFeatureFlagsResponse>, UnauthorizedHttpResult, ForbidHttpResult>> ListFeatureFlagsHandler(
        [FromServices] IFeatureFlag flags,
        CancellationToken ct)
    {
        var list = await flags.ListAsync(ct);
        return TypedResults.Ok(new ListFeatureFlagsResponse(list.Select(FeatureFlagDto.FromDomain).ToList()));
    }

    private static async Task<Results<Ok<FeatureFlagDto>, NotFound<string>, UnauthorizedHttpResult, ForbidHttpResult>> GetFeatureFlagHandler(
        string name,
        [FromServices] IFeatureFlag flags,
        CancellationToken ct)
    {
        var flag = await flags.GetAsync(name, ct);
        return flag is null
            ? TypedResults.NotFound("FEATURE_FLAG/NOT_FOUND")
            : TypedResults.Ok(FeatureFlagDto.FromDomain(flag));
    }

    private static async Task<Results<Ok<FeatureFlagDto>, NotFound<string>, Conflict<string>, UnauthorizedHttpResult, ForbidHttpResult>> UpdateFeatureFlagHandler(
        string name,
        [FromBody] UpdateFeatureFlagRequest body,
        [FromServices] UpdateFeatureFlagHandler handler,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        try
        {
            var updated = await handler.HandleAsync(name, body.Value, userId, body.Reason, ct);
            return TypedResults.Ok(FeatureFlagDto.FromDomain(updated));
        }
        catch (FeatureFlagNotFoundException)
        {
            return TypedResults.NotFound("FEATURE_FLAG/NOT_FOUND");
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Conflict("FEATURE_FLAG/CONFLICT");
        }
    }

    private static async Task<Results<Ok<AuditLogResponse>, UnauthorizedHttpResult, ForbidHttpResult>> GetAuditLogHandler(
        string name,
        [FromServices] IFeatureFlagStore store,
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        CancellationToken ct)
    {
        var entries = await store.GetAuditLogAsync(name, limit ?? 50, cursor, ct);
        var nextCursor = entries.Count == (limit ?? 50) && entries.Count > 0
            ? BuildCursor(entries[^1])
            : null;
        return TypedResults.Ok(new AuditLogResponse(
            entries.Select(AuditLogDto.FromDomain).ToList(), nextCursor));
    }

    private static string BuildCursor(FeatureFlagAuditLog last)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{last.ChangedAt.Ticks}:{last.Id}"));
}

public sealed record ListFeatureFlagsResponse(List<FeatureFlagDto> Flags);

public sealed record FeatureFlagDto(
    string Name, bool DefaultValue, bool CurrentValue, DateTime UpdatedAt, Guid? UpdatedBy)
{
    public static FeatureFlagDto FromDomain(FeatureFlag f) =>
        new(f.Name, f.DefaultValue, f.CurrentValue, f.UpdatedAt, f.UpdatedBy);
}

public sealed record UpdateFeatureFlagRequest(bool Value, string? Reason);

public sealed record AuditLogResponse(List<AuditLogDto> Entries, string? NextCursor);

public sealed record AuditLogDto(
    Guid Id, string FlagName, bool? OldValue, bool NewValue,
    Guid ChangedBy, DateTime ChangedAt, string? Reason)
{
    public static AuditLogDto FromDomain(FeatureFlagAuditLog l) =>
        new(l.Id, l.FlagName, l.OldValue, l.NewValue, l.ChangedBy, l.ChangedAt, l.Reason);
}
```

### Program.cs (modify) — add auth policy + rate limit + endpoint mapping

```csharp
// In service registration
builder.Services.AddAuthPolicies();
builder.Services.AddRateLimitPolicies();

// After app.Build()
app.MapFeatureFlagAdminEndpoints();
```

### Existing 011/012/013 call sites (modified — adapters wired in DI)

| Existing service | Old registration | New registration |
|---|---|---|
| `IInvoiceProvider` (011-factus) | null-conditional DI registration | `AddScoped<IInvoiceProvider, FeatureFlagInvoiceAdapter>()` |
| `IPaymentProvider` (012-wompi) | Active/Disabled split registration | `AddScoped<IPaymentProvider, FeatureFlagPaymentAdapter>()` |
| `ICreditsFeatureFlag` (013-credits) | `AddSingleton<ICreditsFeatureFlag, CreditsFeatureFlag>()` (already in DI) | `AddSingleton<ICreditsFeatureFlag, FeatureFlagCreditsAdapter>()` |

**No call-site code changes** in 011/012/013 — adapters keep the same public contract. Existing test suites rerun unchanged to prove zero regression.

## Test strategy

### Unit tests (Domain — 5+)

- `FeatureFlag_Create_RequiresName` — empty/whitespace name → `ArgumentException`
- `FeatureFlag_Create_NameExceedsHundredChars_Throws`
- `FeatureFlag_Create_DefaultsCurrentValueToDefaultValue`
- `FeatureFlagAuditLog_Create_RequiresFlagNameAndChangedBy`
- `FeatureFlagAuditLog_Create_ReasonExceeds500Chars_Throws`
- `FeatureFlagAuditLog_Create_DefaultsChangedAtToUtcNow`
- `FeatureFlagNotFoundException_IncludesFlagName`

### Unit tests (Application — 20+)

- `IsEnabledAsync_ReturnsCurrentValue_WhenFlagInDb`
- `IsEnabledAsync_ReturnsDefaultValue_WhenFlagNotInDb_AndInAppsettings`
- `IsEnabledAsync_ThrowsFeatureFlagNotFound_WhenNoDbOrConfig`
- `IsEnabledAsync_AppliesDbOverride_OverAppsettingsDefault`
- `GetAsync_ReturnsFlag_WhenExists`
- `GetAsync_ReturnsNull_WhenNotExists`
- `ListAsync_ReturnsAllFlagsFromDb`
- `UpdateAsync_UpdatesCurrentValue_AndWritesAuditLog`
- `UpdateAsync_ThrowsFeatureFlagNotFound_WhenFlagNotRegistered`
- `UpdateAsync_ThrowsDbUpdateConcurrency_OnXminMismatch`
- `GetAuditLogAsync_ReturnsRecentEntries_NewestFirst`
- `GetAuditLogAsync_PaginatesCorrectly_WithCursor`
- `GetAuditLogAsync_ClampsLimitTo200`
- `CachingFeatureFlagDecorator_CachesResult_ForTtl`
- `CachingFeatureFlagDecorator_Invalidate_RemovesFromCache`
- `CachingFeatureFlagDecorator_TtlExpires_RefreshesFromDb`
- `CachingFeatureFlagDecorator_FallsBackToAppsettings_WhenDbReturnsNull`
- `FeatureFlagMigrationService_SeedsFlagsFromAppsettings_Idempotently`
- `FeatureFlagInvoiceAdapter_DelegatesToFactus_WhenFlagEnabled`
- `FeatureFlagInvoiceAdapter_FallsBackToLocal_WhenFlagDisabled`
- `FeatureFlagPaymentAdapter_DelegatesToWompi_WhenFlagEnabled`
- `FeatureFlagPaymentAdapter_FallsBackToDisabled_WhenFlagDisabled`
- `FeatureFlagCreditsAdapter_DelegatesToFeatureFlag`

### Integration tests (Infrastructure — 15+, uses Testcontainers PostgreSQL)

- `EfFeatureFlagStore_UpsertAsync_PersistsToDb`
- `EfFeatureFlagStore_GetAsync_ReturnsNullForUnknownFlag`
- `EfFeatureFlagStore_AppendAuditLogAsync_AppendsRow`
- `EfFeatureFlagStore_GetAuditLogAsync_PaginatesCorrectly`
- `EfFeatureFlagStore_GetAuditLogAsync_DecodesCursorCorrectly`
- `EfFeatureFlagStore_GetAuditLogAsync_ClampsLimitTo200`
- `CachingFeatureFlagDecorator_IsEnabledAsync_CachesResult_AcrossCalls`
- `CachingFeatureFlagDecorator_Invalidate_RemovesCacheEntry`
- `CachingFeatureFlagDecorator_TtlExpires_RefreshesFromDb`
- `FeatureFlagMigrationService_SeedsThreeRowsFromAppsettings`
- `FeatureFlagMigrationService_IdempotentOnRerun`
- `EfFeatureFlagStore_OptimisticConcurrency_OnXminMismatch`
- `Migration_20260625_AddFeatureFlags_AppliesAndRollsBackCleanly`
- `FeatureFlagInvoiceAdapter_IntegrationWithFactus_WhenEnabled`
- `FeatureFlagPaymentAdapter_IntegrationWithDisabled_WhenFlagDisabled`

### E2E tests (API — 10+, uses `WebApplicationFactory` + Testcontainers PostgreSQL)

- `PUT_FeatureFlag_Returns200_WithValidAdminAuth`
- `PUT_FeatureFlag_Returns401_WithoutAuth`
- `PUT_FeatureFlag_Returns403_ForNonAdmin`
- `PUT_FeatureFlag_Returns404_ForUnknownFlag`
- `PUT_FeatureFlag_Returns409_OnConcurrentUpdate`
- `PUT_FeatureFlag_RateLimited_After30PerMinute`
- `PUT_FeatureFlag_WritesAuditLogRow_InSameTransaction`
- `PUT_FeatureFlag_InvalidatesCache_ImmediatelyAfterCommit`
- `GET_FeatureFlag_Returns200_WithCurrentValue`
- `GET_FeatureFlagList_Returns200_WithAllFlags`
- `GET_AuditLog_Returns200_WithRecentEntries`
- `GET_AuditLog_Pagination_ReturnsNextCursor`
- `011_Factus_RerunsUnchanged_AfterAdapterWired`
- `012_Wompi_RerunsUnchanged_AfterAdapterWired`
- `013_Credits_RerunsUnchanged_AfterAdapterWired`

## Configuration

### appsettings.json (modify) — adds `FeatureFlags` section

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "Cors": { "AllowedOrigins": [] },
  "Ai": { "ApiKey": "" },
  "Wompi": { "Enabled": false, "Environment": "sandbox", "PublicKey": "", "PrivateKey": "", "WebhookSecret": "" },
  "Credits": { "Enabled": true },
  "FeatureFlags": {
    "CacheTtlSeconds": 60,
    "Defaults": {
      "factus-enabled": false,
      "wompi-enabled": true,
      "credits-enabled": true
    }
  },
  "NextAuth": {
    "SigningKey": "replace-with-shared-secret-min-32-chars-must-match-NEXTAUTH_SECRET",
    "Issuer": "buildcv-web",
    "Audience": "buildcv-api"
  }
}
```

**Backward compatibility note**: existing `Wompi:Enabled` and `Credits:Enabled` keys are kept — adapters read `FeatureFlags:Defaults:*` instead. Old keys become documentation for the migration period.

## Compliance

- **Art. I (Cero invención)**: N/A — flag infrastructure, no CV/job content.
- **Art. II (Puntaje determinista)**: N/A — score engine untouched. `IFeatureFlag.IsEnabledAsync` is process-stable within the cache TTL (single boolean return per call).
- **Art. III (Privacidad primero)**: ✅ `FeatureFlagAuditLog.ChangedBy` is a `Guid` user id — never email, name, IP, or CV/job content. Logs use the 011/012/013 pattern: `flagName, oldValue, newValue, changedBy, traceId`.
- **Art. IV (Encuadre honesto)**: ✅ admin API returns raw boolean + description. No "advanced AI" copy.
- **Art. V (Entrada como dato)**: N/A — flag names are config-time constants, not user input.
- **Art. VI (Clean Architecture)**: ✅ Domain pure (verified by `dotnet list src/BuildCv.Domain package references` returning 0). Ports (`IFeatureFlag`, `IFeatureFlagStore`, `IFeatureFlagAdminService`) in `BuildCv.Application/Common/`. Adapters in `BuildCv.Infrastructure/FeatureFlags/`. Backward-compat adapters keep 011/012/013 contracts unchanged.
- **Art. VII (Rate limits)**: ✅ new `"admin"` policy: 30/min/IP for `/api/v1/admin/feature-flags/*`. Lower than `score` (60/min) and `ai` (5/h) intentionally — admin endpoints are sensitive. `score`/`ai`/`export`/`import` policies unchanged.
- **Art. VIII (TDD)**: ✅ red-green-refactor on every handler, decorator, and adapter. Adapter tests rerun 011/012/013 suites unchanged to prove no regression.
- **Art. IX (Habeas Data)**: ✅ **Access:** R7 lists all flags. **Rectification:** R4 updates flag values + writes audit row. **Cancellation:** N/A (operational config). **Consent:** N/A. **Audit:** R5 reads audit log; every change is recorded with `changed_by`, `old_value`, `new_value`, `changed_at`, `reason` — compliance evidence for kill-switches.

## Out of scope (deferred)

- Admin dashboard UI (v1.5)
- Per-user flags / targeting (v1.5)
- A/B testing framework (v1.5)
- Time-based rollout (`enable_at` / `disable_at`) (v1.5)
- Multi-tenant flags (single-tenant)
- Flag analytics / telemetry
- Migration of 012's `Wompi:Environment` (3-state, stays as `IOptions<WompiOptions>`)
- Audit log retention policy (indefinite for v1; cron deferred to v1.5)

## Strategy: 3 chained PRs

**Pattern mirrors 013-credit-consumption**: each PR keeps `dotnet build + dotnet format + dotnet test + constitution-check` green, each merges directly to `main` (no stacked branches).

### PR1 (~200 lines, +20 unit tests): Domain + Application

**New files**:
- `BuildCv.Domain/FeatureFlags/FeatureFlag.cs`
- `BuildCv.Domain/FeatureFlags/FeatureFlagAuditLog.cs`
- `BuildCv.Domain/FeatureFlags/FeatureFlagNotFoundException.cs`
- `BuildCv.Application/Common/IFeatureFlag.cs`
- `BuildCv.Application/Common/IFeatureFlagStore.cs`
- `BuildCv.Application/Common/IFeatureFlagAdminService.cs`
- `BuildCv.Application/Common/FeatureFlagsOptions.cs`
- `BuildCv.Application/FeatureFlags/GetFeatureFlagHandler.cs`
- `BuildCv.Application/FeatureFlags/ListFeatureFlagsHandler.cs`
- `BuildCv.Application/FeatureFlags/UpdateFeatureFlagHandler.cs`
- `BuildCv.Application/FeatureFlags/GetFeatureFlagAuditLogHandler.cs`
- `tests/BuildCv.Domain.Tests/FeatureFlags/FeatureFlagTests.cs`
- `tests/BuildCv.Domain.Tests/FeatureFlags/FeatureFlagAuditLogTests.cs`
- `tests/BuildCv.Application.Tests/FeatureFlags/FeatureFlagHandlersTests.cs`

**Work-unit commits** (red → green → refactor):
1. `test(015): tests rojos de dominio (FeatureFlag, FeatureFlagAuditLog, Exception)` — 5 tests
2. `feat(015): dominio — FeatureFlag + FeatureFlagAuditLog + Exception`
3. `test(015): tests rojos de puertos (IFeatureFlag, IFeatureFlagStore, IFeatureFlagAdminService)`
4. `feat(015): application — IFeatureFlag + IFeatureFlagStore + IFeatureFlagAdminService + Options`
5. `test(015): tests rojos de handlers (Get, List, Update, AuditLog)`
6. `feat(015): application — 4 handlers`
7. `chore(015): refactor + verificación constitution-check`

### PR2 (~250 lines, +15 integration tests): Infrastructure + DB

**New files**:
- `BuildCv.Infrastructure/FeatureFlags/EfFeatureFlagStore.cs`
- `BuildCv.Infrastructure/FeatureFlags/InMemoryFeatureFlagStore.cs`
- `BuildCv.Infrastructure/FeatureFlags/CachingFeatureFlagDecorator.cs`
- `BuildCv.Infrastructure/FeatureFlags/FeatureFlagAdminService.cs`
- `BuildCv.Infrastructure/FeatureFlags/FeatureFlagMigrationService.cs`
- `BuildCv.Infrastructure/Persistence/Configurations/FeatureFlagConfiguration.cs`
- `BuildCv.Infrastructure/Persistence/Configurations/FeatureFlagAuditLogConfiguration.cs`
- `BuildCv.Infrastructure/Persistence/Migrations/20260625_AddFeatureFlags.cs`
- `BuildCv.Infrastructure/Invoicing/FeatureFlagInvoiceAdapter.cs`
- `BuildCv.Infrastructure/Payments/FeatureFlagPaymentAdapter.cs`
- `BuildCv.Infrastructure/Credits/FeatureFlagCreditsAdapter.cs`
- `tests/BuildCv.Infrastructure.Tests/FeatureFlags/EfFeatureFlagStoreTests.cs` (Testcontainers PostgreSQL)
- `tests/BuildCv.Infrastructure.Tests/FeatureFlags/CachingFeatureFlagDecoratorTests.cs`
- `tests/BuildCv.Infrastructure.Tests/FeatureFlags/FeatureFlagMigrationServiceTests.cs`

**Modified files**:
- `BuildCv.Infrastructure/Persistence/BuildCvDbContext.cs` — add DbSets + ApplyConfiguration
- `BuildCv.Infrastructure/DependencyInjection.cs` — register IFeatureFlag, IFeatureFlagStore, IFeatureFlagAdminService, IHostedService, ICreditsFeatureFlag adapter
- `BuildCv.Api/appsettings.json` — add `FeatureFlags` section

**Work-unit commits**:
1. `test(015): tests rojos EF configuration (DbContext + 2 configurations)`
2. `feat(015): infrastructure — EF configuration + DbContext`
3. `feat(015): infrastructure — migración AddFeatureFlags (20260625)`
4. `test(015): tests rojos EfFeatureFlagStore (CRUD + paginación + concurrencia)`
5. `feat(015): infrastructure — EfFeatureFlagStore + InMemoryFeatureFlagStore`
6. `test(015): tests rojos CachingFeatureFlagDecorator (TTL + invalidación + fallback)`
7. `feat(015): infrastructure — CachingFeatureFlagDecorator + FeatureFlagAdminService`
8. `feat(015): infrastructure — FeatureFlagMigrationService (IHostedService)`
9. `test(015): tests rojos adapters (Invoice, Payment, Credits)`
10. `feat(015): infrastructure — 3 adapters de backward-compat`
11. `chore(015): DI registration + appsettings + preflight verde`

### PR3 (~150 lines, +10 e2e tests): API

**New files**:
- `BuildCv.Api/Endpoints/FeatureFlagAdminEndpoints.cs`
- `BuildCv.Api/Auth/AuthPolicies.cs`
- `BuildCv.Api/RateLimiting/RateLimitPolicies.cs`
- `tests/BuildCv.Api.Tests/Endpoints/FeatureFlagAdminEndpointsTests.cs`

**Modified files**:
- `BuildCv.Api/Program.cs` — add auth policy + rate limit + endpoint mapping
- `BuildCv.Api/Endpoints/InvoiceEndpoints.cs` (011-factus) — re-wire to `FeatureFlagInvoiceAdapter`
- `BuildCv.Api/Endpoints/PaymentEndpoints.cs` (012-wompi) — re-wire to `FeatureFlagPaymentAdapter`

**Work-unit commits**:
1. `test(015): tests rojos admin endpoints (GET list, GET single, PUT, GET audit-log, 401, 403, 404, 409, 429)`
2. `feat(015): api — FeatureFlagAdminEndpoints + DTOs + AuthPolicies + RateLimitPolicies`
3. `feat(015): api — wire FeatureFlagInvoiceAdapter (011) + FeatureFlagPaymentAdapter (012) en DI`
4. `chore(015): re-run 011/012/013 e2e suites para probar zero regression`
5. `chore(015): preflight verde + constitution-check + git tag 015-feature-flags-v1.0`

**Per-PR gates** (must all pass before merge):
1. `dotnet build BuildCv.slnx -c Release` — 0 warnings (warnings-as-errors)
2. `dotnet format --verify-no-changes`
3. `dotnet test -c Release --no-build` — 451+ existing pass + new tests pass + 011/012/013 suites rerun unchanged
4. `constitution-check.sh` — no Art. I-IX violations
5. `./scripts/preflight.sh` — full pipeline green
6. `dotnet list src/BuildCv.Domain package references` — 0 packages (Domain purity invariant)

## Next

`sdd-tasks` → forecast 400-line budget per PR, lock work-unit commits per PR.
