using System.ComponentModel.DataAnnotations;
using BuildCv.Domain.Iterations;

namespace BuildCv.Api.Contracts;

public sealed record IterateRequestDto(
    [Required, MaxLength(50_000)] string CvText,
    [Required, MaxLength(20_000)] string VacancyText,
    int? IterationCount,
    int? ProbabilityThreshold);

public sealed record IterationStepDto(
    int IterationNumber,
    string AdaptedCvText,
    int Score,
    bool PassedArtI,
    DateTime Timestamp);

public sealed record IterationResultDto(
    Guid RequestId,
    string Status,
    IterationStepDto? BestStep,
    IReadOnlyList<IterationStepDto> AllSteps,
    string? ProbabilityWarning,
    int CreditsConsumed,
    bool Partial,
    DateTime CompletedAt);

public static class IterationResultMapper
{
    public static IterationResultDto Map(IterationResult result) => new(
        RequestId: result.RequestId,
        Status: result.Status.ToString(),
        BestStep: result.BestStep is null ? null : MapStep(result.BestStep),
        AllSteps: result.AllSteps.Select(MapStep).ToList(),
        ProbabilityWarning: result.ProbabilityWarning,
        CreditsConsumed: result.CreditsConsumed,
        Partial: result.Partial,
        CompletedAt: result.CompletedAt);

    private static IterationStepDto MapStep(IterationStep s) => new(
        IterationNumber: s.IterationNumber,
        AdaptedCvText: s.AdaptedCvText,
        Score: s.Score,
        PassedArtI: s.PassedArtI,
        Timestamp: s.Timestamp);
}
