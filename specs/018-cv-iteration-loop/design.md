# Design: 018-cv-iteration-loop — Best-of-N CV Adaptation with Probability Warning

## Status

[Design] — Pending tasks (locked architecture; ready for `sdd-tasks`)

## Architecture overview

**Best-of-N iteration loop** that runs N adaptations of a CV against a vacancy, validates each via the existing `CrossEntityValidator` (Art. I gate), scores each via the existing `ScoreCvHandler` (Art. II), and returns the best result that passed Art. I. When the best score is below the configured threshold, a `ProbabilityWarning` is attached with 3 generic, honest recommended actions (Art. IV — never invent entities).

**Zero modifications** to 003-adapt-ia, 002-score-engine, or 013-credit-consumption. All new code is additive:

- **Domain (4 new types, 1 enum)**: `IterationRequest`, `IterationStep`, `IterationResult`, `ProbabilityWarning`, `RequestStatus`.
- **Application (2 ports, 2 handlers, 1 service)**: `IIterationService`, `IIterationStore`, `IterateAdaptationHandler`, `GetIterationResultHandler`, `IterationService`.
- **Infrastructure (2 adapters, 1 worker, 1 EF migration, 1 configuration pair, 1 ICreditConsumptionService extension)**: `EfIterationStore`, `InMemoryIterationStore`, `IterationCleanupWorker`, migration `20260625HHMMSS_AddIterationResults`, `IterationRequestConfiguration`, `IterationResultConfiguration`, `ConsumeForIterationAsync` method on `ICreditConsumptionService`.
- **API (1 endpoint class, 1 rate-limit policy)**: `IterationEndpoints`, `RateLimiting.IteratePolicy`.
- **Web (4 components, 1 BFF pair, 1 page, 1 doc)**: `IterationControlPanel`, `IterationResultCard`, `IterationStepList`, `ProbabilityWarning`, BFF POST + GET routes, `/analizar/iterate` page, `docs/integrations/cv-generator.md`.

**Iteration loop flow (orchestration pattern)**:

```
POST /api/v1/adapt/iterate
  │
  ├── 1. Auth (JWT, Art. VII)
  ├── 2. Validate request (iterationCount ∈ [1,20], threshold ∈ [0,100])
  ├── 3. Rate-limit gate ("iterate" policy: 10/h per IP)
  ├── 4. Credit gate (.RequireCredits(N))         ── 013 ConsumeForAdaptHandler pattern
  ├── 5. IterateAdaptationHandler.HandleAsync()
  │     │
  │     ├── a. ICreditConsumptionService.ConsumeForIterationAsync(N credits, atomic, debit-before-loop)
  │     ├── b. Save IterationRequest(Status=Running)
  │     ├── c. For i ∈ [1..N]:
  │     │     ├── linked CTS with per-iteration 30s timeout + total 5min cap
  │     │     ├── AdaptCvHandler.HandleAsync(cvText, jobText, seed=$"{RequestId}:{i}", ct)
  │     │     ├── CrossEntityValidator.Validate(...) — gate Art. I
  │     │     │     └── Severity != Critical ⇒ PassedArtI=true
  │     │     ├── IF PassedArtI: ScoreCvHandler.Handle(adaptedCv, jobText)
  │     │     └── Record IterationStep { IterationNumber, AdaptedCvText, Score, Severity, PassedArtI, Duration, CompletedAt }
  │     │     └── IF PassedArtI AND score > bestStep.Score: bestStep = step
  │     ├── d. Status = Completed | Failed | TimedOut
  │     ├── e. ProbabilityWarning = IF bestStep != null AND bestStep.Score < threshold THEN build
  │     ├── f. IterationResult + persist to IIterationStore.SaveAsync (TTL 24h)
  │     └── g. Return IterationResult
  └── 6. 200 OK with IterationResultDto (synchronous) | 202 Accepted (wait=false)
```

**Integration guarantees** (reused unchanged):
- `AdaptCvHandler.HandleAsync(cvText, jobText, seed, ct)` — call signature extended to accept optional `iterationSeed` parameter; existing callers unaffected (default null).
- `ScoreCvHandler.Handle(command)` — unchanged.
- `CrossEntityValidator.Validate(...)` — unchanged; invoked directly per iteration.
- `ICreditConsumptionService` — additive `ConsumeForIterationAsync` (does NOT modify `ConsumeForAdaptAsync`).
- `RequireCreditsFilter` — reused as-is (configured with `iterationCount`).

## Domain model (final)

### `IterationRequest` — `BuildCv-api/src/BuildCv.Domain/Iterations/IterationRequest.cs`

```csharp
namespace BuildCv.Domain.Iterations;

public sealed record IterationRequest
{
    public Guid RequestId { get; init; }
    public Guid UserId { get; init; }
    public string CvText { get; init; } = "";
    public string JobText { get; init; } = "";
    public int IterationCount { get; init; }
    public int ProbabilityThreshold { get; init; }
    public DateTime CreatedAt { get; init; }
    public RequestStatus Status { get; init; }

    public static IterationRequest Create(
        Guid userId,
        string cvText,
        string jobText,
        int iterationCount,
        int probabilityThreshold,
        DateTime now)
    {
        if (iterationCount < 1 || iterationCount > 20)
            throw new ArgumentException("IterationCount must be in [1, 20].", nameof(iterationCount));
        if (probabilityThreshold < 0 || probabilityThreshold > 100)
            throw new ArgumentException("ProbabilityThreshold must be in [0, 100].", nameof(probabilityThreshold));
        if (string.IsNullOrWhiteSpace(cvText))
            throw new ArgumentException("CvText must not be empty.", nameof(cvText));
        if (string.IsNullOrWhiteSpace(jobText))
            throw new ArgumentException("JobText must not be empty.", nameof(jobText));

        return new IterationRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = userId,
            CvText = cvText,
            JobText = jobText,
            IterationCount = iterationCount,
            ProbabilityThreshold = probabilityThreshold,
            CreatedAt = now,
            Status = RequestStatus.Running,
        };
    }
}

public enum RequestStatus
{
    Running = 1,
    Completed = 2,
    Failed = 3,
    TimedOut = 4,
}
```

### `IterationStep` — `BuildCv-api/src/BuildCv.Domain/Iterations/IterationStep.cs`

```csharp
namespace BuildCv.Domain.Iterations;

public sealed record IterationStep
{
    public int IterationNumber { get; init; }
    public string AdaptedCvText { get; init; } = "";
    public int Score { get; init; }
    public Severity Severity { get; init; }
    public bool PassedArtI { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime CompletedAt { get; init; }
}
```

### `IterationResult` — `BuildCv-api/src/BuildCv.Domain/Iterations/IterationResult.cs`

```csharp
namespace BuildCv.Domain.Iterations;

public sealed record IterationResult
{
    public Guid RequestId { get; init; }
    public RequestStatus Status { get; init; }
    public IterationStep? BestStep { get; init; }
    public IReadOnlyList<IterationStep> AllSteps { get; init; } = Array.Empty<IterationStep>();
    public ProbabilityWarning? ProbabilityWarning { get; init; }
    public int CreditsConsumed { get; init; }
    public bool Partial { get; init; }
    public int ArtIViolations { get; init; }
    public string EngineVersion { get; init; } = "018-iteration-loop-1.0.0";
    public DateTime CompletedAt { get; init; }
}
```

### `ProbabilityWarning` — `BuildCv-api/src/BuildCv.Domain/Iterations/ProbabilityWarning.cs`

