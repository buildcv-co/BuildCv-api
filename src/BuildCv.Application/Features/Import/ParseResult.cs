using BuildCv.Domain.Resumes;

namespace BuildCv.Application.Features.Import;

/// <summary>
/// Discriminated union que devuelve cada adaptador de <see cref="IStructuredParser"/>.
/// Constitution Art. I: un resultado estructurado NUNCA contiene datos que el parser no
/// haya encontrado en el archivo fuente; los marcadores de confianza (Inferred / Explicit /
/// UserConfirmed) viven dentro del <see cref="CvDocument"/>. Esta unión solo decide si el
/// parser emite texto crudo o un documento tipado.
///
/// Variantes:
///   - <see cref="RawParseResult"/>: adaptadores legacy que solo emiten texto plano
///     (engineVersion 1.0.0). El consumidor debe migrar a 2.0.0 para recibir
///     <see cref="StructuredParseResult"/>.
///   - <see cref="StructuredParseResult"/>: adaptadores que emiten un
///     <see cref="CvDocument"/> tipado (engineVersion 2.0.0).
///
/// Constitution Art. II: el discriminador es inmutable; <c>EngineVersion</c> se sella
/// por variante y se mantiene estable por SemVer.
/// </summary>
public abstract record ParseResult
{
    /// <summary>
    /// Devuelve el <see cref="CvDocument"/> estructurado.
    /// Lanza <see cref="InvalidOperationException"/> si el resultado es crudo —
    /// el cliente debe pedir explícitamente <c>engineVersion 2.0.0</c>.
    /// </summary>
    public abstract CvDocument ToCvDocument();

    /// <summary>
    /// Devuelve el texto crudo extraído.
    /// Lanza <see cref="InvalidOperationException"/> si el resultado ya es estructurado —
    /// el cliente debe usar <see cref="ToCvDocument"/> en su lugar.
    /// </summary>
    public abstract string ToRawText();

    /// <summary>SemVer del motor que produjo el resultado. Sellado por variante.</summary>
    public abstract string EngineVersion { get; }
}

/// <summary>
/// Variante cruda (engineVersion 1.0.0). Devuelta por los adaptadores legacy que
/// solo extraen texto plano; será reemplazada por <see cref="StructuredParseResult"/>
/// cuando los parsers concreten emitan un <see cref="CvDocument"/> tipado
/// (PR 2b/2c de 021).
/// </summary>
public sealed record RawParseResult(string Text, IReadOnlyList<ParsingWarning> Warnings)
    : ParseResult
{
    public override string EngineVersion => "1.0.0";

    public override CvDocument ToCvDocument() => throw new InvalidOperationException(
        "RawParseResult no tiene CvDocument estructurado; el cliente debe pedir engineVersion 2.0.0.");

    public override string ToRawText() => Text;
}

/// <summary>
/// Variante estructurada (engineVersion 2.0.0). Devuelta por adaptadores que producen
/// un <see cref="CvDocument"/> JSON Resume; entrada canónica del motor de puntaje v2
/// (PR 3 de 021).
/// </summary>
public sealed record StructuredParseResult(
    CvDocument Cv,
    IReadOnlyList<ParsingWarning> Warnings)
    : ParseResult
{
    public override string EngineVersion => "2.0.0";

    public override CvDocument ToCvDocument() => Cv;

    public override string ToRawText() => throw new InvalidOperationException(
        "StructuredParseResult no expone texto crudo; use ToCvDocument().");
}

/// <summary>
/// Aviso no bloqueante emitido por un parser. Renombrado desde <c>ImportWarning</c>
/// para evitar colisión con tipos externos y reflejar que también lo producen los
/// parsers estructurados (no solo el flujo de import legacy).
/// </summary>
public sealed record ParsingWarning(
    string Code,
    string Message,
    string Severity);

/// <summary>
/// Shim temporal que envuelve un <see cref="ICvParser"/> legacy (engineVersion 1.0.0) y
/// lo expone como <see cref="IStructuredParser"/> produciendo un <see cref="RawParseResult"/>.
/// Permite migrar la interfaz y el handler sin tocar los parsers concretos.
///
/// TODO(021/2a): este shim se elimina en PR 2b/2c/2d cuando PdfPigCvParser y
/// OpenXmlCvParser emitan directamente <see cref="StructuredParseResult"/>. El
/// <see cref="ICvParser"/> legacy también se retira cuando el último adaptador concreto
/// se haya migrado.
/// </summary>
public sealed class LegacyParserAdapter : IStructuredParser
{
    private readonly ICvParser _legacy;

    public LegacyParserAdapter(ICvParser legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        _legacy = legacy;
    }

    public ParseResult Parse(ImportCvCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var legacyResult = _legacy.Parse(command);
        return new RawParseResult(legacyResult.Text, ConvertWarnings(legacyResult.Warnings));
    }

    private static IReadOnlyList<ParsingWarning> ConvertWarnings(IReadOnlyList<ImportWarning> legacy)
    {
        var list = new List<ParsingWarning>(legacy.Count);
        foreach (var w in legacy)
        {
            list.Add(new ParsingWarning(w.Code, w.Message, w.Severity));
        }

        return list;
    }
}
