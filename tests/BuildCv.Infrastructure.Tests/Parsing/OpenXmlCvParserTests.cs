using BuildCv.Application.Features.Import;
using BuildCv.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace BuildCv.Infrastructure.Tests.Parsing;

public sealed class OpenXmlCvParserTests
{
    private readonly OpenXmlCvParser _parser = new();

    [Fact]
    public void Should_extract_text_and_sections_from_a_real_docx()
    {
        var bytes = DocxTestFixtures.CreateDocxWithHeadings("EXPERIENCIA", "EDUCACIÓN", "HABILIDADES");
        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "cv.docx",
            TraceId: "trace-1");

        var result = _parser.Parse(command);

        var legacy = result.Should().BeOfType<LegacyImportResult>().Subject;
        legacy.Text.Should().Contain("EXPERIENCIA");
        legacy.Text.Should().Contain("EDUCACIÓN");
        legacy.Text.Should().Contain("HABILIDADES");
        legacy.EngineVersion.Should().Be("1.0.0");
        legacy.TraceId.Should().Be("trace-1");
        legacy.Sections.Select(s => s.Heading).Should().Contain("EXPERIENCIA");
    }

    [Fact]
    public void Should_throw_ParserEngineException_for_garbage_bytes()
    {
        var command = new ImportCvCommand(
            FileBytes: [0x47, 0x49, 0x46, 0x38, 0x39, 0x61],
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "fake.docx",
            TraceId: "trace-1");

        var act = () => _parser.Parse(command);

        act.Should().Throw<ParserEngineException>()
            .Which.Code.Should().Be("INVALID_DOCX");
    }

    [Fact]
    public void Should_throw_ParserEngineException_for_empty_bytes()
    {
        var command = new ImportCvCommand(
            FileBytes: [],
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "empty.docx",
            TraceId: "trace-1");

        var act = () => _parser.Parse(command);

        act.Should().Throw<ParserEngineException>()
            .Which.Code.Should().Be("EMPTY_FILE");
    }
}
