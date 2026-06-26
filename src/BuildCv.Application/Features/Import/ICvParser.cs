namespace BuildCv.Application.Features.Import;

/// <summary>
/// Puerto de parseo de archivos (Constitution Art. VI v1.1.0 — ICvParser).
/// Los adaptadores concretos (PdfPig, OpenXml) viven en Infrastructure.
///
/// Este contrato se mantiene durante la transición a engineVersion 2.0.0 para no
/// romper a los parsers legacy. Se retirará cuando el último adaptador concreto
/// haya migrado a <see cref="IStructuredParser"/> (PR 2b/2c/2d de 021).
/// </summary>
public interface ICvParser
{
    ImportResult Parse(ImportCvCommand command);
}

/// <summary>
/// Puerto nuevo (change 021) que retorna el <see cref="ParseResult"/> discriminated
/// union. Los adaptadores legacy se conectan a este puerto vía
/// <see cref="LegacyParserAdapter"/>; los nuevos (PdfPig v2, OpenXml v2) emitirán
/// <see cref="StructuredParseResult"/> directamente (PR 2b/2c).
/// </summary>
public interface IStructuredParser
{
    ParseResult Parse(ImportCvCommand command);
}

/// <summary>
/// Marker de tipo que anuncia el <c>MimeType</c> que un adaptador concreto es capaz
/// de parsear. El <c>ParserRouter</c> (micro-batch 2d de 021) usa este contrato
/// para resolver el parser adecuado para cada <see cref="ImportCvCommand.MimeType"/>
/// desde un <see cref="IEnumerable{T}"/> registrado por DI, sin necesidad de
/// acoplarse a tipos concretos.
///
/// Implementado por todos los adaptadores que exponen tanto la ruta legacy
/// (<see cref="ICvParser"/>) como la ruta estructurada (<see cref="IStructuredParser"/>):
/// <c>PdfPigCvParser</c> (PDF) y <c>OpenXmlCvParser</c> (DOCX).
/// </summary>
public interface IKnownMimeParser
{
    /// <summary>
    /// <c>MimeType</c> (RFC 2046) que este adaptador sabe parsear. La comparación es
    /// case-insensitive y se hace sin parámetros (sin charset ni boundary).
    /// </summary>
    string SupportedMimeType { get; }
}

/// <summary>
/// Puerto del router de parseo (micro-batch 2d de 021). Es la única abstracción
/// que la capa de Aplicación consume para convertir un <see cref="ImportCvCommand"/>
/// en un <see cref="ParseResult"/> discriminated union.
///
/// El adaptador (<c>ParserRouter</c>, en Infrastructure) es responsable de:
/// <list type="bullet">
///   <item>Resolver el parser correcto a partir de <c>command.MimeType</c> y
///     <c>command.EngineVersion</c>.</item>
///   <item>Validar bytes mágicos como fast-fail.</item>
///   <item>Enrutar a <see cref="ICvParser"/> (engineVersion 1.0.0) o
///     <see cref="IStructuredParser"/> (engineVersion 2.0.0).</item>
///   <item>Adaptar el resultado legacy al <see cref="RawParseResult"/> vía
///     <see cref="LegacyParserAdapter"/> cuando corresponde.</item>
/// </list>
///
/// Constitución Art. VI: este es el ÚNICO puerto de parseo que la Aplicación conoce.
/// La familia <see cref="ICvParser"/> / <see cref="IStructuredParser"/> queda como
/// detalle interno del adaptador (Infrastructure) — el handler ya no la conoce.
/// </summary>
public interface IParserRouter
{
    /// <summary>
    /// Despacha el comando al parser correcto y devuelve un <see cref="ParseResult"/>
    /// (raw para v1, estructurado para v2).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> es null.</exception>
    /// <exception cref="ParserEngineException">MIME no soportado o bytes mágicos inválidos.</exception>
    /// <exception cref="InvalidOperationException">
    /// <c>command.EngineVersion</c> no es <c>"1.0.0"</c> ni <c>"2.0.0"</c>.
    /// </exception>
    ParseResult Parse(ImportCvCommand command);
}
