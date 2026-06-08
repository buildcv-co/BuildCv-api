using BuildCv.Application.Features.Export;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace BuildCv.Application.Tests.Export;

public sealed class ExportPdfValidatorTests
{
    private readonly ExportPdfValidator _validator = new();

    [Fact]
    public void Should_reject_empty_adapted_cv()
    {
        var cmd = new ExportPdfCommand("", ValidReport(), "Name");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.AdaptedCv);
    }

    [Fact]
    public void Should_reject_cv_over_50000_chars()
    {
        var cmd = new ExportPdfCommand(new string('a', 50_001), ValidReport(), "Name");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.AdaptedCv);
    }

    [Fact]
    public void Should_reject_candidate_name_over_100_chars()
    {
        var cmd = new ExportPdfCommand("valid cv", ValidReport(), new string('n', 101));
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.CandidateName);
    }

    [Fact]
    public void Should_accept_valid_input()
    {
        var cmd = new ExportPdfCommand("valid cv content", ValidReport(), "Juan Pérez");
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_accept_default_candidate_name()
    {
        var cmd = new ExportPdfCommand("valid cv", ValidReport(), "");
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }

    private static BuildCv.Domain.Adapt.ValidationReport ValidReport() =>
        new(true, BuildCv.Domain.Adapt.Severity.None, Array.Empty<BuildCv.Domain.Adapt.EntityInvention>(), Array.Empty<string>());
}
