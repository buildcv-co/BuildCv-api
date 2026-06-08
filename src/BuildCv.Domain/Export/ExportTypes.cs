using BuildCv.Domain.Adapt;

namespace BuildCv.Domain.Export;

public sealed record ExportRequest(
    string AdaptedCv,
    ValidationReport Validation,
    string CandidateName);

public sealed record ExportResult(
    byte[] Pdf,
    string Filename,
    int SizeBytes,
    PdfMetadata Metadata);

public sealed record PdfMetadata(
    DateTimeOffset GeneratedAt,
    string EngineVersion,
    string ModelVersion,
    Severity Severity,
    int InventionCount,
    TimeSpan GenerationTime);

public sealed class ValidationGate
{
    public bool CanExport(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return !report.Inventions.Any(i => i.InventionSeverity == InventionSeverity.Hard);
    }

    public string ExplainWhyBlocked(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (CanExport(report))
        {
            return string.Empty;
        }

        var hardInventions = report.Inventions
            .Where(i => i.InventionSeverity == InventionSeverity.Hard)
            .Select(i => i.Claimed)
            .ToList();

        return $"El CV adaptado tiene {hardInventions.Count} invención(es) Hard: [{string.Join(", ", hardInventions)}]. " +
               "Regenera la adaptación con prompt más estricto antes de exportar.";
    }
}
