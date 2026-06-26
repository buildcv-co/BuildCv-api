using BuildCv.Application.Features.Import;

namespace BuildCv.Infrastructure.Parsing;

/// <summary>
/// Despachador único que enruta un <see cref="ImportCvCommand"/> al adaptador de parseo
/// correcto según <c>command.EngineVersion</c> y <c>command.MimeType</c>.
///
/// Constitution Art. VI (puertos ICvParser / IStructuredParser) — el dominio solo conoce
/// los puertos; este es el ÚNICO adaptador que se inyecta al consumidor (handler /
/// endpoint). Los adaptadores concretos (PdfPig, OpenXml) se registran en DI como
/// implementaciones de los puertos y se inyectan aquí como colecciones.
///
/// Engine versions soportadas (micro-batch 2d de 021):
/// <list type="bullet">
///   <item><c>"1.0.0"</c> (default cuando el comando llega con <c>EngineVersion=null</c> o
///     ausente): enruta a un <see cref="ICvParser"/> legacy para el MIME declarado,
///     envuelve el resultado vía <see cref="LegacyParserAdapter"/> y devuelve un
///     <see cref="RawParseResult"/>.</item>
///   <item><c>"2.0.0"</c>: enruta a un <see cref="IStructuredParser"/> para el MIME
///     declarado y devuelve el <see cref="StructuredParseResult"/> tal cual.</item>
///   <item>Cualquier otro valor: lanza <see cref="InvalidOperationException"/> con la lista
///     de versiones soportadas. Esto protege contra typos del cliente y contra la
///     aceptación silenciosa de versiones desconocidas (Constitution Art. II: el cambio
///     de versión del motor es un acto explícito bumpeado por SemVer).</item>
/// </list>
///
/// Validación adicional: se hace un chequeo de bytes mágicos (%PDF- para PDF, PK\\x03\\x04
/// para DOCX) ANTES de delegar al parser, para fallar rápido cuando el cliente declara un
/// MIME incorrecto.
/// </summary>
public sealed class ParserRouter : IParserRouter
{
    private const string LegacyEngineVersion = "1.0.0";
    private const string StructuredEngineVersion = "2.0.0";
    private const string PdfMimeType = "application/pdf";
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IReadOnlyList<ICvParser> _legacyParsers;
    private readonly IReadOnlyList<IStructuredParser> _structuredParsers;

    public ParserRouter(
        IEnumerable<ICvParser> legacyParsers,
        IEnumerable<IStructuredParser> structuredParsers)
    {
        ArgumentNullException.ThrowIfNull(legacyParsers);
        ArgumentNullException.ThrowIfNull(structuredParsers);
        _legacyParsers = legacyParsers.ToList();
        _structuredParsers = structuredParsers.ToList();
    }

    /// <summary>
    /// Despacha el comando al parser adecuado para <c>command.EngineVersion</c> y
    /// <c>command.MimeType</c>. Devuelve un <see cref="ParseResult"/> discriminated
    /// union: <see cref="RawParseResult"/> para <c>engineVersion="1.0.0"</c>,
    /// <see cref="StructuredParseResult"/> para <c>engineVersion="2.0.0"</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> es null.</exception>
    /// <exception cref="ParserEngineException">
    /// El MIME no es soportado por ningún adaptador registrado, o los bytes mágicos no
    /// coinciden con el MIME declarado.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <c>command.EngineVersion</c> no es <c>"1.0.0"</c> ni <c>"2.0.0"</c>.
    /// </exception>
    public ParseResult Parse(ImportCvCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var engineVersion = ResolveEngineVersion(command.EngineVersion);

        if (string.Equals(engineVersion, LegacyEngineVersion, StringComparison.Ordinal))
        {
            return ParseLegacy(command);
        }

        if (string.Equals(engineVersion, StructuredEngineVersion, StringComparison.Ordinal))
        {
            return ParseStructured(command);
        }

        throw new InvalidOperationException(
            $"Unsupported engineVersion '{command.EngineVersion}'. Supported: 1.0.0, 2.0.0.");
    }

    private ParseResult ParseLegacy(ImportCvCommand command)
    {
        var mime = NormalizeMime(command.MimeType);
        var parser = ResolveLegacyParser(mime);

        if (parser is null)
        {
            throw new ParserEngineException(
                "UNSUPPORTED_MIME",
                $"Tipo de archivo no soportado: {mime}. Sube un PDF o DOCX.");
        }

        EnsureMagicBytes(command.FileBytes, mime);

        return new LegacyParserAdapter(parser).Parse(command);
    }

    private ParseResult ParseStructured(ImportCvCommand command)
    {
        var mime = NormalizeMime(command.MimeType);
        var parser = ResolveStructuredParser(mime);

        if (parser is null)
        {
            throw new ParserEngineException(
                "UNSUPPORTED_MIME",
                $"Tipo de archivo no soportado: {mime}. Sube un PDF o DOCX.");
        }

        EnsureMagicBytes(command.FileBytes, mime);

        return parser.Parse(command);
    }

    private ICvParser? ResolveLegacyParser(string mime)
    {
        foreach (var parser in _legacyParsers)
        {
            if (parser is IKnownMimeParser known
                && known.SupportedMimeType.Equals(mime, StringComparison.OrdinalIgnoreCase))
            {
                return parser;
            }
        }

        return null;
    }

    private IStructuredParser? ResolveStructuredParser(string mime)
    {
        foreach (var parser in _structuredParsers)
        {
            if (parser is IKnownMimeParser known
                && known.SupportedMimeType.Equals(mime, StringComparison.OrdinalIgnoreCase))
            {
                return parser;
            }
        }

        return null;
    }

    private static string ResolveEngineVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return LegacyEngineVersion;
        }

        return raw.Trim();
    }

    private static string NormalizeMime(string? mime)
    {
        return (mime ?? string.Empty).Trim();
    }

    private static void EnsureMagicBytes(byte[] bytes, string mime)
    {
        if (bytes is null || bytes.Length < 4)
        {
            throw new ParserEngineException(
                "UNSUPPORTED_MIME",
                "Archivo demasiado pequeño para validar.");
        }

        if (string.Equals(mime, PdfMimeType, StringComparison.OrdinalIgnoreCase))
        {
            var isPdf = bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;
            if (!isPdf)
            {
                throw new ParserEngineException(
                    "UNSUPPORTED_MIME",
                    "El archivo no tiene la firma de un PDF (%PDF-).");
            }

            return;
        }

        if (string.Equals(mime, DocxMimeType, StringComparison.OrdinalIgnoreCase))
        {
            var isZip = bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04;
            if (!isZip)
            {
                throw new ParserEngineException(
                    "UNSUPPORTED_MIME",
                    "El archivo no tiene la firma de un DOCX (PK\\x03\\x04).");
            }
        }
    }
}
