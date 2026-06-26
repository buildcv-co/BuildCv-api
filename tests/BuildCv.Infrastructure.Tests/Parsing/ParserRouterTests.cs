using BuildCv.Application.Features.Import;
using BuildCv.Domain.Resumes;
using BuildCv.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace BuildCv.Infrastructure.Tests.Parsing;

/// <summary>
/// Tests for <see cref="ParserRouter"/> engineVersion dispatch (micro-batch 2d of
/// change 021 — Structured CV Import + Mandatory Job Spec).
///
/// The router is the SINGLE entry point that all callers (web BFF, future endpoints)
/// use to convert a CV file into a <see cref="ParseResult"/> discriminated union.
/// It dispatches based on <c>command.EngineVersion</c>:
/// <list type="bullet">
///   <item><c>"1.0.0"</c> (or null/absent): route to a legacy <see cref="ICvParser"/>
///     for the declared MIME; wrap the <see cref="ImportResult"/> via
///     <see cref="LegacyParserAdapter"/> and return a <see cref="RawParseResult"/>.</item>
///   <item><c>"2.0.0"</c>: route to a structured <see cref="IStructuredParser"/>
///     for the declared MIME; return the <see cref="StructuredParseResult"/> as-is.</item>
///   <item>Any other value: throw <see cref="InvalidOperationException"/> (guard against typos).</item>
/// </list>
///
/// Test doubles (not real PdfPig / OpenXml) — the integration between router and
/// concrete parsers is covered separately in <c>PdfPigCvParserStructuredTests</c>
/// and <c>OpenXmlCvParserStructuredTests</c>.
/// Constitution: Art. I (no invented data — the router only routes; the parsers do the work),
/// Art. II (deterministic dispatch — same input + same engineVersion ⇒ same parser),
/// Art. VI (router is the unique entry point that the Application layer depends on).
/// </summary>
public sealed class ParserRouterTests
{
    private const string PdfMime = "application/pdf";
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string TraceId = "trace-2d-router";

    [Fact]
    public void Parse_CommandWithoutEngineVersion_Defaults_To_1_0_0_And_Returns_RawParseResult()
    {
        var legacy = new StubLegacyParser(PdfMime, "legacy-pdf-text");
        var structured = new StubStructuredParser(PdfMime, CvDocumentFixture());
        var router = new ParserRouter(new[] { legacy }, new[] { structured });

        var command = new ImportCvCommand(
            FileBytes: [0x25, 0x50, 0x44, 0x46, 0x2D],
            MimeType: PdfMime,
            OriginalFileName: "cv.pdf",
            TraceId: TraceId);

        var result = router.Parse(command);

        result.Should().BeOfType<RawParseResult>();
        result.EngineVersion.Should().Be("1.0.0");
        result.ToRawText().Should().Be("legacy-pdf-text");
        legacy.Calls.Should().Be(1);
        structured.Calls.Should().Be(0);
    }

    [Fact]
    public void Parse_CommandWithEngineVersion_1_0_0_Returns_RawParseResult()
    {
        var legacy = new StubLegacyParser(DocxMime, "legacy-docx-text");
        var structured = new StubStructuredParser(DocxMime, CvDocumentFixture());
        var router = new ParserRouter(new[] { legacy }, new[] { structured });

        var command = new ImportCvCommand(
            FileBytes: [0x50, 0x4B, 0x03, 0x04],
            MimeType: DocxMime,
            OriginalFileName: "cv.docx",
            TraceId: TraceId,
            EngineVersion: "1.0.0");

        var result = router.Parse(command);

        result.Should().BeOfType<RawParseResult>();
        result.EngineVersion.Should().Be("1.0.0");
        result.ToRawText().Should().Be("legacy-docx-text");
        legacy.Calls.Should().Be(1);
        structured.Calls.Should().Be(0);
    }

    [Fact]
    public void Parse_CommandWithEngineVersion_2_0_0_Returns_StructuredParseResult()
    {
        var legacy = new StubLegacyParser(PdfMime, "legacy-pdf-text");
        var cv = CvDocumentFixture();
        var structured = new StubStructuredParser(PdfMime, cv);
        var router = new ParserRouter(new[] { legacy }, new[] { structured });

        var command = new ImportCvCommand(
            FileBytes: [0x25, 0x50, 0x44, 0x46, 0x2D],
            MimeType: PdfMime,
            OriginalFileName: "cv.pdf",
            TraceId: TraceId,
            EngineVersion: "2.0.0");

        var result = router.Parse(command);

        var structuredResult = result.Should().BeOfType<StructuredParseResult>().Subject;
        structuredResult.EngineVersion.Should().Be("2.0.0");
        structuredResult.Cv.Should().BeSameAs(cv);
        legacy.Calls.Should().Be(0);
        structured.Calls.Should().Be(1);
    }

