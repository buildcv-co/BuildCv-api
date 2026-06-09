using BuildCv.Application.Features.Import;
using BuildCv.Domain.Common;
using FluentAssertions;
using Xunit;

namespace BuildCv.Application.Tests.Import;

public sealed class ImportCvHandlerTests
{
    private const string PdfBytes = "fake-pdf-bytes";
    private const string TraceId = "test-trace-1";

    [Fact]
    public async Task Should_call_parser_and_return_success_when_parser_returns_result()
    {
        var parser = new FakeParser();
        var handler = new ImportCvHandler(parser, new ImportCvValidator());
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
        parser.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Should_return_failure_with_code_when_parser_throws_ParserEngineException_for_encrypted_pdf()
    {
        var parser = new FakeParser(throwCode: "PDF_ENCRYPTED");
        var handler = new ImportCvHandler(parser, new ImportCvValidator());
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
    public async Task Should_return_failure_with_code_when_parser_throws_ParserEngineException_for_scanned_pdf()
    {
        var parser = new FakeParser(throwCode: "SCANNED_PDF");
        var handler = new ImportCvHandler(parser, new ImportCvValidator());
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
    public async Task Should_return_failure_with_code_when_parser_throws_ParserEngineException_for_protected_docx()
    {
        var parser = new FakeParser(throwCode: "DOCX_PROTECTED");
        var handler = new ImportCvHandler(parser, new ImportCvValidator());
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
    public async Task Should_return_failure_with_code_when_parser_throws_ParserEngineException_for_too_many_pages()
    {
        var parser = new FakeParser(throwCode: "TOO_MANY_PAGES");
        var handler = new ImportCvHandler(parser, new ImportCvValidator());
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
        var parser = new FakeParser(throwGeneric: true);
        var handler = new ImportCvHandler(parser, new ImportCvValidator());
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
        var parser = new FakeParser();
        var handler = new ImportCvHandler(parser, new ImportCvValidator());
        var command = new ImportCvCommand(
            FileBytes: "%PDF-1.4 x"u8.ToArray(),
            MimeType: "application/pdf",
            OriginalFileName: "",
            TraceId: TraceId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IMPORT_VALIDATION");
        parser.Calls.Should().Be(0);
    }

    private sealed class FakeParser : ICvParser
    {
        private readonly string? _throwCode;
        private readonly bool _throwGeneric;

        public FakeParser(string? throwCode = null, bool throwGeneric = false)
        {
            _throwCode = throwCode;
            _throwGeneric = throwGeneric;
        }

        public int Calls { get; private set; }

        public ImportResult Parse(ImportCvCommand command)
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
            return new ImportResult(
                Text: "Fake parsed text",
                Sections: [],
                Warnings: [],
                EngineVersion: "1.0.0",
                TraceId: command.TraceId);
        }
    }
}