```csharp
namespace BuildCv.Domain.Iterations;

public sealed record ProbabilityWarning
{
    public bool BelowThreshold { get; init; }
    public int ThresholdPct { get; init; }
    public int BestPct { get; init; }
    public IReadOnlyList<string> RecommendedActions { get; init; } = Array.Empty<string>();

    public static ProbabilityWarning From(int bestScore, int threshold) => new()
    {
        BelowThreshold = true,
        ThresholdPct = threshold,
        BestPct = bestScore,
        RecommendedActions = new[]
        {
            "Considera mejorar tu CV antes de aplicar.",
            "La vacante puede requerir experiencia que tu CV no refleja aún; busca vacantes más afines o gana experiencia en las áreas clave.",
            "Esta información es orientativa y no garantiza el resultado del proceso de selección.",
        },
    };
}
```

## Application layer

### `IIterationService` — `BuildCv-api/src/BuildCv.Application/Features/Iterations/IIterationService.cs`

```csharp
using BuildCv.Domain.Iterations;

namespace BuildCv.Application.Features.Iterations;

public interface IIterationService
{
    Task<IterationResult> RunAsync(IterationRequest request, CancellationToken ct = default);
    Task<IterationResult?> GetAsync(Guid requestId, CancellationToken ct = default);
}
```

### `IIterationStore` — `BuildCv-api/src/BuildCv.Application/Features/Iterations/IIterationStore.cs`

```csharp
using BuildCv.Domain.Iterations;

namespace BuildCv.Application.Features.Iterations;

public interface IIterationStore
{
    Task<IterationResult?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default);
    Task SaveAsync(IterationResult result, CancellationToken ct = default);
    Task UpdateRequestStatusAsync(Guid requestId, RequestStatus status, CancellationToken ct = default);
    Task DeleteExpiredAsync(DateTime olderThan, CancellationToken ct = default);
}
```

### `IterateAdaptationHandler` — `BuildCv.Application/Features/Iterations/IterateAdaptationHandler.cs`

```csharp
using System.Diagnostics;
using BuildCv.Application.Features.Adapt;
using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Scoring;
using BuildCv.Domain.Adapt;
using BuildCv.Domain.Iterations;
using BuildCv.Domain.Scoring;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Iterations;

public sealed class IterateAdaptationHandler
{
    private static readonly TimeSpan PerIterationTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromMinutes(5);

    private readonly AdaptCvHandler _adaptHandler;
    private readonly ScoreCvHandler _scoreHandler;
    private readonly CrossEntityValidator _crossValidator;
    private readonly EntityExtractor _extractor;
    private readonly ICreditConsumptionService _creditService;
    private readonly IIterationStore _store;
    private readonly ILogger<IterateAdaptationHandler> _logger;

    public IterateAdaptationHandler(
        AdaptCvHandler adaptHandler,
        ScoreCvHandler scoreHandler,
        CrossEntityValidator crossValidator,
        EntityExtractor extractor,
        ICreditConsumptionService creditService,
        IIterationStore store,
        ILogger<IterateAdaptationHandler> logger)
    {
        _adaptHandler = adaptHandler;
        _scoreHandler = scoreHandler;
        _crossValidator = crossValidator;
        _extractor = extractor;
        _creditService = creditService;
        _store = store;
        _logger = logger;
    }

    public async Task<IterationResult> HandleAsync(
        Guid userId,
        string cvText,
        string jobText,
        int iterationCount,
        int probabilityThreshold,
        CancellationToken ct = default)
    {
        var request = IterationRequest.Create(
            userId, cvText, jobText, iterationCount, probabilityThreshold, DateTime.UtcNow);

        await _store.SaveAsync(IterationResult.FromRunningRequest(request), ct);

        var consumeResult = await _creditService.ConsumeForIterationAsync(
            userId, request.RequestId, iterationCount, ct);

        if (!consumeResult.Success)
        {
            await _store.UpdateRequestStatusAsync(request.RequestId, RequestStatus.Failed, ct);
            throw new InsufficientCreditsException(consumeResult.BalanceAfter, iterationCount);
        }

        var startedAt = DateTime.UtcNow;
        var allSteps = new List<IterationStep>(iterationCount);
        IterationStep? bestStep = null;
        var artIViolations = 0;
        var timedOut = false;

        for (var i = 1; i <= iterationCount; i++)
        {
            if (DateTime.UtcNow - startedAt > TotalTimeout)
            {
                timedOut = true;
                break;
            }

            ct.ThrowIfCancellationRequested();
            var stepStart = DateTime.UtcNow;
            var stepDuration = TimeSpan.Zero;

            try
            {
                using var perIterCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                perIterCts.CancelAfter(PerIterationTimeout);

                var adaptCmd = new AdaptCvCommand(request.CvText, request.JobText);
                var adaptResult = await _adaptHandler.Handle(adaptCmd, perIterCts.Token);

                if (adaptResult.IsFailure)
                {
                    allSteps.Add(new IterationStep
                    {
                        IterationNumber = i,
                        AdaptedCvText = string.Empty,
                        Score = 0,
                        Severity = Severity.Critical,
                        PassedArtI = false,
                        Duration = DateTime.UtcNow - stepStart,
                        CompletedAt = DateTime.UtcNow,
                    });
                    artIViolations++;
                    _logger.LogWarning(
                        "Iteration adapt failed (requestId={RequestId}, iteration={I}, error={Error}, cvLength={CvLength})",
                        request.RequestId, i, adaptResult.Error.Code, request.CvText.Length);
                    continue;
                }

                var originalEntities = _extractor.Extract(request.CvText);
                var adaptedEntities = _extractor.Extract(adaptResult.Value.AdaptedCv);
                var mergedOriginal = UnionEntities(originalEntities);
                var mergedAdapted = UnionEntities(adaptedEntities);
                var entityTypes = BuildEntityTypeMap(originalEntities, adaptedEntities);
                var validation = _crossValidator.Validate(mergedOriginal, mergedAdapted, entityTypes);
                var passedArtI = validation.Severity != Severity.Critical;

                var score = 0;
                if (passedArtI)
                {
                    var scoreCmd = new ScoreCvCommand(adaptResult.Value.AdaptedCv, request.JobText);
                    var scoreResult = _scoreHandler.Handle(scoreCmd);
                    score = scoreResult.OverallScore;
                }
                else
                {
                    artIViolations++;
                }

                var step = new IterationStep
                {
                    IterationNumber = i,
                    AdaptedCvText = adaptResult.Value.AdaptedCv,
                    Score = score,
                    Severity = validation.Severity,
                    PassedArtI = passedArtI,
                    Duration = DateTime.UtcNow - stepStart,
                    CompletedAt = DateTime.UtcNow,
                };
                allSteps.Add(step);

                if (passedArtI && (bestStep is null || step.Score > bestStep.Score))
                {
                    bestStep = step;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                allSteps.Add(new IterationStep
                {
                    IterationNumber = i,
                    AdaptedCvText = string.Empty,
                    Score = 0,
                    Severity = Severity.Critical,
                    PassedArtI = false,
                    Duration = DateTime.UtcNow - stepStart,
                    CompletedAt = DateTime.UtcNow,
                });
                artIViolations++;
                _logger.LogWarning(
                    "Iteration timed out (requestId={RequestId}, iteration={I}, perIterationTimeoutSec={TimeoutSec})",
                    request.RequestId, i, PerIterationTimeout.TotalSeconds);
            }
        }

        var finalTimedOut = timedOut || (DateTime.UtcNow - startedAt) > TotalTimeout;
        var status = bestStep is null
            ? RequestStatus.Failed
            : finalTimedOut ? RequestStatus.TimedOut : RequestStatus.Completed;

        ProbabilityWarning? warning = null;
        if (bestStep is not null && bestStep.Score < request.ProbabilityThreshold)
        {
            warning = ProbabilityWarning.From(bestStep.Score, request.ProbabilityThreshold);
        }

        var result = new IterationResult
        {
            RequestId = request.RequestId,
            Status = status,
            BestStep = bestStep,
            AllSteps = allSteps,
            ProbabilityWarning = warning,
            CreditsConsumed = iterationCount,
            Partial = finalTimedOut && bestStep is not null,
            ArtIViolations = artIViolations,
            CompletedAt = DateTime.UtcNow,
        };

        await _store.SaveAsync(result, ct);
        await _store.UpdateRequestStatusAsync(request.RequestId, status, ct);

        _logger.LogInformation(
            "Iteration loop completed (requestId={RequestId}, status={Status}, iterationsRun={IterationsRun}, bestScore={BestScore}, artIViolations={ArtIViolations})",
            request.RequestId, status, allSteps.Count, bestStep?.Score ?? 0, artIViolations);

        return result;
    }

    private static IReadOnlyList<string> UnionEntities(ExtractedEntities entities)
        => entities.Skills
            .Concat(entities.Companies)
            .Concat(entities.Dates)
            .Concat(entities.Metrics)
            .Concat(entities.Certifications)
            .Concat(entities.Titles)
            .ToList();

    private static Dictionary<string, InventionType> BuildEntityTypeMap(
        ExtractedEntities original,
        ExtractedEntities adapted)
    {
        var map = new Dictionary<string, InventionType>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in original.Skills.Concat(adapted.Skills)) map[s] = InventionType.Skill;
        foreach (var s in original.Companies.Concat(adapted.Companies)) map[s] = InventionType.Company;
        foreach (var s in original.Dates.Concat(adapted.Dates)) map[s] = InventionType.Date;
        foreach (var s in original.Metrics.Concat(adapted.Metrics)) map[s] = InventionType.Metric;
        foreach (var s in original.Certifications.Concat(adapted.Certifications)) map[s] = InventionType.Certification;
        foreach (var s in original.Titles.Concat(adapted.Titles)) map[s] = InventionType.Title;
        return map;
    }
}

public sealed class InsufficientCreditsException(int balance, int required)
    : Exception($"Insufficient credits: required {required}, balance {balance}.")
{
    public int Balance { get; } = balance;
    public int Required { get; } = required;
}
```

