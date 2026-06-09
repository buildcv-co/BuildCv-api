using BuildCv.Application.Features.Import;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace BuildCv.Application.Tests.Import;

public sealed class ImportCvValidatorTests
{
    private readonly ImportCvValidator _validator = new();

    [Fact]
    public void Should_reject_empty_original_file_name()
    {
        var command = new ImportCvCommand(
            FileBytes: new byte[] { 0x25, 0x50, 0x44, 0x46 },
            MimeType: "application/pdf",
            OriginalFileName: "",
            TraceId: "trace-1");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.OriginalFileName);
    }

    [Fact]
    public void Should_reject_empty_mime_type()
    {
        var command = new ImportCvCommand(
            FileBytes: [0x25, 0x50, 0x44, 0x46],
            MimeType: "",
            OriginalFileName: "cv.pdf",
            TraceId: "trace-1");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.MimeType);
    }

    [Fact]
    public void Should_accept_valid_command()
    {
        var command = new ImportCvCommand(
            FileBytes: [0x25, 0x50, 0x44, 0x46],
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: "trace-1");

        var result = _validator.TestValidate(command);

        result.IsValid.Should().BeTrue();
    }
}
