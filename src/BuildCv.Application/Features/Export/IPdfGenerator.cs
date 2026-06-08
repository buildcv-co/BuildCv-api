using BuildCv.Domain.Adapt;
using BuildCv.Domain.Export;

namespace BuildCv.Application.Features.Export;

public sealed record ExportPdfCommand(
    string AdaptedCv,
    ValidationReport Validation,
    string CandidateName);

/// <summary>
/// Puerto de IO para generación de PDF. La capa Domain y Application NO saben
/// que existe QuestPDF — la implementación vive en Infrastructure (Constitution Art. VI).
/// </summary>
public interface IPdfGenerator
{
    byte[] GeneratePdf(ExportRequest request);
}