### `GetIterationResultHandler` — `BuildCv.Application/Features/Iterations/GetIterationResultHandler.cs`

```csharp
using BuildCv.Domain.Iterations;

namespace BuildCv.Application.Features.Iterations;

public sealed class GetIterationResultHandler(IIterationStore store)
{
    public Task<IterationResult?> HandleAsync(Guid requestId, CancellationToken ct = default)
        => store.GetByRequestIdAsync(requestId, ct);
}
```

### `IterationService` — `BuildCv.Application/Features/Iterations/IterationService.cs`

```csharp
using BuildCv.Domain.Iterations;

namespace BuildCv.Application.Features.Iterations;

public sealed class IterationService(
    IterateAdaptationHandler iterate,
    GetIterationResultHandler get,
    ILogger<IterationService> logger) : IIterationService
{
    public Task<IterationResult> RunAsync(IterationRequest request, CancellationToken ct = default)
        => iterate.HandleAsync(request.UserId, request.CvText, request.JobText,
            request.IterationCount, request.ProbabilityThreshold, ct);

    public Task<IterationResult?> GetAsync(Guid requestId, CancellationToken ct = default)
        => get.HandleAsync(requestId, ct);
}
```

### `ICreditConsumptionService` extension (additive) — `BuildCv.Application/Features/Credits/ICreditConsumptionService.cs`

```csharp
public interface ICreditConsumptionService
{
    // ... existing methods unchanged

    /// <summary>
    /// Debits <paramref name="creditCount"/> credits atomically against the iteration request ledger entry.
    /// Returns success=false with BalanceAfter when balance < creditCount. No partial debits.
    /// </summary>
    Task<CreditConsumeResult> ConsumeForIterationAsync(
        Guid userId,
        Guid iterationRequestId,
        int creditCount,
        CancellationToken ct);
}
```

### `IterationResult.FromRunningRequest` — domain helper

```csharp
public sealed record IterationResult
{
    // ... existing properties

    /// <summary>
    /// Builds a transient "Running" snapshot for persistence before the loop starts.
    /// Used so the GET endpoint can return 404 vs 200 with status=Running consistently.
    /// </summary>
    public static IterationResult FromRunningRequest(IterationRequest request) => new()
    {
        RequestId = request.RequestId,
        Status = RequestStatus.Running,
        AllSteps = Array.Empty<IterationStep>(),
        CreditsConsumed = 0,
        CompletedAt = request.CreatedAt,
    };
}
```

## Infrastructure layer

### `IterationRequestConfiguration` — `BuildCv.Infrastructure/Persistence/IterationRequestConfiguration.cs`

```csharp
using BuildCv.Domain.Iterations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence;

public sealed class IterationRequestConfiguration : IEntityTypeConfiguration<IterationRequest>
{
    public void Configure(EntityTypeBuilder<IterationRequest> builder)
    {
        builder.ToTable("iteration_requests");
        builder.HasKey(r => r.RequestId);

        builder.Property(r => r.RequestId).HasColumnName("request_id");
        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.CvText).HasColumnName("cv_text").HasColumnType("text").IsRequired();
        builder.Property(r => r.JobText).HasColumnName("job_text").HasColumnType("text").IsRequired();
        builder.Property(r => r.IterationCount).HasColumnName("iteration_count");
        builder.Property(r => r.ProbabilityThreshold).HasColumnName("probability_threshold");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>();

        builder.HasIndex(r => new { r.UserId, r.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_iteration_requests_user_created_at");

        builder.HasIndex(r => new { r.Status, r.CreatedAt })
            .HasDatabaseName("ix_iteration_requests_status_created_at");
    }
}
```

### `IterationResultConfiguration` — `BuildCv.Infrastructure/Persistence/IterationResultConfiguration.cs`

```csharp
using BuildCv.Domain.Iterations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence;

public sealed class IterationResultConfiguration : IEntityTypeConfiguration<IterationResult>
{
    public void Configure(EntityTypeBuilder<IterationResult> builder)
    {
        builder.ToTable("iteration_results");
        builder.HasKey(r => r.RequestId);

        builder.Property(r => r.RequestId).HasColumnName("request_id");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(r => r.BestStepJson).HasColumnName("best_step").HasColumnType("jsonb");
        builder.Property(r => r.AllStepsJson).HasColumnName("all_steps").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.ProbabilityWarningJson).HasColumnName("probability_warning").HasColumnType("jsonb");
        builder.Property(r => r.CreditsConsumed).HasColumnName("credits_consumed");
        builder.Property(r => r.Partial).HasColumnName("partial");
        builder.Property(r => r.ArtIViolations).HasColumnName("art_i_violations");
        builder.Property(r => r.EngineVersion).HasColumnName("engine_version").HasMaxLength(50).IsRequired();
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at");

        builder.HasIndex(r => r.ExpiresAt)
            .HasDatabaseName("ix_iteration_results_expires_at");
    }
}
```

> **Persistence shape**: `IterationResult` is stored as `jsonb` for `best_step`, `all_steps`, and `probability_warning` (denormalized — query by score threshold is deferred to v1.5). The record's complex shape is flattened to string columns + JSON columns; the entity class adds shadow properties for the JSON columns.

### `IterationResultEntity` (storage projection) — `BuildCv.Domain/Iterations/IterationResultEntity.cs`

