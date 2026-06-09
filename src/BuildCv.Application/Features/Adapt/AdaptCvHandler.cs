using BuildCv.Domain.Adapt;
using BuildCv.Domain.Common;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Adapt;

/// <summary>
/// Orquesta el flujo de adaptación:
/// 1. Extrae entidades del CV original
/// 2. Llama al LLM (vía IAiClient) con prompt construido por PromptBuilder
/// 3. Extrae entidades del CV adaptado
/// 4. Cruza entidades (CrossEntityValidator) → detecta invenciones
/// 5. Clasifica severidad (SeverityPolicy)
/// 6. Devuelve resultado con metadata (NUNCA contenido del CV en logs — Constitution Art. III)
/// </summary>
public sealed class AdaptCvHandler
{
    private readonly IAiClient _aiClient;
    private readonly EntityExtractor _extractor;
    private readonly CrossEntityValidator _crossValidator;
    private readonly SeverityPolicy _severityPolicy;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<AdaptCvHandler> _logger;

    public AdaptCvHandler(
        IAiClient aiClient,
        EntityExtractor extractor,
        CrossEntityValidator crossValidator,
        SeverityPolicy severityPolicy,
        PromptBuilder promptBuilder,
        ILogger<AdaptCvHandler> logger)
    {
        _aiClient = aiClient;
        _extractor = extractor;
        _crossValidator = crossValidator;
        _severityPolicy = severityPolicy;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<Result<AdaptationResult>> Handle(AdaptCvCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var originalEntities = _extractor.Extract(command.CvText);

        string adaptedCv;
        try
        {
            var prompt = _promptBuilder.Build(command.CvText, command.JobText);
            adaptedCv = await _aiClient.CompleteAsync(prompt, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("AI client failed (cvLength={CvLength}, jobLength={JobLength}, error={ErrorType})", command.CvText.Length, command.JobText.Length, ex.GetType().Name);
            return Result.Failure<AdaptationResult>(new Error("AI_UNAVAILABLE", "Servicio de IA no disponible temporalmente."));
        }

        var adaptedEntities = _extractor.Extract(adaptedCv);

        var entityTypes = BuildEntityTypeMap(originalEntities, adaptedEntities);
        var mergedOriginal = UnionEntities(originalEntities);
        var mergedAdapted = UnionEntities(adaptedEntities);
        var report = _crossValidator.Validate(mergedOriginal, mergedAdapted, entityTypes);

        var computedSeverity = _severityPolicy.Classify(report.Inventions);
        var finalReport = new ValidationReport(
            report.IsValid && computedSeverity != Severity.Critical,
            computedSeverity,
            report.Inventions,
            report.Warnings);

        var result = new AdaptationResult(
            AdaptedCv: adaptedCv,
            Validation: finalReport,
            EngineVersion: "1.0.0",
            AiModel: "claude-sonnet-4-20250514");

        _logger.LogInformation("Adapt completed (cvLength={CvLength}, jobLength={JobLength}, severity={Severity}, inventions={InventionCount})", command.CvText.Length, command.JobText.Length, finalReport.Severity, finalReport.Inventions.Count);

        return Result.Success(result);
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

    private static Dictionary<string, InventionType> BuildEntityTypeMap(ExtractedEntities original, ExtractedEntities adapted)
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
