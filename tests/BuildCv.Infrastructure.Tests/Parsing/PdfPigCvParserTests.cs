using BuildCv.Application.Features.Import;
using BuildCv.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace BuildCv.Infrastructure.Tests.Parsing;

public sealed class PdfPigCvParserTests
{
    private readonly PdfPigCvParser _parser = new();

    [Fact]
    public void Should_extract_text_and_sections_from_a_real_multi_page_pdf()
    {
        var bytes = PdfTestFixtures.CreateMultiPageCvPdf();
        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: "trace-1");

        var result = _parser.Parse(command);

        result.Should().BeOfType<LegacyImportResult>();
        var legacy = (LegacyImportResult)result;
        legacy.Text.Should().Contain("EXPERIENCIA");
        legacy.Text.Should().Contain("EDUCACIÓN");
        legacy.Text.Should().Contain("HABILIDADES");
        legacy.EngineVersion.Should().Be("1.0.0");
        legacy.TraceId.Should().Be("trace-1");
        legacy.Sections.Should().NotBeEmpty();
        legacy.Sections.Select(s => s.Heading).Should().Contain("EXPERIENCIA");
    }

    [Fact]
    public void Should_throw_ParserEngineException_for_encrypted_or_invalid_pdf_bytes()
    {
        var command = new ImportCvCommand(
            FileBytes: PdfTestFixtures.CreateGarbageBytesWithPdfHeader(),
            MimeType: "application/pdf",
            OriginalFileName: "garbage.pdf",
            TraceId: "trace-1");

        var act = () => _parser.Parse(command);

        act.Should().Throw<ParserEngineException>()
            .Which.Code.Should().BeOneOf("INVALID_PDF", "PDF_ENCRYPTED");
    }

    [Fact]
    public void Should_throw_ParserEngineException_for_empty_bytes()
    {
        var command = new ImportCvCommand(
            FileBytes: [],
            MimeType: "application/pdf",
            OriginalFileName: "empty.pdf",
            TraceId: "trace-1");

        var act = () => _parser.Parse(command);

        act.Should().Throw<ParserEngineException>()
            .Which.Code.Should().Be("EMPTY_FILE");
    }

    [Fact]
    public void Should_preserve_spanish_accents_in_extracted_text()
    {
        var bytes = PdfTestFixtures.CreateSimplePdf("DESARROLLO con tecnología, experiencia, año 2024");
        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: "trace-1");

        var result = _parser.Parse(command);

        var legacy = result.Should().BeOfType<LegacyImportResult>().Subject;
        legacy.Text.Should().Contain("DESARROLLO");
        legacy.Text.Should().Contain("tecnología");
        legacy.Text.Should().Contain("año");
    }
}
