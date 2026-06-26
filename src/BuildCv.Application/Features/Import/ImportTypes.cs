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
