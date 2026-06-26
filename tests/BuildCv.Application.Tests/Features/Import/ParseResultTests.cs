using BuildCv.Application.Features.Import;
using BuildCv.Domain.Resumes;
using FluentAssertions;
using Xunit;

namespace BuildCv.Application.Tests.Features.Import;

/// <summary>
/// Tests for the <see cref="ParseResult"/> discriminated union introduced by
/// change 021 (micro-batch 2a). The contract is a sealed algebraic data type
/// with exactly two variants:
///   - <see cref="RawParseResult"/>: legacy parsers that emit plain text (engineVersion 1.0.0).
///   - <see cref="StructuredParseResult"/>: parsers that emit a typed <see cref="CvDocument"/>
///     (engineVersion 2.0.0).
/// The abstract <see cref="ParseResult"/> exposes three members that each variant
/// must implement: <c>ToCvDocument()</c>, <c>ToRawText()</c> and
/// <c>EngineVersion</c>. Cross-variant accessors throw <see cref="InvalidOperationException"/>
/// so the caller is forced to opt in to the correct engineVersion.
///
/// Constitution: Art. I (no invented data — confidence markers live inside the
/// CvDocument; this union just decides whether the parser emitted text or a
/// structured document) and Art. II (deterministic, pure data records).
/// </summary>
public sealed class ParseResultTests
{
    [Fact]
    public void ParseResult_RawParseResult_Holds_Text_And_Warnings()
    {
        var warnings = new List<ParsingWarning>
        {
            new("TEXT_TRUNCATED", "Truncated at 50000 chars.", "Warning"),
        };

        var result = new RawParseResult("plain text payload", warnings);

        result.Text.Should().Be("plain text payload");
        result.Warnings.Should().BeSameAs(warnings);
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Code.Should().Be("TEXT_TRUNCATED");
        result.Warnings[0].Severity.Should().Be("Warning");
    }

    [Fact]
    public void ParseResult_RawParseResult_Accepts_Empty_Warnings()
    {
        var result = new RawParseResult("text", Array.Empty<ParsingWarning>());

        result.Text.Should().Be("text");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ParseResult_StructuredParseResult_Holds_CvDocument_And_Warnings()
    {
        var cv = CreateMinimalCvDocument();
        var warnings = new List<ParsingWarning>
        {
            new("OCR_LOW_CONFIDENCE", "Some lines had low OCR confidence.", "Info"),
        };

        var result = new StructuredParseResult(cv, warnings);

        result.Cv.Should().BeSameAs(cv);
        result.Cv.Basics.Name.Should().Be("Ada Lovelace");
        result.Warnings.Should().BeSameAs(warnings);
        result.Warnings.Should().HaveCount(1);
    }

    [Fact]
    public void ParseResult_StructuredParseResult_Exposes_EngineVersion()
    {
        var result = new StructuredParseResult(CreateMinimalCvDocument(), Array.Empty<ParsingWarning>());

        result.EngineVersion.Should().Be("2.0.0");
    }

    [Fact]
    public void ParseResult_RawParseResult_Exposes_EngineVersion()
    {
        var result = new RawParseResult("text", Array.Empty<ParsingWarning>());

        result.EngineVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void ParseResult_ToCvDocument_On_Raw_Throws_InvalidOperationException()
    {
        var raw = new RawParseResult("text", Array.Empty<ParsingWarning>());

        var act = () => raw.ToCvDocument();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*engineVersion 2.0.0*");
    }

    [Fact]
    public void ParseResult_ToRawText_On_Structured_Throws_InvalidOperationException()
    {
        var structured = new StructuredParseResult(CreateMinimalCvDocument(), Array.Empty<ParsingWarning>());

        var act = () => structured.ToRawText();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ToCvDocument()*");
    }

    [Fact]
    public void ParseResult_ToRawText_On_Raw_Returns_The_Original_Text()
    {
        var raw = new RawParseResult("hello world", Array.Empty<ParsingWarning>());

        raw.ToRawText().Should().Be("hello world");
    }

    [Fact]
    public void ParseResult_ToCvDocument_On_Structured_Returns_The_Structured_Cv()
    {
        var cv = CreateMinimalCvDocument();
        var structured = new StructuredParseResult(cv, Array.Empty<ParsingWarning>());

        structured.ToCvDocument().Should().BeSameAs(cv);
    }

    [Fact]
    public void ICvParser_Contract_Returns_ParseResult_Not_Legacy_ImportResult()
    {
        IStructuredParser parser = new MockStructuredParser();

        var command = new ImportCvCommand(
            FileBytes: "%PDF-1.4"u8.ToArray(),
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: "test-trace-2a");

        ParseResult result = parser.Parse(command);

        result.Should().BeOfType<RawParseResult>();
        result.EngineVersion.Should().Be("1.0.0");
    }

    private static CvDocument CreateMinimalCvDocument() => new(
        Basics: new Basics(
            Name: "Ada Lovelace",
            Email: "ada@example.com",
            Phone: null,
            Location: null,
            Url: null,
            Profiles: Array.Empty<ResumeProfile>(),
            Summary: null,
            DatosPersonales: null,
            Confidence: new BasicsConfidence(
                Name: ConfidenceMarker.Inferred,
                Email: ConfidenceMarker.Inferred,
                Phone: ConfidenceMarker.Inferred,
                Location: ConfidenceMarker.Inferred,
                Url: ConfidenceMarker.Inferred,
                Profiles: ConfidenceMarker.Inferred,
                Summary: ConfidenceMarker.Inferred,
                DatosPersonales: ConfidenceMarker.Inferred)),
        Work: Array.Empty<TaggedResumeWork>(),
        Education: Array.Empty<TaggedResumeEducation>(),
        Skills: Array.Empty<TaggedResumeSkill>(),
        Projects: Array.Empty<TaggedResumeProject>(),
        Certificates: Array.Empty<TaggedResumeCertificate>(),
        Languages: Array.Empty<TaggedResumeLanguage>(),
        Meta: new CvMeta(EngineVersion: "2.0.0"));

    private sealed class MockStructuredParser : IStructuredParser
    {
        public ParseResult Parse(ImportCvCommand command)
        {
            return new RawParseResult(
                Text: $"parsed:{command.OriginalFileName}",
                Warnings: Array.Empty<ParsingWarning>());
        }
    }
}
