namespace BuildCv.Application.Features.Import;

/// <summary>
/// Puerto de parseo de archivos (Constitution Art. VI v1.1.0 — ICvParser).
/// Los adaptadores concretos (PdfPig, OpenXml) viven en Infrastructure.
/// </summary>
public interface ICvParser
{
    ImportResult Parse(ImportCvCommand command);
}
