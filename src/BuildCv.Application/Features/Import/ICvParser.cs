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
