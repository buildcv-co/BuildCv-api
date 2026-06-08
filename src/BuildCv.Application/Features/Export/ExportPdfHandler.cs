using System.Diagnostics;
using BuildCv.Domain.Common;
using BuildCv.Domain.Export;

namespace BuildCv.Application.Features.Export;

/// <summary>
/// Orquesta el flujo de export:
/// 1. ValidationGate decide si el ValidationReport permite export (Art. I)
/// 2. IPdfGenerator genera el PDF (delegado a Infrastructure)
/// 3. Metadata (timestamps, sizes) se sella en el resultado
/// 4. Log estructurado sin PII (Constitution Art. III NFR-002)
/// </summary>
public sealed class ExportPdfHandler
{
    private readonly IPdfGenerator _generator;
    private readonly ValidationGate _gate;

    public ExportPdfHandler(IPdfGenerator generator, ValidationGate gate)
    {
        _generator = generator;
        _gate = gate;
    }

    public Task<Result<ExportResult>> Handle(ExportPdfCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_gate.CanExport(command.Validation))
        {
            var explanation = _gate.ExplainWhyBlocked(command.Validation);
            System.Console.WriteLine($"ExportPdfHandler: blocked (hard inventions present)");
            return Task.FromResult(Result.Failure<ExportResult>(new Error("EXPORT_BLOCKED_INVENTION", explanation)));
        }

        var request = new ExportRequest(
            AdaptedCv: command.AdaptedCv,
            Validation: command.Validation,
            CandidateName: string.IsNullOrWhiteSpace(command.CandidateName) ? "Candidato" : command.CandidateName);

        var stopwatch = Stopwatch.StartNew();
        byte[] pdfBytes;
        try
        {
            pdfBytes = _generator.GeneratePdf(request);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Console.Error.WriteLine($"ExportPdfHandler: PDF generation failed (error={ex.GetType().Name})");
            return Task.FromResult(Result.Failure<ExportResult>(new Error("PDF_UNAVAILABLE", "Generación de PDF no disponible temporalmente.")));
        }
        stopwatch.Stop();

        var filename = $"cv-adapted-{DateTimeOffset.UtcNow:yyyy-MM-dd}.pdf";
        var metadata = new PdfMetadata(
            GeneratedAt: DateTimeOffset.UtcNow,
            EngineVersion: "1.0.0",
            ModelVersion: "004-export-pdf",
            Severity: command.Validation.Severity,
            InventionCount: command.Validation.Inventions.Count,
            GenerationTime: stopwatch.Elapsed);

        var result = new ExportResult(
            Pdf: pdfBytes,
            Filename: filename,
            SizeBytes: pdfBytes.Length,
            Metadata: metadata);

        System.Console.WriteLine(
            $"Export completed (cvLength={command.AdaptedCv.Length}, fileSize={pdfBytes.Length}, generationTimeMs={stopwatch.ElapsedMilliseconds}, severity={command.Validation.Severity})");

        return Task.FromResult(Result.Success(result));
    }
}
