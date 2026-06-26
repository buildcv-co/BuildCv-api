using BuildCv.Application.Features.Import;
using BuildCv.Domain.Common;
using FluentAssertions;
using Xunit;

namespace BuildCv.Application.Tests.Import;

public sealed class ImportCvHandlerTests
{
    private const string TraceId = "test-trace-1";

    [Fact]
    public async Task Should_call_router_and_return_success_when_router_returns_raw_result()
    {
        var router = new FakeRouter(legacyText: "Fake parsed text");
        var handler = new ImportCvHandler(router, new ImportCvValidator());
        var command = new ImportCvCommand(
            FileBytes: "%PDF-1.4 fake"u8.ToArray(),
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: TraceId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be("Fake parsed text");
        result.Value.EngineVersion.Should().Be("1.0.0");
        result.Value.TraceId.Should().Be(TraceId);
        router.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Should_call_router_and_return_success_when_router_returns_structured_result()
    {
        var router = new FakeRouter(structured: MakeStructured());
        var handler = new ImportCvHandler(router, new ImportCvValidator());
        var command = new ImportCvCommand(
            FileBytes: "%PDF-1.4 fake"u8.ToArray(),
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: TraceId,
            EngineVersion: "2.0.0");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EngineVersion.Should().Be("2.0.0");
        result.Value.TraceId.Should().Be(TraceId);
        result.Value.Text.Should().Contain("Ada Lovelace");
        router.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Should_return_failure_with_code_when_router_throws_ParserEngineException_for_encrypted_pdf()
    {
        var router = new FakeRouter(throwCode: "PDF_ENCRYPTED");
        var handler = new ImportCvHandler(router, new ImportCvValidator());
        var command = new ImportCvCommand(
            FileBytes: "%PDF-1.4 encrypted"u8.ToArray(),
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: TraceId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IMPORT_PDF_ENCRYPTED");
    }

    [Fact]
    public async Task Should_return_failure_with_code_when_router_throws_ParserEngineException_for_scanned_pdf()
    {
        var router = new FakeRouter(throwCode: "SCANNED_PDF");
        var handler = new ImportCvHandler(router, new ImportCvValidator());
        var command = new ImportCvCommand(
            FileBytes: "%PDF-1.4 scanned"u8.ToArray(),
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: TraceId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IMPORT_SCANNED_PDF");
    }

    [Fact]
    public async Task Should_return_failure_with_code_when_router_throws_ParserEngineException_for_protected_docx()
    {
        var router = new FakeRouter(throwCode: "DOCX_PROTECTED");
        var handler = new ImportCvHandler(router, new ImportCvValidator());
        var command = new ImportCvCommand(
            FileBytes: "PK fake"u8.ToArray(),
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "cv.docx",
            TraceId: TraceId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IMPORT_DOCX_PROTECTED");
    }

    [Fact]
    public async Task Should_return_failure_with_code_when_router_throws_ParserEngineException_for_too_many_pages()
    {
        var router = new FakeRouter(throwCode: "TOO_MANY_PAGES");
        var handler = new ImportCvHandler(router, new ImportCvValidator());
        var command = new ImportCvCommand(
            FileBytes: "%PDF-1.4 long"u8.ToArray(),
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: TraceId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IMPORT_TOO_MANY_PAGES");
    }

    [Fact]
    public async Task Should_wrap_generic_exception_as_IMPORT_ENGINE_ERROR()
    {
        var router = new FakeRouter(throwGeneric: true);
        var handler = new ImportCvHandler(router, new ImportCvValidator());
        var command = new ImportCvCommand(
            FileBytes: "%PDF-1.4 oops"u8.ToArray(),
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: TraceId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IMPORT_ENGINE_ERROR");
    }

    [Fact]
    public async Task Should_return_failure_with_IMPORT_VALIDATION_when_file_name_is_empty()
    {
        var router = new FakeRouter();
        var handler = new ImportCvHandler(router, new ImportCvValidator());
        var command = new ImportCvCommand(
            FileBytes: "%PDF-1.4 x"u8.ToArray(),
            MimeType: "application/pdf",
            OriginalFileName: "",
            TraceId: TraceId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IMPORT_VALIDATION");
        router.Calls.Should().Be(0);
    }

    private static BuildCv.Domain.Resumes.CvDocument MakeStructured() => new(
        Basics: new BuildCv.Domain.Resumes.Basics(
            Name: "Ada Lovelace",
            Email: "ada@example.com",
            Phone: null,
            Location: null,
            Url: null,
            Profiles: Array.Empty<BuildCv.Domain.Resumes.ResumeProfile>(),
            Summary: null,
            DatosPersonales: null,
            Confidence: new BuildCv.Domain.Resumes.BasicsConfidence(
                Name: BuildCv.Domain.Resumes.ConfidenceMarker.Inferred,
                Email: BuildCv.Domain.Resumes.ConfidenceMarker.Inferred,
                Phone: BuildCv.Domain.Resumes.ConfidenceMarker.Inferred,
                Location: BuildCv.Domain.Resumes.ConfidenceMarker.Inferred,
                Url: BuildCv.Domain.Resumes.ConfidenceMarker.Inferred,
                Profiles: BuildCv.Domain.Resumes.ConfidenceMarker.Inferred,
                Summary: BuildCv.Domain.Resumes.ConfidenceMarker.Inferred,
                DatosPersonales: BuildCv.Domain.Resumes.ConfidenceMarker.Inferred)),
        Work: Array.Empty<BuildCv.Domain.Resumes.TaggedResumeWork>(),
        Education: Array.Empty<BuildCv.Domain.Resumes.TaggedResumeEducation>(),
        Skills: Array.Empty<BuildCv.Domain.Resumes.TaggedResumeSkill>(),
        Projects: Array.Empty<BuildCv.Domain.Resumes.TaggedResumeProject>(),
        Certificates: Array.Empty<BuildCv.Domain.Resumes.TaggedResumeCertificate>(),
        Languages: Array.Empty<BuildCv.Domain.Resumes.TaggedResumeLanguage>(),
        Meta: new BuildCv.Domain.Resumes.CvMeta(EngineVersion: "2.0.0"));

    private sealed class FakeRouter : IParserRouter
    {
        private readonly string? _legacyText;
        private readonly string? _throwCode;
        private readonly bool _throwGeneric;
        private readonly BuildCv.Domain.Resumes.CvDocument? _structured;

        public FakeRouter(
            string? legacyText = null,
            string? throwCode = null,
            bool throwGeneric = false,
            BuildCv.Domain.Resumes.CvDocument? structured = null)
        {
            _legacyText = legacyText;
            _throwCode = throwCode;
            _throwGeneric = throwGeneric;
            _structured = structured;
        }

        public int Calls { get; private set; }

        public ParseResult Parse(ImportCvCommand command)
        {
            Calls++;
            if (_throwGeneric)
            {
                throw new InvalidOperationException("engine exploded");
            }

            if (_throwCode is not null)
            {
                throw new ParserEngineException(_throwCode, $"Simulated {_throwCode} failure");
            }

            if (_structured is not null)
            {
                return new StructuredParseResult(_structured, Array.Empty<ParsingWarning>());
            }

            return new RawParseResult(
                Text: _legacyText ?? "Fake parsed text",
                Warnings: Array.Empty<ParsingWarning>());
        }
    }
}
