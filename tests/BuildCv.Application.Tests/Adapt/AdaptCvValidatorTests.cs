using BuildCv.Application.Features.Adapt;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace BuildCv.Application.Tests.Adapt;

public sealed class AdaptCvValidatorTests
{
    private readonly AdaptCvValidator _validator = new();

    [Fact]
    public void Should_reject_empty_cv()
    {
        var cmd = new AdaptCvCommand("", "some job text");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.CvText);
    }

    [Fact]
    public void Should_reject_cv_over_50000_chars()
    {
        var cmd = new AdaptCvCommand(new string('a', 50_001), "job");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.CvText);
    }

    [Fact]
    public void Should_reject_empty_job()
    {
        var cmd = new AdaptCvCommand("some cv", "");
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.JobText);
    }

    [Fact]
    public void Should_reject_job_over_20000_chars()
    {
        var cmd = new AdaptCvCommand("cv", new string('j', 20_001));
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(c => c.JobText);
    }

    [Fact]
    public void Should_reject_identical_cv_and_job()
    {
        var cmd = new AdaptCvCommand("same text", "same text");
        var result = _validator.TestValidate(cmd);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Should_accept_valid_input()
    {
        var cmd = new AdaptCvCommand("valid cv", "valid job");
        var result = _validator.TestValidate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
