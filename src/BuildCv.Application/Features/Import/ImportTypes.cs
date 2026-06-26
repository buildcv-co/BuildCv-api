using BuildCv.Domain.Resumes;

namespace BuildCv.Application.Features.Import;

/// <summary>
/// Comando inmutable para importar un CV. La unidad de aplicación (handler)
/// orquesta validación, parseo y mapeo de errores. El endpoint construye
/// este record a partir del IFormFile multipart.
/// </summary>
/// <param name="FileBytes">Contenido binario del archivo en RAM (Constitution Art. III: nunca se persiste).</param>
/// <param name="MimeType">MIME declarado por el cliente (validado contra PDF/DOCX).</param>
/// <param name="OriginalFileName">Nombre original del archivo, saneado en el endpoint.</param>
/// <param name="TraceId">Identificador de correlación para logs y respuestas de error.</param>
/// <param name="EngineVersion">
/// Versión del motor de parsing solicitada por el cliente. Valores soportados:
/// <c>"1.0.0"</c> (legacy, texto crudo vía <see cref="ICvParser"/>) y
/// <c>"2.0.0"</c> (estructurado, <see cref="StructuredParseResult"/> vía <see cref="IStructuredParser"/>).
/// Cuando es <c>null</c>, el router por defecto usa <c>"1.0.0"</c> (Constitution Art. II: cambio
/// de versión explícito y bumpeado por SemVer; nunca implícito por cliente).
/// </param>
public sealed record ImportCvCommand(
    byte[] FileBytes,
    string MimeType,
    string OriginalFileName,
    string TraceId,
    string? EngineVersion = null);

/// <summary>Sección candidata detectada por la heurística de regex.</summary>
public sealed record ImportSection(
    string Heading,
    int Start,
    int End,
    string Confidence);

/// <summary>Aviso no bloqueante sobre el resultado del parseo (variante legacy).</summary>
public sealed record ImportWarning(
    string Code,
    string Message,
    string Severity);

/// <summary>
/// Resultado del import discriminado por <see cref="EngineVersion"/>. El discriminador
/// inmutable permite al endpoint mapear a la DTO correcta sin reinventar el shape del
/// contrato HTTP (Constitution Art. II — versionado SemVer sellado por variante).
///
/// Variantes:
///   - <see cref="LegacyImportResult"/>: motor 1.0.0 — texto crudo + secciones heurísticas.
///   - <see cref="StructuredImportResult"/>: motor 2.0.0 — <see cref="CvDocument"/> tipado
///     con <c>confidence</c> markers (Constitution Art. I: cero invención).
/// </summary>
public abstract record ImportResult
{
    /// <summary>SemVer del motor que produjo el resultado. Sellado por variante.</summary>
    public abstract string EngineVersion { get; }

    /// <summary>Identificador de correlación del request.</summary>
    public abstract string TraceId { get; }
}

/// <summary>
/// Resultado legacy (engineVersion 1.0.0). Texto crudo extraído del archivo más las
/// secciones candidatas detectadas por heurística. Compatible con clientes que aún
/// no migran al motor estructurado (PR 2e de 021).
/// </summary>
public sealed record LegacyImportResult : ImportResult
{
    public string Text { get; init; }
    public IReadOnlyList<ImportSection> Sections { get; init; }
    public IReadOnlyList<ImportWarning> Warnings { get; init; }

    public LegacyImportResult(
        string text,
        IReadOnlyList<ImportSection> sections,
        IReadOnlyList<ImportWarning> warnings,
        string traceId)
    {
        Text = text;
        Sections = sections;
        Warnings = warnings;
        TraceIdValue = traceId;
    }

    public override string EngineVersion => "1.0.0";
    public override string TraceId => TraceIdValue;
    private string TraceIdValue { get; init; }
}

/// <summary>
/// Resultado estructurado (engineVersion 2.0.0). Contiene el <see cref="CvDocument"/>
/// tipado (JSON Resume extendido) con <c>confidence</c> markers por campo. Esta es
/// la entrada canónica del motor de puntaje v2 (PR 3 de 021) y del editor (PR 4 de 021).
/// </summary>
public sealed record StructuredImportResult : ImportResult
{
    public CvDocument Cv { get; init; }
    public IReadOnlyList<ParsingWarning> Warnings { get; init; }

    public StructuredImportResult(
        CvDocument cv,
        IReadOnlyList<ParsingWarning> warnings,
        string traceId)
    {
        Cv = cv;
        Warnings = warnings;
        TraceIdValue = traceId;
    }

    public override string EngineVersion => "2.0.0";
    public override string TraceId => TraceIdValue;
    private string TraceIdValue { get; init; }
}