```csharp
namespace BuildCv.Domain.Iterations;

internal sealed record IterationResultEntity
{
    public Guid RequestId { get; init; }
    public int Status { get; init; }
    public string? BestStepJson { get; init; }
    public string AllStepsJson { get; init; } = "[]";
    public string? ProbabilityWarningJson { get; init; }
    public int CreditsConsumed { get; init; }
    public bool Partial { get; init; }
    public int ArtIViolations { get; init; }
    public string EngineVersion { get; init; } = "";
    public DateTime CompletedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}
```

> **Rationale**: storing `IterationResult` directly in EF is awkward because the domain record is pure (no EF attributes, no IO, Constitution Art. VI). The adapter `EfIterationStore` is responsible for (de)serializing JSON. The entity class is a thin projection that EF can map to columns; `EfIterationStore.SaveAsync(result)` does `JsonSerializer.Serialize(result.AllSteps)` etc. and computes `ExpiresAt = UtcNow + 24h`.

### `EfIterationStore` — `BuildCv.Infrastructure/Iterations/EfIterationStore.cs`

```csharp
using System.Text.Json;
using BuildCv.Application.Features.Iterations;
using BuildCv.Domain.Iterations;
using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Iterations;

public sealed class EfIterationStore(BuildCvDbContext db) : IIterationStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async Task<IterationResult?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var entity = await db.Set<IterationResultEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct);

        if (entity is null) return null;

        return new IterationResult
        {
            RequestId = entity.RequestId,
            Status = (RequestStatus)entity.Status,
            BestStep = entity.BestStepJson is null
                ? null
                : JsonSerializer.Deserialize<IterationStep>(entity.BestStepJson, JsonOpts),
            AllSteps = JsonSerializer.Deserialize<List<IterationStep>>(entity.AllStepsJson, JsonOpts)
                       ?? new List<IterationStep>(),
            ProbabilityWarning = entity.ProbabilityWarningJson is null
                ? null
                : JsonSerializer.Deserialize<ProbabilityWarning>(entity.ProbabilityWarningJson, JsonOpts),
            CreditsConsumed = entity.CreditsConsumed,
            Partial = entity.Partial,
            ArtIViolations = entity.ArtIViolations,
            EngineVersion = entity.EngineVersion,
            CompletedAt = entity.CompletedAt,
        };
    }

    public async Task SaveAsync(IterationResult result, CancellationToken ct = default)
    {
        var entity = new IterationResultEntity
        {
            RequestId = result.RequestId,
            Status = (int)result.Status,
            BestStepJson = result.BestStep is null
                ? null
                : JsonSerializer.Serialize(result.BestStep, JsonOpts),
            AllStepsJson = JsonSerializer.Serialize(result.AllSteps, JsonOpts),
            ProbabilityWarningJson = result.ProbabilityWarning is null
                ? null
                : JsonSerializer.Serialize(result.ProbabilityWarning, JsonOpts),
            CreditsConsumed = result.CreditsConsumed,
            Partial = result.Partial,
            ArtIViolations = result.ArtIViolations,
            EngineVersion = result.EngineVersion,
            CompletedAt = result.CompletedAt,
            ExpiresAt = result.CompletedAt.AddHours(24),
        };

        var existing = await db.Set<IterationResultEntity>()
            .FirstOrDefaultAsync(r => r.RequestId == result.RequestId, ct);

        if (existing is null)
        {
            await db.Set<IterationResultEntity>().AddAsync(entity, ct);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(entity);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateRequestStatusAsync(Guid requestId, RequestStatus status, CancellationToken ct = default)
    {
        var existing = await db.Set<IterationResultEntity>()
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct);

        if (existing is null) return;

        existing.GetType().GetProperty(nameof(IterationResultEntity.Status))!
            .SetValue(existing, (int)status);

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteExpiredAsync(DateTime olderThan, CancellationToken ct = default)
    {
        await db.Set<IterationResultEntity>()
            .Where(r => r.ExpiresAt < olderThan)
            .ExecuteDeleteAsync(ct);
    }
}
```

### `InMemoryIterationStore` — `BuildCv.Infrastructure/Iterations/InMemoryIterationStore.cs`

```csharp
using System.Collections.Concurrent;
using BuildCv.Application.Features.Iterations;
using BuildCv.Domain.Iterations;

namespace BuildCv.Infrastructure.Iterations;

public sealed class InMemoryIterationStore : IIterationStore
{
    private readonly ConcurrentDictionary<Guid, IterationResult> _results = new();

    public Task<IterationResult?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default)
        => Task.FromResult(_results.TryGetValue(requestId, out var r) ? r : null);

    public Task SaveAsync(IterationResult result, CancellationToken ct = default)
    {
        _results[result.RequestId] = result;
        return Task.CompletedTask;
    }

    public Task UpdateRequestStatusAsync(Guid requestId, RequestStatus status, CancellationToken ct = default)
    {
        if (_results.TryGetValue(requestId, out var existing))
        {
            _results[requestId] = existing with { Status = status };
        }
        return Task.CompletedTask;
    }

    public Task DeleteExpiredAsync(DateTime olderThan, CancellationToken ct = default)
    {
        foreach (var kvp in _results.Where(kvp => kvp.Value.CompletedAt.AddHours(24) < olderThan).ToList())
        {
            _results.TryRemove(kvp.Key, out _);
        }
        return Task.CompletedTask;
    }
}
```

### `IterationCleanupWorker` — `BuildCv.Infrastructure/Iterations/IterationCleanupWorker.cs`

```csharp
using BuildCv.Application.Features.Iterations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildCv.Infrastructure.Iterations;

public sealed class IterationCleanupWorker(
    IIterationStore store,
    ILogger<IterationCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var deleted = await store.DeleteExpiredAsync(DateTime.UtcNow, stoppingToken);
                logger.LogInformation("Iteration cleanup tick (deleted={Deleted})", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Iteration cleanup failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
```

### Migration — `BuildCv-api/src/BuildCv.Infrastructure/Persistence/Migrations/20260625HHMMSS_AddIterationResults.cs`

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildCv.Infrastructure.Persistence.Migrations;

