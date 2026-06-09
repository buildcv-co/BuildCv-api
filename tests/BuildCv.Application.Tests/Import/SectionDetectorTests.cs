using BuildCv.Application.Features.Import;
using FluentAssertions;
using Xunit;

namespace BuildCv.Application.Tests.Import;

public sealed class SectionDetectorTests
{
    [Fact]
    public void Should_detect_spanish_headers_with_high_confidence()
    {
        var text = """
            Juan Pérez
            Backend Developer

            EXPERIENCIA
            Acme Corp 2020-2024

            EDUCACIÓN
            Universidad Nacional 2015-2019

            HABILIDADES
            C#, .NET
            """;

        var sections = SectionDetector.Detect(text);

        sections.Should().HaveCount(3);
        sections[0].Heading.Should().Be("EXPERIENCIA");
        sections[0].Confidence.Should().Be(SectionDetector.ConfidenceHigh);
        sections[1].Heading.Should().Be("EDUCACIÓN");
        sections[1].Confidence.Should().Be(SectionDetector.ConfidenceHigh);
        sections[2].Heading.Should().Be("HABILIDADES");
        sections[2].Confidence.Should().Be(SectionDetector.ConfidenceHigh);
    }

    [Fact]
    public void Should_detect_english_headers_with_high_confidence()
    {
        var text = """
            John Doe
            Backend Developer

            EXPERIENCE
            Acme Corp 2020-2024

            EDUCATION
            University 2015-2019

            SKILLS
            C#, .NET
            """;

        var sections = SectionDetector.Detect(text);

        sections.Should().HaveCount(3);
        sections.Select(s => s.Heading).Should().Contain(["EXPERIENCE", "EDUCATION", "SKILLS"]);
        sections.Should().OnlyContain(s => s.Confidence == SectionDetector.ConfidenceHigh);
    }

    [Fact]
    public void Should_return_empty_when_no_headers_found()
    {
        var text = "Juan Pérez sin headers en mayúsculas. Solo texto narrativo sobre su carrera.";

        var sections = SectionDetector.Detect(text);

        sections.Should().BeEmpty();
    }

    [Fact]
    public void Should_match_only_clean_header_lines_and_ignore_padded_lines()
    {
        const string text = "EXPERIENCIA LABORAL\nAlgo de texto\nEDUCACIÓN\nOtro texto";

        var sections = SectionDetector.Detect(text);

        sections.Should().HaveCount(1);
        sections[0].Heading.Should().Be("EDUCACIÓN");
    }

    [Fact]
    public void Should_return_correct_start_and_end_indices_pointing_after_match()
    {
        const string text = "intro\nEXPERIENCIA\ncontenido\nEDUCACIÓN\nresto";

        var sections = SectionDetector.Detect(text);

        sections.Should().HaveCount(2);
        sections[0].Heading.Should().Be("EXPERIENCIA");
        sections[0].Start.Should().Be(text.IndexOf("EXPERIENCIA") + "EXPERIENCIA".Length);
        sections[0].End.Should().Be(text.IndexOf("EDUCACIÓN"));
        sections[1].Heading.Should().Be("EDUCACIÓN");
        sections[1].Start.Should().Be(text.IndexOf("EDUCACIÓN") + "EDUCACIÓN".Length);
        sections[1].End.Should().Be(text.Length);
    }

    [Fact]
    public void Should_handle_empty_text_gracefully()
    {
        SectionDetector.Detect(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Should_handle_whitespace_only_text_gracefully()
    {
        SectionDetector.Detect("   \n\t  \n  ").Should().BeEmpty();
    }

    [Fact]
    public void Should_handle_null_text_gracefully()
    {
        SectionDetector.Detect(null).Should().BeEmpty();
    }

    [Fact]
    public void Should_not_match_lowercase_headers()
    {
        const string text = "experiencia\ncontenido";

        SectionDetector.Detect(text).Should().BeEmpty();
    }

    [Fact]
    public void Should_match_headers_with_trailing_colon()
    {
        const string text = "EXPERIENCIA:\ntrabajos varios";

        var sections = SectionDetector.Detect(text);

        sections.Should().HaveCount(1);
        sections[0].Heading.Should().Be("EXPERIENCIA");
        sections[0].Confidence.Should().Be(SectionDetector.ConfidenceHigh);
    }
}
