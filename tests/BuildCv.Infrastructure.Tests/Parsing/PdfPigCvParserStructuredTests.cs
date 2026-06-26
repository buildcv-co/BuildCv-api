using BuildCv.Application.Features.Import;
using BuildCv.Domain.Resumes;
using BuildCv.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace BuildCv.Infrastructure.Tests.Parsing;

/// <summary>
/// Tests for <see cref="PdfPigCvParser"/> as an <see cref="IStructuredParser"/>.
/// Micro-batch 2b of change 021 — the parser MUST emit a <see cref="StructuredParseResult"/>
/// (engineVersion 2.0.0) carrying a typed <see cref="CvDocument"/> with per-field
/// <see cref="ConfidenceMarker"/> tags. Constitution Art. I: parsers only emit
/// <c>inferred</c> or <c>explicit</c>; <c>user_confirmed</c> is editor-only (PR 4).
/// </summary>
public sealed class PdfPigCvParserStructuredTests
{
    private readonly IStructuredParser _parser = new PdfPigCvParser();

    [Fact]
    public void Parse_ValidPdfText_Emits_StructuredParseResult_With_InferredConfidence()
    {
        var bytes = PdfTestFixtures.CreateMultiPageCvPdf();
        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/pdf",
            OriginalFileName: "cv.pdf",
            TraceId: "trace-2b-structured");

        var result = _parser.Parse(command);