public partial class AddIterationResults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "iteration_requests",
            columns: table => new
            {
                request_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                cv_text = table.Column<string>(type: "text", nullable: false),
                job_text = table.Column<string>(type: "text", nullable: false),
                iteration_count = table.Column<int>(type: "integer", nullable: false),
                probability_threshold = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_iteration_requests", x => x.request_id);
                table.ForeignKey(
                    name: "FK_iteration_requests_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.CheckConstraint("CK_iteration_requests_count", "iteration_count BETWEEN 1 AND 20");
                table.CheckConstraint("CK_iteration_requests_threshold", "probability_threshold BETWEEN 0 AND 100");
            });

        migrationBuilder.CreateTable(
            name: "iteration_results",
            columns: table => new
            {
                request_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                best_step = table.Column<string>(type: "jsonb", nullable: true),
                all_steps = table.Column<string>(type: "jsonb", nullable: false),
                probability_warning = table.Column<string>(type: "jsonb", nullable: true),
                credits_consumed = table.Column<int>(type: "integer", nullable: false),
                partial = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                art_i_violations = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                engine_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_iteration_results", x => x.request_id);
                table.ForeignKey(
                    name: "FK_iteration_results_iteration_requests_request_id",
                    column: x => x.request_id,
                    principalTable: "iteration_requests",
                    principalColumn: "request_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_iteration_requests_user_created_at",
            table: "iteration_requests",
            columns: new[] { "user_id", "created_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "ix_iteration_requests_status_created_at",
            table: "iteration_requests",
            columns: new[] { "status", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_iteration_results_expires_at",
            table: "iteration_results",
            column: "expires_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "iteration_results");
        migrationBuilder.DropTable(name: "iteration_requests");
    }
}
```

### `BuildCvDbContext` (modified) — `BuildCv.Infrastructure/Persistence/BuildCvDbContext.cs`

```csharp
public sealed class BuildCvDbContext(DbContextOptions<BuildCvDbContext> options) : DbContext(options)
{
    // ... existing DbSets

    public DbSet<BuildCv.Domain.Iterations.IterationResultEntity> IterationResults
        => Set<BuildCv.Domain.Iterations.IterationResultEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BuildCvDbContext).Assembly);
        // NEW: IterationResultConfiguration registered via ApplyConfigurationsFromAssembly
    }
}
```

> `IterationRequest` is a transient domain record (no EF mapping); only `IterationResultEntity` is mapped. The lifecycle entry (Request vs Result) collapses to a single persisted row (`iteration_results`) with the JSON `status` field — this simplifies the schema and matches the spec's "requestId = primary key" idempotency contract.

### `EfCreditConsumptionService.ConsumeForIterationAsync` (additive implementation) — `BuildCv.Infrastructure/Credits/EfCreditConsumptionService.cs`

```csharp
public async Task<CreditConsumeResult> ConsumeForIterationAsync(
    Guid userId,
    Guid iterationRequestId,
    int creditCount,
    CancellationToken ct)
{
    if (creditCount <= 0)
        throw new ArgumentException("creditCount must be > 0", nameof(creditCount));

    await using var tx = await _db.Database.BeginTransactionAsync(ct);

    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
    if (user is null) return CreditConsumeResult.Insufficient(0);
    if (user.CreditBalance < creditCount)
        return CreditConsumeResult.Insufficient(user.CreditBalance);

    user.CreditBalance -= creditCount;
    _db.CreditLedgerEntries.Add(new CreditLedgerEntry
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Reason = CreditLedgerReason.Consumption,
        Reference = $"iterate:{iterationRequestId}",
        Delta = -creditCount,
        BalanceAfter = user.CreditBalance,
        Metadata = JsonSerializer.Serialize(new { iterationRequestId, creditCount }),
        CreatedAt = DateTime.UtcNow,
    });

    await _db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
    return new CreditConsumeResult(true, user.CreditBalance, null);
}
```

### `DependencyInjection` (modified) — `BuildCv.Infrastructure/DependencyInjection.cs`

```csharp
// inside AddInfrastructure(), in the Postgres branch:
services.AddScoped<IIterationStore, EfIterationStore>();
services.AddSingleton<IterateAdaptationHandler>(sp => new IterateAdaptationHandler(
    sp.GetRequiredService<AdaptCvHandler>(),
    sp.GetRequiredService<ScoreCvHandler>(),
    sp.GetRequiredService<CrossEntityValidator>(),
    sp.GetRequiredService<EntityExtractor>(),
    sp.GetRequiredService<ICreditConsumptionService>(),
    sp.GetRequiredService<IIterationStore>(),
    sp.GetRequiredService<ILogger<IterateAdaptationHandler>>()));
services.AddSingleton<GetIterationResultHandler>();
services.AddSingleton<IIterationService, IterationService>();
services.AddHostedService<IterationCleanupWorker>();

// inside the InMemory branch:
services.AddSingleton<IIterationStore, InMemoryIterationStore>();
// (same handler/service singletons)
```

## API layer

### `IterationEndpoints` — `BuildCv-api/src/BuildCv.Api/Endpoints/IterationEndpoints.cs`

```csharp
using System.Security.Claims;
using BuildCv.Api.Contracts;
using BuildCv.Api.Filters;
using BuildCv.Application.Features.Iterations;
using BuildCv.Domain.Iterations;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BuildCv.Api.Endpoints;

public static class IterationEndpoints
{
    public const string IteratePolicy = "iterate";

    public static IEndpointRouteBuilder MapIterationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/adapt/iterate")
            .RequireAuthorization()
            .WithTags("Iterations");

        group.MapPost("/", IterateHandler)
            .AddEndpointFilter<ValidationFilter<IterateRequestDto>>()
            .RequireRateLimiting(IteratePolicy)
            .RequireCredits(0) // overridden by per-request credit check; see handler
            .WithName("StartIteration")
            .WithSummary("Run a best-of-N adaptation loop. Consumes N credits (one per iteration).")
            .Produces<IterationResultDto>(200)
            .Produces(401)
            .Produces(402)
            .Produces(422)
            .Produces(429);

        group.MapGet("/{requestId:guid}", GetIterationHandler)
            .RequireRateLimiting(IteratePolicy)
            .WithName("GetIteration")
            .WithSummary("Retrieve a cached iteration result by requestId (24h TTL).")
            .Produces<IterationResultDto>(200)
            .Produces(401)
            .Produces(404);

        return app;
    }

    private static async Task<Results<
        Ok<IterationResultDto>,
        UnauthorizedHttpResult,
        StatusCodeHttpResult,
        UnprocessableEntityHttpResult>> IterateHandler(
        [FromBody] IterateRequestDto body,
        [FromServices] IterateAdaptationHandler handler,
        [FromServices] IIterationService service,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var parsedUserId))
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            var result = await service.RunAsync(IterationRequest.Create(
                parsedUserId,
                body.CvText,
                body.VacancyText,
                body.IterationCount ?? 5,
                body.ProbabilityThreshold ?? 50,
                DateTime.UtcNow), ct);

            return TypedResults.Ok(IterationResultDto.FromDomain(result));
        }
        catch (ArgumentException ex)
        {
            return TypedResults.UnprocessableEntity(new
            {
                type = "https://buildcv.com/errors/validation-invalid-input",
                title = "VALIDATION/INVALID_INPUT",
                status = StatusCodes.Status422UnprocessableEntity,
                detail = ex.Message,
            });
        }
        catch (InsufficientCreditsException ex)
        {
            httpContext.Response.Headers["X-Credit-Balance"] = ex.Balance.ToString();
            httpContext.Response.Headers["Retry-After"] = "0";
            return TypedResults.Json(
                new
                {
                    type = "https://buildcv.com/errors/credit-insufficient",
                    title = "INSUFFICIENT_CREDITS",
                    status = StatusCodes.Status402PaymentRequired,
                    code = "CREDIT/INSUFFICIENT",
                    balance = ex.Balance,
                    required = ex.Required,
                },
                statusCode: StatusCodes.Status402PaymentRequired);
        }
    }

    private static async Task<Results<Ok<IterationResultDto>, NotFound<string>, UnauthorizedHttpResult>>
        GetIterationHandler(
            Guid requestId,
            [FromServices] IIterationService service,
            ClaimsPrincipal user,
            CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out _))
        {
            return TypedResults.Unauthorized();
        }

        var result = await service.GetAsync(requestId, ct);
        return result is null
            ? TypedResults.NotFound("ITERATION/NOT_FOUND")
            : TypedResults.Ok(IterationResultDto.FromDomain(result));
    }
}
```

> **Note on `.RequireCredits(0)`**: the per-request credit gate is enforced inside the handler (because `iterationCount` is in the request body, not knowable at endpoint registration time). The `.RequireCredits(0)` is a no-op placeholder — it documents intent and keeps the endpoint declarative. The actual gate is `ConsumeForIterationAsync` which throws `InsufficientCreditsException` if `balance < iterationCount`.

### Contracts — `BuildCv-api/src/BuildCv.Api/Contracts/IterationContracts.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using BuildCv.Domain.Iterations;

namespace BuildCv.Api.Contracts;

