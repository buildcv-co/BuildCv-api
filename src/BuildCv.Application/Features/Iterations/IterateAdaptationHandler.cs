using BuildCv.Application.Features.Adapt;
using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Scoring;
using BuildCv.Domain.Adapt;
using BuildCv.Domain.Credits;
using BuildCv.Domain.Iterations;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Iterations;

public sealed class IterateAdaptationHandler(
    AdaptCvHandler adaptHandler,
    ScoreCvHandler scoreHandler,
    CrossEntityValidator crossValidator,
    EntityExtractor extractor,
    IIterationStore store,
    ICreditLedger ledger,
    ILogger<IterateAdaptationHandler> logger,
    TimeSpan? perIterationTimeout = null,
    TimeSpan? totalTimeout = null)
{
    private readonly TimeSpan _perIterationTimeout = perIterationTimeout ?? TimeSpan.FromSeconds(30);
    private readonly TimeSpan _totalTimeout = totalTimeout ?? TimeSpan.FromMinutes(5);

    public async Task<IterationResult> HandleAsync(
        Guid userId,
        string cvText,
        string vacancyText,
        int iterationCount,
        int threshold,
        CancellationToken ct = default)
    {
        var request = IterationRequest.Create(userId, cvText, vacancyText, iterationCount, threshold, DateTime.UtcNow);
        await store.SaveRequestAsync(request, ct);

        var balance = await ledger.GetBalanceAsync(userId, ct);
        if (balance < iterationCount)
        {
            await store.UpdateRequestStatusAsync(request.RequestId, RequestStatus.Failed, ct);
            throw new InsufficientCreditsException(balance, iterationCount);
        }

        await ledger.AccreditAsync(
            userId: userId,
            reason: CreditLedgerReason.Consumption,
            reference: $"iterate:{request.RequestId}",
            delta: -iterationCount,
            balanceAfter: balance - iterationCount,
            metadata: null,
            ct: ct);

        var allSteps = new List<IterationStep>(iterationCount);
        IterationStep? bestStep = null;
        var startTime = DateTime.UtcNow;
        var timedOut = false;

        for (var i = 1; i <= iterationCount; i++)
        {
            if (DateTime.UtcNow - startTime > _totalTimeout)
            {
                timedOut = true;
                break;
            }

            ct.ThrowIfCancellationRequested();
            var stepStart = DateTime.UtcNow;

            try
            {
                using var iterCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                iterCts.CancelAfter(_perIterationTimeout);

                var adaptCmd = new AdaptCvCommand(request.CvText, request.VacancyText, Seed: $"{request.RequestId}:{i}");
                var adaptOutcome = await adaptHandler.Handle(adaptCmd, iterCts.Token);

                if (adaptOutcome.IsFailure)
                {
                    allSteps.Add(new IterationStep
                    {
                        IterationNumber = i,
                        AdaptedCvText = string.Empty,
                        Score = 0,
                        PassedArtI = false,
                        Timestamp = stepStart,
                        Duration = DateTime.UtcNow - stepStart,
                    });
                    logger.LogWarning(
                        "Iteration adapt failed (requestId={RequestId}, iteration={I}, errorCode={ErrorCode}, cvLength={CvLength})",
                        request.RequestId, i, adaptOutcome.Error.Code, request.CvText.Length);
                    continue;
                }

                var adaptedCv = adaptOutcome.Value;
                var validation = Validate(adaptedCv.AdaptedCv, request.CvText);
                var passedArtI = validation.Severity != Severity.Critical;

                var score = 0;
                if (passedArtI)
                {
                    var scoreCmd = new TextScoreCommand(adaptedCv.AdaptedCv, request.VacancyText);
                    var scoreOutcome = scoreHandler.Handle(scoreCmd);
                    if (scoreOutcome is V1ScoreOutcome v1)
                    {
                        score = v1.Result.Overall;
                    }
                }

                var step = new IterationStep
                {
                    IterationNumber = i,
                    AdaptedCvText = adaptedCv.AdaptedCv,
                    Score = score,
                    PassedArtI = passedArtI,
                    Timestamp = stepStart,
                    Duration = DateTime.UtcNow - stepStart,
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
                    PassedArtI = false,
                    Timestamp = stepStart,
                    Duration = DateTime.UtcNow - stepStart,
                });
                logger.LogWarning(
                                        "Iteration timed out (requestId={RequestId}, iteration={I}, perIterationTimeoutSec={TimeoutSec})",
                                        request.RequestId, i, _perIterationTimeout.TotalSeconds);
            }
        }

        var finalTimedOut = timedOut || (DateTime.UtcNow - startTime) > _totalTimeout;
        var status = bestStep is null
            ? RequestStatus.Failed
            : finalTimedOut ? RequestStatus.TimedOut : RequestStatus.Completed;

        string? warning = null;
        if (bestStep is not null && bestStep.Score < threshold)
        {
            warning = $"Tu compatibilidad con esta vacante es del {bestStep.Score}% (umbral: {threshold}%). Considera mejorar tu CV antes de aplicar o buscar vacantes más afines.";
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
            CompletedAt = DateTime.UtcNow,
        };

        await store.SaveResultAsync(result, ct);
        await store.UpdateRequestStatusAsync(request.RequestId, status, ct);

        logger.LogInformation(
            "Iteration loop completed (requestId={RequestId}, status={Status}, iterationsRun={IterationsRun}, bestScore={BestScore})",
            request.RequestId, status, allSteps.Count, bestStep?.Score ?? 0);

        return result;
    }

    private ValidationReport Validate(string adaptedCv, string originalCv)
    {
        var originalEntities = extractor.Extract(originalCv);
        var adaptedEntities = extractor.Extract(adaptedCv);
        var mergedOriginal = UnionEntities(originalEntities);
        var mergedAdapted = UnionEntities(adaptedEntities);
        var entityTypes = BuildEntityTypeMap(originalEntities, adaptedEntities);
        return crossValidator.Validate(mergedOriginal, mergedAdapted, entityTypes);
    }

    private static IReadOnlyList<string> UnionEntities(ExtractedEntities entities)
    {
        return entities.Skills
            .Concat(entities.Companies)
            .Concat(entities.Dates)
            .Concat(entities.Metrics)
            .Concat(entities.Certifications)
            .Concat(entities.Titles)
            .ToList();
    }

    private static Dictionary<string, InventionType> BuildEntityTypeMap(
        ExtractedEntities original,
        ExtractedEntities adapted)
    {
        var map = new Dictionary<string, InventionType>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in original.Skills.Concat(adapted.Skills))
        {
            map[s] = InventionType.Skill;
        }

        foreach (var s in original.Companies.Concat(adapted.Companies))
        {
            map[s] = InventionType.Company;
        }

        foreach (var s in original.Dates.Concat(adapted.Dates))
        {
            map[s] = InventionType.Date;
        }

        foreach (var s in original.Metrics.Concat(adapted.Metrics))
        {
            map[s] = InventionType.Metric;
        }

        foreach (var s in original.Certifications.Concat(adapted.Certifications))
        {
            map[s] = InventionType.Certification;
        }

        foreach (var s in original.Titles.Concat(adapted.Titles))
        {
            map[s] = InventionType.Title;
        }

        return map;
    }
}