        var structured = result.Should().BeOfType<StructuredParseResult>().Subject;
        structured.Cv.Basics.Should().NotBeNull();
        structured.Cv.Basics.Name.Should().NotBeNullOrWhiteSpace();
        structured.Cv.Basics.Email.Should().NotBeNullOrWhiteSpace();
        structured.Cv.Basics.Confidence.Name.Should().Be(ConfidenceMarker.Inferred);
        structured.Cv.Basics.Confidence.Email.Should().Be(ConfidenceMarker.Explicit);
        structured.Cv.Basics.Confidence.Phone.Should().NotBe(ConfidenceMarker.UserConfirmed);
        structured.Cv.Basics.Confidence.Url.Should().NotBe(ConfidenceMarker.UserConfirmed);
        structured.Cv.Basics.Confidence.Location.Should().NotBe(ConfidenceMarker.UserConfirmed);
        structured.Cv.Basics.Confidence.Profiles.Should().NotBe(ConfidenceMarker.UserConfirmed);
        structured.Cv.Basics.Confidence.Summary.Should().NotBe(ConfidenceMarker.UserConfirmed);
        structured.Cv.Basics.Confidence.DatosPersonales.Should().NotBe(ConfidenceMarker.UserConfirmed);
        structured.Cv.Work.Should().NotBeNull();
        structured.Cv.Education.Should().NotBeNull();
        structured.Cv.Skills.Should().NotBeNull();
    }

    [Fact]
    public void Parse_PdfWithoutSemanticStructure_Still_Emits_Structured_With_InferredConfidence()
    {
        var bytes = PdfTestFixtures.CreateSimplePdf(
            "Juan Perez\njuan.perez@example.com\n+57 300 123 4567\n\nAlgo de texto sin secciones.\nMas texto en el mismo parrafo.");

        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/pdf",
            OriginalFileName: "wall.pdf",
            TraceId: "trace-2b-no-structure");

        var result = _parser.Parse(command);

        var structured = result.Should().BeOfType<StructuredParseResult>().Subject;
        structured.Cv.Basics.Should().NotBeNull();
        structured.Cv.Basics.Name.Should().NotBeNullOrWhiteSpace();
        structured.Cv.Basics.Email.Should().NotBeNullOrWhiteSpace();
        structured.Warnings.Should().Contain(w => w.Code == "PDF_NO_SEMANTIC_STRUCTURE");
    }

    [Fact]
    public void Parse_PdfWithExplicitEmail_Confidence_Is_Explicit_On_Basics_Email()
    {
        var bytes = PdfTestFixtures.CreateSimplePdf(
            "Maria Rodriguez\nmaria.rodriguez@example.com\n+57 1 234 5678\nhttps://maria.dev\nLinkedIn: linkedin.com/in/maria");

        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/pdf",
            OriginalFileName: "explicit.pdf",
            TraceId: "trace-2b-explicit");

        var result = _parser.Parse(command);

        var structured = result.Should().BeOfType<StructuredParseResult>().Subject;
        structured.Cv.Basics.Confidence.Email.Should().Be(ConfidenceMarker.Explicit);
        structured.Cv.Basics.Email.Should().Be("maria.rodriguez@example.com");
    }

    [Fact]
    public void Parse_EngineVersion_Is_2_0_0()
    {
        var bytes = PdfTestFixtures.CreateSimplePdf("Ana Lopez\nana@example.com\nBogota, Colombia");
        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/pdf",
            OriginalFileName: "version.pdf",
            TraceId: "trace-2b-version");

        var result = _parser.Parse(command);

        result.EngineVersion.Should().Be("2.0.0");
        result.Should().BeOfType<StructuredParseResult>();
    }

    [Fact]
    public void Parse_Never_Sets_Confidence_UserConfirmed()
    {
        var bytes = PdfTestFixtures.CreateMultiPageCvPdf();
        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/pdf",
            OriginalFileName: "no-uc.pdf",
            TraceId: "trace-2b-no-uc");

        var result = _parser.Parse(command);

        var structured = result.Should().BeOfType<StructuredParseResult>().Subject;
        var cv = structured.Cv;

        cv.Basics.Confidence.Name.Should().NotBe(ConfidenceMarker.UserConfirmed);
        cv.Basics.Confidence.Email.Should().NotBe(ConfidenceMarker.UserConfirmed);
        cv.Basics.Confidence.Phone.Should().NotBe(ConfidenceMarker.UserConfirmed);
        cv.Basics.Confidence.Location.Should().NotBe(ConfidenceMarker.UserConfirmed);
        cv.Basics.Confidence.Url.Should().NotBe(ConfidenceMarker.UserConfirmed);
        cv.Basics.Confidence.Profiles.Should().NotBe(ConfidenceMarker.UserConfirmed);
        cv.Basics.Confidence.Summary.Should().NotBe(ConfidenceMarker.UserConfirmed);
        cv.Basics.Confidence.DatosPersonales.Should().NotBe(ConfidenceMarker.UserConfirmed);

        foreach (var work in cv.Work)
        {
            work.Confidence.Name.Should().NotBe(ConfidenceMarker.UserConfirmed);
            work.Confidence.Position.Should().NotBe(ConfidenceMarker.UserConfirmed);
            work.Confidence.StartDate.Should().NotBe(ConfidenceMarker.UserConfirmed);
            work.Confidence.EndDate.Should().NotBe(ConfidenceMarker.UserConfirmed);
            work.Confidence.Summary.Should().NotBe(ConfidenceMarker.UserConfirmed);
            work.Confidence.Highlights.Should().NotBe(ConfidenceMarker.UserConfirmed);
        }

        foreach (var edu in cv.Education)
        {
            edu.Confidence.Institution.Should().NotBe(ConfidenceMarker.UserConfirmed);
            edu.Confidence.Area.Should().NotBe(ConfidenceMarker.UserConfirmed);
            edu.Confidence.StudyType.Should().NotBe(ConfidenceMarker.UserConfirmed);
            edu.Confidence.StartDate.Should().NotBe(ConfidenceMarker.UserConfirmed);
            edu.Confidence.EndDate.Should().NotBe(ConfidenceMarker.UserConfirmed);
            edu.Confidence.Score.Should().NotBe(ConfidenceMarker.UserConfirmed);
        }

        foreach (var skill in cv.Skills)
        {
            skill.Confidence.Name.Should().NotBe(ConfidenceMarker.UserConfirmed);
            skill.Confidence.Level.Should().NotBe(ConfidenceMarker.UserConfirmed);
        }
    }

    [Fact]
    public void Parse_MultiPageCv_Extracts_Work_Education_And_Skills_From_Known_Headers()
    {
        var bytes = PdfTestFixtures.CreateMultiPageCvPdf();
        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/pdf",
            OriginalFileName: "triangulate.pdf",
            TraceId: "trace-2b-triangulate");

        var result = _parser.Parse(command);

        var structured = result.Should().BeOfType<StructuredParseResult>().Subject;
        structured.Cv.Work.Should().NotBeEmpty();
        structured.Cv.Work[0].Entry.Name.Should().Contain("Acme Corp");
        structured.Cv.Work[0].Entry.Position.Should().Contain("Senior Developer");
        structured.Cv.Work[0].Entry.StartDate.Should().Be("2022-01");
        structured.Cv.Work[0].Entry.EndDate.Should().Be("2026-12");

        structured.Cv.Education.Should().NotBeEmpty();
        structured.Cv.Education[0].Entry.Institution.Should().Contain("Universidad Nacional");

        structured.Cv.Skills.Should().NotBeEmpty();
        structured.Cv.Skills.Should().HaveCountGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void Parse_CvWithLinkedinUrl_Extracts_LinkedIn_Profile()
    {
        var bytes = PdfTestFixtures.CreateSimplePdf(
            "Carlos Mendez\ncarlos@example.com\nLinkedIn: linkedin.com/in/carlos");
        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/pdf",
            OriginalFileName: "linkedin.pdf",
            TraceId: "trace-2b-linkedin");

        var result = _parser.Parse(command);

        var structured = result.Should().BeOfType<StructuredParseResult>().Subject;
        structured.Cv.Basics.Profiles.Should().NotBeEmpty();
        structured.Cv.Basics.Profiles.Should().Contain(p => p.Network == "LinkedIn");
        structured.Cv.Basics.Confidence.Profiles.Should().Be(ConfidenceMarker.Explicit);
    }
}
