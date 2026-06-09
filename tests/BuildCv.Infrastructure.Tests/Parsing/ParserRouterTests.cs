using BuildCv.Application.Features.Import;
using BuildCv.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace BuildCv.Infrastructure.Tests.Parsing;

public sealed class ParserRouterTests
{
    private readonly ParserRouter _router;

    public ParserRouterTests()
    {
        _router = new ParserRouter(new PdfPigCvParser(), new OpenXmlCvParser());
    }

    [Fact]
    public void Should_dispatch_to_pdf_parser_for_pdf_mime_and_magic()
    {
        var bytes = PdfTestFixtures.CreateMultiPageCvPdf();
        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: "trace-1");

        var result = _router.Parse(command);

        result.Text.Should().Contain("EXPERIENCIA");
        result.Text.Should().Contain("HABILIDADES");
    }

    [Fact]
    public void Should_dispatch_to_docx_parser_for_docx_mime_and_magic()
    {
        var bytes = DocxTestFixtures.CreateDocxWithHeadings("EXPERIENCIA", "EDUCACIÓN");
        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "cv.docx",
            TraceId: "trace-1");

        var result = _router.Parse(command);

        result.Text.Should().Contain("EXPERIENCIA");
        result.Text.Should().Contain("EDUCACIÓN");
    }

    [Fact]
    public void Should_throw_ParserEngineException_for_unsupported_mime()
    {
        var command = new ImportCvCommand(
            FileBytes: [0x25, 0x50, 0x44, 0x46],
            MimeType: "text/plain",
            OriginalFileName: "fake.txt",
            TraceId: "trace-1");

        var act = () => _router.Parse(command);

        act.Should().Throw<ParserEngineException>()
            .Which.Code.Should().Be("UNSUPPORTED_MIME");
    }

    [Fact]
    public void Should_throw_ParserEngineException_when_pdf_magic_bytes_missing()
    {
        var command = new ImportCvCommand(
            FileBytes: [0x47, 0x49, 0x46, 0x38, 0x39, 0x61],
            MimeType: "application/pdf",
            OriginalFileName: "fake.pdf",
            TraceId: "trace-1");

        var act = () => _router.Parse(command);

        act.Should().Throw<ParserEngineException>()
            .Which.Code.Should().Be("UNSUPPORTED_MIME");
    }
}