    [Fact]
    public void Parse_WithEngineVersion_1_0_0_Routes_To_Legacy_Even_If_Structured_Would_Throw()
    {
        // Design decision (documented per spec): the v1 path ONLY uses legacy parsers.
        // There is no implicit "try structured first, fall back to legacy" semantics for v1;
        // v1 is the legacy path by definition. This test pins that contract: the structured
        // parser is NEVER invoked when engineVersion="1.0.0", regardless of what it would
        // do if called. The v2 path, by contrast, has no fallback (a failed structured parse
        // propagates the ParserEngineException up to the caller, which the endpoint maps
        // to HTTP 503 per the import error code table).
        var legacy = new StubLegacyParser(PdfMime, "legacy-pdf-text");
        var structured = new ThrowingStructuredParser(
            new ParserEngineException("UNSUPPORTED_MIME", "structured would throw"));
        var router = new ParserRouter(new[] { legacy }, new[] { structured });

        var command = new ImportCvCommand(
            FileBytes: [0x25, 0x50, 0x44, 0x46, 0x2D],
            MimeType: PdfMime,
            OriginalFileName: "cv.pdf",
            TraceId: TraceId,
            EngineVersion: "1.0.0");

        var result = router.Parse(command);

        result.Should().BeOfType<RawParseResult>();
        result.ToRawText().Should().Be("legacy-pdf-text");
        structured.Calls.Should().Be(0);
        legacy.Calls.Should().Be(1);
    }

    [Fact]
    public void Parse_UnknownEngineVersion_Throws_InvalidOperationException()
    {
        var legacy = new StubLegacyParser(PdfMime, "legacy-pdf-text");
        var structured = new StubStructuredParser(PdfMime, CvDocumentFixture());
        var router = new ParserRouter(new[] { legacy }, new[] { structured });

        var command = new ImportCvCommand(
            FileBytes: [0x25, 0x50, 0x44, 0x46, 0x2D],
            MimeType: PdfMime,
            OriginalFileName: "cv.pdf",
            TraceId: TraceId,
            EngineVersion: "3.0.0-rc1");

        var act = () => router.Parse(command);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported engineVersion '3.0.0-rc1'*")
            .WithMessage("*1.0.0*")
            .WithMessage("*2.0.0*");
        legacy.Calls.Should().Be(0);
        structured.Calls.Should().Be(0);
    }

    private static CvDocument CvDocumentFixture() => new(
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

    private sealed class StubLegacyParser : ICvParser, IKnownMimeParser
    {
        private readonly string _servedMime;
        private readonly string _text;

        public StubLegacyParser(string servedMime, string text)
        {
            _servedMime = servedMime;
            _text = text;
        }

        public int Calls { get; private set; }

        public string SupportedMimeType => _servedMime;

        public ImportResult Parse(ImportCvCommand command)
        {
            Calls++;
            if (!string.Equals(command.MimeType, _servedMime, StringComparison.OrdinalIgnoreCase))
            {
                throw new ParserEngineException(
                    "UNSUPPORTED_MIME",
                    $"StubLegacyParser only serves '{_servedMime}', got '{command.MimeType}'.");
            }

            return new LegacyImportResult(
                text: _text,
                sections: Array.Empty<ImportSection>(),
                warnings: Array.Empty<ImportWarning>(),
                traceId: command.TraceId);
        }
    }

    private sealed class StubStructuredParser : IStructuredParser, IKnownMimeParser
    {
        private readonly string _servedMime;
        private readonly CvDocument _cv;

        public StubStructuredParser(string servedMime, CvDocument cv)
        {
            _servedMime = servedMime;
            _cv = cv;
        }

        public int Calls { get; private set; }

        public string SupportedMimeType => _servedMime;

        public ParseResult Parse(ImportCvCommand command)
        {
            Calls++;
            if (!string.Equals(command.MimeType, _servedMime, StringComparison.OrdinalIgnoreCase))
            {
                throw new ParserEngineException(
                    "UNSUPPORTED_MIME",
                    $"StubStructuredParser only serves '{_servedMime}', got '{command.MimeType}'.");
            }

            return new StructuredParseResult(_cv, Array.Empty<ParsingWarning>());
        }
    }

    private sealed class ThrowingStructuredParser : IStructuredParser
    {
        private readonly ParserEngineException _exception;

        public ThrowingStructuredParser(ParserEngineException exception)
        {
            _exception = exception;
        }

        public int Calls { get; private set; }

        public ParseResult Parse(ImportCvCommand command)
        {
            Calls++;
            throw _exception;
        }
    }
}
