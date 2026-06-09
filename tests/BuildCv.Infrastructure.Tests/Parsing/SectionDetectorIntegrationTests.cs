using BuildCv.Application.Features.Import;
using BuildCv.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace BuildCv.Infrastructure.Tests.Parsing;

public sealed class SectionDetectorIntegrationTests
{
    [Fact]
    public void Should_emit_warnings_when_no_sections_detected()
    {
        var text = "Juan Pérez sin headers reconocibles en mayúsculas.";
        var sections = SectionDetector.Detect(text);

        sections.Should().BeEmpty();
    }

    [Fact]
    public void Should_truncate_text_over_50000_chars_and_emit_warning()
    {
        var huge = new string('a', 50_001);

        var sections = SectionDetector.Detect(huge);
        sections.Should().BeEmpty();
    }
}