public sealed record IterateRequestDto
{
    [Required, MaxLength(50_000)] public string CvText { get; init; } = "";
    [Required, MaxLength(20_000)] public string VacancyText { get; init; } = "";
    public int? IterationCount { get; init; }
    public int? ProbabilityThreshold { get; init; }
}

public sealed record IterationResultDto
{
    public Guid RequestId { get; init; }
    public string Status { get; init; } = "";
    public IterationStepDto? BestStep { get; init; }
    public IReadOnlyList<IterationStepDto> AllSteps { get; init; } = Array.Empty<IterationStepDto>();
    public ProbabilityWarningDto? ProbabilityWarning { get; init; }
    public int CreditsConsumed { get; init; }
    public bool Partial { get; init; }
    public int ArtIViolations { get; init; }
    public string EngineVersion { get; init; } = "";
    public DateTime CompletedAt { get; init; }

    public static IterationResultDto FromDomain(IterationResult r) => new()
    {
        RequestId = r.RequestId,
        Status = r.Status.ToString(),
        BestStep = r.BestStep is null ? null : IterationStepDto.FromDomain(r.BestStep),
        AllSteps = r.AllSteps.Select(IterationStepDto.FromDomain).ToList(),
        ProbabilityWarning = r.ProbabilityWarning is null
            ? null
            : ProbabilityWarningDto.FromDomain(r.ProbabilityWarning),
        CreditsConsumed = r.CreditsConsumed,
        Partial = r.Partial,
        ArtIViolations = r.ArtIViolations,
        EngineVersion = r.EngineVersion,
        CompletedAt = r.CompletedAt,
    };
}

public sealed record IterationStepDto
{
    public int IterationNumber { get; init; }
    public string AdaptedCvText { get; init; } = "";
    public int Score { get; init; }
    public string Severity { get; init; } = "";
    public bool PassedArtI { get; init; }
    public double DurationMs { get; init; }
    public DateTime CompletedAt { get; init; }

    public static IterationStepDto FromDomain(IterationStep s) => new()
    {
        IterationNumber = s.IterationNumber,
        AdaptedCvText = s.AdaptedCvText,
        Score = s.Score,
        Severity = s.Severity.ToString(),
        PassedArtI = s.PassedArtI,
        DurationMs = s.Duration.TotalMilliseconds,
        CompletedAt = s.CompletedAt,
    };
}

public sealed record ProbabilityWarningDto
{
    public bool BelowThreshold { get; init; }
    public int ThresholdPct { get; init; }
    public int BestPct { get; init; }
    public IReadOnlyList<string> RecommendedActions { get; init; } = Array.Empty<string>();

    public static ProbabilityWarningDto FromDomain(ProbabilityWarning p) => new()
    {
        BelowThreshold = p.BelowThreshold,
        ThresholdPct = p.ThresholdPct,
        BestPct = p.BestPct,
        RecommendedActions = p.RecommendedActions,
    };
}
```

### `RateLimiting.cs` (modified) — `BuildCv-api/src/BuildCv.Api/Security/RateLimiting.cs`

```csharp
public const string IteratePolicy = "iterate";

// inside AddAppRateLimiting:
options.AddPolicy(IteratePolicy, httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ClientKey(httpContext),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        }));
```

### `Program.cs` (modified) — `BuildCv-api/src/BuildCv.Api/Program.cs`

```csharp
// inside top-level pipeline:
app.MapIterationEndpoints();   // NEW
```

## CV_generator integration

**v1 (this change)**: the user manually uploads the generated CV via the existing `POST /api/v1/import` endpoint (005-cv-pdf-docx-import), or pastes Markdown directly into the iteration request body (`cvText` field). Documented in `BuildCv-web/docs/integrations/cv-generator.md`:

```markdown
# CV_generator → BuildCv integration (v1)

BuildCv v1 does NOT have direct API integration with `~/Documentos/CV_generator:main`.

## Recommended workflow

1. Generate your CV in `CV_generator` (output: Markdown or PDF).
2. Open BuildCv and navigate to `/analizar/iterate`.
3. **Option A** — PDF/DOCX: click "Subir CV" → uses `POST /api/v1/import` (005) → text is parsed and pre-filled into the iteration form.
4. **Option B** — Markdown: open the `.md` file in your editor, copy the contents, paste into the "CV" textarea.
5. Paste the vacancy text into the "Vacante" textarea.
6. Configure iteration count (1-20, default 5) and probability threshold (0-100, default 50).
7. Click "Iniciar iteración" — credits are debited, best-of-N loop runs, results are returned.

## v2 roadmap (deferred)

