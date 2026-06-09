using BuildCv.Application.Features.Export;
using BuildCv.Domain.Adapt;
using BuildCv.Domain.Common;
using BuildCv.Domain.Export;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BuildCv.Application.Tests.Export;

public sealed class ExportPdfHandlerTests
{
    private readonly FakePdfGenerator _generator = new();
    private readonly ValidationGate _gate = new();
    private readonly ExportPdfHandler _handler;

    public ExportPdfHandlerTests()
    {
        _handler = new ExportPdfHandler(_generator, _gate, NullLogger<ExportPdfHandler>.Instance);
    }

    [Fact]
    public async Task Should_return_pdf_bytes_with_metadata_on_valid_input()
    {
        var report = new ValidationReport(true, Severity.None, Array.Empty<EntityInvention>(), Array.Empty<string>());
        var cmd = new ExportPdfCommand("CV content here", report, "Juan Pérez");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Pdf.Length.Should().BeGreaterThan(0);
        result.Value.Filename.Should().StartWith("cv-adapted-").And.EndWith(".pdf");
        result.Value.Metadata.EngineVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Should_block_export_when_hard_invention_present()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Company, "FakeCorp", null, InventionSeverity.Hard, 0)
        };
        var report = new ValidationReport(false, Severity.Critical, inventions, new[] { "1 hard" });
        var cmd = new ExportPdfCommand("CV", report, "Test");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("EXPORT_BLOCKED_INVENTION");
        result.Error.Message.Should().Contain("FakeCorp");
    }

    [Fact]
    public async Task Should_allow_export_with_warning_severity()
    {
        var inventions = new[]
        {
            new EntityInvention(InventionType.Skill, "AWS", null, InventionSeverity.Soft, 0)
        };
        var report = new ValidationReport(true, Severity.Warning, inventions, new[] { "1 soft" });
        var cmd = new ExportPdfCommand("CV", report, "Test");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Severity.Should().Be(Severity.Warning);
    }

    [Fact]
    public async Task Should_propagate_generator_exception_as_failure()
    {
        _generator.ShouldThrow = new InvalidOperationException("QuestPDF failed");
        var report = new ValidationReport(true, Severity.None, Array.Empty<EntityInvention>(), Array.Empty<string>());
        var cmd = new ExportPdfCommand("CV", report, "Test");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PDF_UNAVAILABLE");
    }

    [Fact]
    public async Task Should_use_default_candidate_name_when_empty()
    {
        var report = new ValidationReport(true, Severity.None, Array.Empty<EntityInvention>(), Array.Empty<string>());
        var cmd = new ExportPdfCommand("CV", report, "");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}

internal sealed class FakePdfGenerator : IPdfGenerator
{
    public byte[] Response { get; set; } = "%PDF-1.4\n%fake\n%%EOF"u8.ToArray();
    public Exception? ShouldThrow { get; set; }

    public byte[] GeneratePdf(ExportRequest request)
    {
        if (ShouldThrow is not null)
        {
            throw ShouldThrow;
        }
        return Response;
    }
}
