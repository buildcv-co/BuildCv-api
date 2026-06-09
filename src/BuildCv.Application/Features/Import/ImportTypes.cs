namespace BuildCv.Application.Features.Import;

/// <summary>
/// Comando inmutable para importar un CV. La unidad de aplicación (handler)
/// orquesta validación, parseo y mapeo de errores. El endpoint construye
/// este record a partir del IFormFile multipart.
/// </summary>
public sealed record ImportCvCommand(
    byte[] FileBytes,
    string MimeType,
    string OriginalFileName,
    string TraceId);

/// <summary>Sección candidata detectada por la heurística de regex.</summary>
public sealed record ImportSection(
    string Heading,
    int Start,
    int End,
    string Confidence);

/// <summary>Aviso no bloqueante sobre el resultado del parseo.</summary>
public sealed record ImportWarning(
    string Code,
    string Message,
    string Severity);

/// <summary>
/// Resultado del parseo. Es la semilla que consume el editor (006) y, vía
/// este editor, el score (002) y la adaptación (003).
/// </summary>
public sealed record ImportResult(
    string Text,
    IReadOnlyList<ImportSection> Sections,
    IReadOnlyList<ImportWarning> Warnings,
    string EngineVersion,
    string TraceId);