Direct API integration: webhook from `CV_generator` → `BuildCv` to start iteration automatically when a new CV is generated. Tracked in `specs/_archive/018-cv-iteration-loop/`.
```

**v2 (deferred, out of scope)**: webhook from `CV_generator` → `BuildCv` to start iteration automatically. No code in this change.

## Test strategy

### Unit tests — Domain (5+)

| Test | What it asserts |
|------|-----------------|
| `IterationRequest_Create_ValidArgs_ReturnsRunning` | Default Status=Running, Idempotency key = fresh Guid |
| `IterationRequest_Create_IterationCount0_Throws` | Range guard 1..20 |
| `IterationRequest_Create_IterationCount21_Throws` | Range guard 1..20 |
| `IterationRequest_Create_ThresholdMinus1_Throws` | Range guard 0..100 |
| `IterationRequest_Create_Threshold101_Throws` | Range guard 0..100 |
| `IterationRequest_Create_EmptyCv_Throws` | Empty guard |
| `ProbabilityWarning_From_BelowThreshold_PopulatesActions` | 3 generic actions present |
| `IterationResult_FromRunningRequest_Defaults` | AllSteps=[], CreditsConsumed=0, Status=Running |

### Unit tests — Application (15+)

| Test | What it asserts |
|------|-----------------|
| `IterateAsync_RunsN_Iterations` | Loop runs N=5 calls to adapt+score+validate |
| `IterateAsync_SelectsBest_StepWithHighestScore` | bestStep.Score = max(allSteps where PassedArtI) |
| `IterateAsync_SkipsSteps_FailingArtI` | Severity.Critical ⇒ not in bestStep candidates |
| `IterateAsync_AllStepsCritical_StatusFailed` | bestStep is null, Status=Failed |
| `IterateAsync_BelowThreshold_GeneratesProbabilityWarning` | warning.BestPct == bestStep.Score |
| `IterateAsync_AtOrAboveThreshold_NoWarning` | warning is null when bestStep.Score >= threshold |
| `IterateAsync_DebitsN_CreditsAtomically` | creditService.ConsumeForIterationAsync called with N=iterationCount BEFORE loop |
| `IterateAsync_NoRefundOnPartialFailure` | loop throws ⇒ credits NOT refunded |
| `IterateAsync_PerIterationTimeout_RecordsFailedStep` | per-iteration 30s exceeded ⇒ step recorded with Severity.Critical |
| `IterateAsync_TotalTimeout_ReturnsTimedOut_WithPartialFlag` | 5min cap exceeded ⇒ Status=TimedOut, Partial=true |
| `IterateAsync_ArtIViolationsCount_Exposed` | artIViolations = count of PassedArtI=false steps |
| `IterateAsync_NullArgs_Throws` | ArgumentNullException for required args |
| `GetIterationAsync_ReturnsCached_WhenExists` | IIterationStore.GetByRequestIdAsync called once |
| `GetIterationAsync_ReturnsNull_WhenNotFound` | returns null for missing RequestId |
| `IterateAsync_CallsAdaptWith_SeedRequestIdColonI` | seed format = `{RequestId}:{i}` passed to PromptBuilder |

### Integration tests — Infrastructure (15+)

| Test | What it asserts |
|------|-----------------|
| `EfIterationStore_SaveAndGet_RoundTrip` | Save then Get returns equivalent record |
| `EfIterationStore_GetByRequestIdAsync_ReturnsNull_WhenMissing` | missing key returns null |
| `EfIterationStore_JsonbSerialization_RoundTripsSteps` | AllSteps JSON deserializes to equivalent list |
| `EfIterationStore_JsonbSerialization_BestStep_NullWhenAbsent` | null bestStep round-trips as JSON null |
| `EfIterationStore_DeleteExpiredAsync_RemovesOldRows` | rows with ExpiresAt < cutoff are deleted |
| `EfIterationStore_DeleteExpiredAsync_KeepsFreshRows` | rows with ExpiresAt >= cutoff are retained |
| `EfIterationStore_UpdateRequestStatusAsync_Persists` | Status enum updates and persists |
| `EfIterationStore_CascadeDelete_OnUserRemoved` | user anonymize ⇒ iteration_results cascade-delete |
| `EfIterationStore_PostgresMigration_Applies` | migration applies + reverts cleanly |
| `InMemoryIterationStore_GetByRequestIdAsync_ReturnsLatest` | last write wins |
| `InMemoryIterationStore_DeleteExpiredAsync_RemovesOldRows` | rows > 24h removed |
| `IterationCleanupWorker_TickDeletes_ExpiredRows` | background worker removes expired |
| `ConsumeForIterationAsync_DebitsN_Atomically` | balance -= N in single transaction |
| `ConsumeForIterationAsync_InsufficientBalance_NoDebit` | balance < N ⇒ returns Insufficient, balance unchanged |
| `ConsumeForIterationAsync_CreditLedgerEntry_HasReferenceIterate` | entry.Reference = `iterate:{RequestId}` |

### End-to-end tests — API (10+)

| Test | What it asserts |
|------|-----------------|
| `POST_Iterate_200_Auth_5Iterations_DefaultThreshold` | happy path, 5 iterations, default threshold=50 |
| `POST_Iterate_402_InsufficientCredits` | balance=3, iterationCount=5 ⇒ 402 CREDIT/INSUFFICIENT |
| `POST_Iterate_422_IterationCount_OutOfRange` | iterationCount=21 ⇒ 422 |
| `POST_Iterate_422_Threshold_OutOfRange` | threshold=101 ⇒ 422 |
| `POST_Iterate_401_Unauthenticated` | no JWT ⇒ 401 |
| `POST_Iterate_429_RateLimit` | 11th request in 1h ⇒ 429 |
| `GET_Iterate_200_CachedResult` | cached result returned byte-equal |
| `GET_Iterate_404_NotFound` | random Guid ⇒ 404 ITERATION/NOT_FOUND |
| `GET_Iterate_404_Expired` | TTL=24h exceeded ⇒ 404 ITERATION/EXPIRED (after cleanup tick) |
| `POST_Iterate_AllIterationsCritical_ReturnsFailed` | all iterations fail Art. I ⇒ Status=Failed, no warning |
| `POST_Iterate_BelowThreshold_ReturnsProbabilityWarning` | warning has 3 generic actions |
| `POST_Iterate_AtThreshold_NoWarning` | bestStep.Score == threshold ⇒ warning is null |

### End-to-end tests — Web (10+ Playwright)

| Test | What it asserts |
|------|-----------------|
| `IteratePage_LoadsWithDefaults` | iterationCount=5, threshold=50, "5 credits needed" |
| `IteratePage_SliderUpdatesCostEstimate` | iterationCount=10 ⇒ "10 credits needed" |
| `IteratePage_StartButton_TriggersAPI` | click ⇒ BFF POST 200, result card renders |
| `IteratePage_ProbabilityWarning_RendersBelowThreshold` | best=30% threshold=50% ⇒ amber banner with 3 actions |
| `IteratePage_ProbabilityWarning_HiddenAboveThreshold` | best=70% threshold=50% ⇒ no banner |
| `IteratePage_AllFailedBanner_RendersWhenStatusFailed` | Status=Failed ⇒ "ninguna iteración pasó Art. I" |
| `IteratePage_ExportPdfButton_CallsExportEndpoint` | "Exportar PDF" ⇒ 004 endpoint called with bestStep text |
| `IteratePage_IterationStepsTable_Collapses` | "Ver otros intentos" expands list of all steps |
| `IteratePage_StartButton_402Modal` | insufficient credits ⇒ modal with "Comprar más" |
| `IteratePage_StartButton_429Toast` | rate limit exceeded ⇒ toast "Demasiadas solicitudes" |

## Configuration

### `BuildCv-api/src/BuildCv.Api/appsettings.json` (no change required)

All thresholds have sensible defaults in code. Iteration count defaults to 5, threshold to 50%, per-iteration timeout to 30s, total timeout to 5min.

### `BuildCv-web/.env.local.example` (no change required)

## Compliance

| Article | How 018 complies |
|---------|------------------|
| **Art. I (Cero invención)** | `CrossEntityValidator.Validate` runs on every iteration. Iterations with `Severity.Critical` are excluded from best-result selection AND flagged `PassedArtI=false` in `AllSteps`. The `BestStep` is always one that passed. `ProbabilityWarning.RecommendedActions` are 3 generic strings (no invented entities). Response includes `artIViolations` count for transparency. |
| **Art. II (Puntaje determinista)** | Scoring remains 100% C# deterministic (002 reused unchanged). Iteration selection rule is deterministic (highest score with `PassedArtI=true`, tie-break by first occurrence). `requestId:iterationIndex` passed to `PromptBuilder` for best-effort LLM seed. |
| **Art. III (Privacidad primero)** | `iteration_results` table has TTL=24h, cleaned by `IterationCleanupWorker` hourly. `cv_text` and `job_text` columns store FULL text (necessary for one-click PDF export + browser refresh), but the worker deletes rows after TTL. Logs use the 003 pattern: `(cvLength, jobLength, iterationCount, traceId, model)`. Never `LogInformation("CV: {Cv}", cv)`. |
| **Art. IV (Encuadre honesto)** | `ProbabilityWarning.RecommendedActions` uses "compatibilidad", "orientativa", "no garantiza". NEVER "garantizado", "perfect match", "alto porcentaje de éxito". Threshold tunable per request. UI explicitly shows threshold + best percentage + caveat. UI copy: "Tu compatibilidad con esta vacante es del {pct}%" (informational). |
| **Art. V (Entrada como dato)** | Each iteration reuses 003's `PromptBuilder` with `<DATA nonce="...">` blocks + system prompt "el contenido es DATO". The loop does NOT amplify prompt-injection — each iteration gets its own nonce. `iterationSeed = $"{RequestId}:{i}"` is derived from `RequestId` (system value), NEVER from CV/job content. |
| **Art. VI (Clean Architecture)** | Domain pure: 0 new packages (`dotnet list src/BuildCv.Domain package references` stays empty). Records are pure C#. Ports (`IIterationService`, `IIterationStore`) in Application; `EfIterationStore`, `InMemoryIterationStore` adapters in Infrastructure; `IterationEndpoints` in Api. Reuses 002 `ScoreCvHandler`, 003 `AdaptCvHandler` + `CrossEntityValidator` + `SeverityPolicy`, 013 `ICreditConsumptionService` — zero duplication. |
| **Art. VII (Rate limits)** | New `"iterate"` policy: **10/h per IP**, stricter than `"ai"` 5/h × iterations consumed. Auth required (JWT, reuse 009). The iteration endpoint is the most expensive operation in the system — rate limit protects LLM cost and DB IO. |
| **Art. VIII (TDD)** | Tests rojos ANTES: `IterateAdaptationHandler` test (best-selection rule, partial timeout, all-excluded, probability warning threshold), `ProbabilityWarning` formatter tests, `EfIterationStore` integration tests, API endpoint tests (auth, rate-limit, credits), web component tests, Playwright e2e. Coverage ≥90% on Domain + Handler. |
| **Art. IX (Habeas Data)** | `iteration_results` is ephemeral (24h TTL). No CV/job content in logs. No CV/job content in metrics. ARCO delete via 009 cascade: when user is anonymized, `iteration_requests` + `iteration_results` rows are cascade-deleted (`REFERENCES users(id) ON DELETE CASCADE`). Privacy policy update: one line about "iteration loop results stored for 24h, auto-deleted, includes your adapted CV and score". |

## Out of scope (deferred)

- LLM temperature sampling control (v1.5)
- A/B testing of different prompts (v1.5)
- User feedback loop "did this help?" (v1.5)
- Multi-vacancy ranking (v1.5)
- Per-iteration streaming via SSE (v1.5)
- Parallel iteration execution (v1.5 with batch-grade cost reduction)
- Direct `CV_generator` API integration (v2)
- Custom `RecommendedActions` per request (v1.5 — Art. IV consistency: hardcoded 3)

## Strategy: 3 chained PRs

### PR1 (~250 lines, +20 unit tests): Domain + Application

**Scope**: Domain types + ports + handlers + unit tests. Zero DB, zero HTTP.

**Work-unit commits**:

1. `feat(018): domain — IterationRequest + IterationStep + IterationResult + ProbabilityWarning + RequestStatus`
   - Files: `BuildCv.Domain/Iterations/{IterationRequest,IterationStep,IterationResult,ProbabilityWarning}.cs`
   - ~120 LoC, +5 unit tests (range guards, defaults, factory)
2. `feat(018): application — IIterationService + IIterationStore`
   - Files: `BuildCv.Application/Features/Iterations/{IIterationService,IIterationStore}.cs`
   - ~30 LoC, no tests (interfaces)
3. `feat(018): application — IterateAdaptationHandler + GetIterationResultHandler + IterationService`
   - Files: `BuildCv.Application/Features/Iterations/{IterateAdaptationHandler,GetIterationResultHandler,IterationService}.cs`
   - ~150 LoC, +15 unit tests (best-selection, partial timeout, all-excluded, threshold, credits, idempotency)
4. `feat(018): application — ConsumeForIterationAsync extension to ICreditConsumptionService`
   - Files: `BuildCv.Application/Features/Credits/ICreditConsumptionService.cs` (interface only)
   - ~10 LoC, no tests (interface extension)
5. `chore(018): application — DI registration of new ports and handlers`
   - Files: `BuildCv.Infrastructure/DependencyInjection.cs` (handler singletons + InMemoryIterationStore fallback)
   - ~20 LoC, no new tests

**Gates**: `dotnet build` 0 warnings, `dotnet test` 100% green.

### PR2 (~300 lines, +15 integration tests): Infrastructure + DB

**Scope**: EF adapter + InMemory adapter + worker + migration + EfCreditConsumptionService extension.

**Work-unit commits**:

1. `feat(018): infrastructure — IterationRequestConfiguration + IterationResultConfiguration`
   - Files: `BuildCv.Infrastructure/Persistence/IterationRequestConfiguration.cs`, `IterationResultConfiguration.cs`
   - ~80 LoC, no tests
2. `feat(018): infrastructure — IterationResultEntity (storage projection)`
   - Files: `BuildCv.Domain/Iterations/IterationResultEntity.cs`
   - ~30 LoC, +1 unit test (default values)
3. `feat(018): infrastructure — EfIterationStore`
   - Files: `BuildCv.Infrastructure/Iterations/EfIterationStore.cs`
   - ~100 LoC, +5 integration tests (round-trip, JSON, delete-expired)
4. `feat(018): infrastructure — InMemoryIterationStore + IterationCleanupWorker`
   - Files: `BuildCv.Infrastructure/Iterations/{InMemoryIterationStore,IterationCleanupWorker}.cs`
   - ~80 LoC, +3 integration tests (in-memory semantics, worker tick)
5. `feat(018): infrastructure — EfCreditConsumptionService.ConsumeForIterationAsync`
   - Files: `BuildCv.Infrastructure/Credits/EfCreditConsumptionService.cs`
   - ~40 LoC, +3 integration tests (atomic debit, insufficient, idempotency via ledger)
6. `feat(018): infrastructure — migration AddIterationResults`
   - Files: `BuildCv.Infrastructure/Persistence/Migrations/20260625HHMMSS_AddIterationResults.cs` + `.Designer.cs`
   - ~80 LoC, +3 integration tests (migration applies, rollback, cascade)

**Gates**: `dotnet ef migrations script` valid, `dotnet test` 100% green.

### PR3 (~200 lines, +10 e2e tests): API + Web

**Scope**: endpoints, rate-limit policy, DTOs, BFF routes, components, page, doc.

**Work-unit commits**:

1. `feat(018): api — IterationEndpoints + DTOs + ValidationFilter`
   - Files: `BuildCv.Api/Endpoints/IterationEndpoints.cs`, `BuildCv.Api/Contracts/IterationContracts.cs`
   - ~140 LoC, +5 e2e tests (200, 402, 422, 401, 404)
2. `feat(018): api — iterate rate-limit policy + Program.cs wiring`
   - Files: `BuildCv.Api/Security/RateLimiting.cs` (add IteratePolicy), `BuildCv.Api/Program.cs` (MapIterationEndpoints)
   - ~30 LoC, +1 e2e test (429)
3. `feat(018): web — BFF routes + components + /analizar/iterate page + i18n copy`
   - Files: `BuildCv-web/app/api/adapt/iterate/{route.ts, [requestId]/route.ts}`, `BuildCv-web/components/iterate/{IterationControlPanel,IterationResultCard,IterationStepList,ProbabilityWarning}.tsx`, `BuildCv-web/app/analizar/iterate/page.tsx`, `BuildCv-web/messages/{es,en}.json`
   - ~150 LoC, +5 Playwright e2e (loads, slider, banner, export, 402 modal)
4. `docs(018): web — CV_generator integration doc + UI tooltip`
   - Files: `BuildCv-web/docs/integrations/cv-generator.md`, `BuildCv-web/components/iterate/IterationControlPanel.tsx` (tooltip)
   - ~30 LoC, no tests

**Gates**: `dotnet build`, `pnpm lint && pnpm build && pnpm test`, `preflight.sh` all green.

**Total**: ~770 LoC, +50 tests (5+15 unit + 15 integration + 10+5 e2e), 14 work-unit commits across 3 PRs.

## Open questions (carry over from proposal, resolved by spec)

1. **Synchronous vs async** — **synchronous default** (`wait=true`), `wait=false` returns 202 (deferred to v1.5).
2. **Storage shape** — **full text** stored as JSONB in `iteration_results.all_steps` (one-click PDF export + browser refresh).
3. **CV_generator integration** — **v1 = upload/paste**, v2 = direct API (documented in `BuildCv-web/docs/integrations/cv-generator.md`).
4. **Per-iteration scoring cost** — **negligible** (CPU-bound, deterministic, microseconds per CV).
5. **Cleanup frequency** — **hourly** via `IHostedService` `IterationCleanupWorker`.
6. **RecommendedActions content** — **hardcoded 3** in code (Art. IV consistency).

## Next

`sdd-tasks` → forecast 400-line budget per PR, lock work-unit commits per PR (5-6 commits each), generate `tasks.md` with TDD discipline (tests red before implementation).