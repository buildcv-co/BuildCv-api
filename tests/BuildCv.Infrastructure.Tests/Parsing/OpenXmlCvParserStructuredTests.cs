using BuildCv.Application.Features.Import;
using BuildCv.Domain.Resumes;
using BuildCv.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace BuildCv.Infrastructure.Tests.Parsing;

/// <summary>
/// Tests for <see cref="OpenXmlCvParser"/> as an <see cref="IStructuredParser"/>.
/// Micro-batch 2c of change 021 — the DOCX adapter MUST emit a
/// <see cref="StructuredParseResult"/> (engineVersion 2.0.0) carrying a typed
/// <see cref="CvDocument"/> with per-field <see cref="ConfidenceMarker"/> tags.
/// Constitution Art. I: parsers only emit <c>inferred</c> or <c>explicit</c>;
/// <c>user_confirmed</c> is editor-only (PR 4).
///
/// Distinct from PR 2b (PdfPig): the DOCX adapter walks the body element-by-element
/// (paragraphs, tables, SdtBlocks) so that DOCX tables and bullet lists are preserved
/// as <see cref="ResumeWorkEntry.Highlights"/> instead of being flattened with '\t'.
/// </summary>
public sealed class OpenXmlCvParserStructuredTests
{
    private readonly IStructuredParser _parser = new OpenXmlCvParser();

    [Fact]
    public void Parse_ValidDocxText_Emits_StructuredParseResult_With_InferredConfidence()
    {
        var bytes = DocxTestFixtures.CreateStructuredDocx(new DocxBlock[]
        {
            new DocxParagraph("Juan Perez"),
            new DocxParagraph("juan.perez@example.com"),
            new DocxParagraph("+57 300 123 4567"),
            new DocxParagraph("EXPERIENCE"),
            new DocxParagraph("Acme Corp · Developer · 2022-2026"),
            new DocxParagraph("Built microservices in .NET"),
            new DocxParagraph("EDUCATION"),
            new DocxParagraph("Universidad Nacional · Systems Engineering · 2014-2019"),
            new DocxParagraph("SKILLS"),
            new DocxParagraph("C#, .NET, SQL"),
        });

        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "cv.docx",
            TraceId: "trace-2c-structured");

        var result = _parser.Parse(command);

        var structured = result.Should().BeOfType<StructuredParseResult>().Subject;
        structured.Cv.Basics.Name.Should().NotBeNullOrWhiteSpace();
        structured.Cv.Basics.Email.Should().Be("juan.perez@example.com");
        structured.Cv.Basics.Confidence.Name.Should().Be(ConfidenceMarker.Inferred);
        structured.Cv.Basics.Confidence.Email.Should().NotBe(ConfidenceMarker.UserConfirmed);
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
    public void Parse_DocxWithBulletList_PreservesBullets_AsExperienceBullets()
    {
        var bytes = DocxTestFixtures.CreateStructuredDocx(new DocxBlock[]
        {
            new DocxParagraph("EXPERIENCE"),
            new DocxParagraph("Acme Corp · Developer · 2022-2026"),
            new DocxParagraph("• Built microservices"),
            new DocxParagraph("• Mentored juniors"),
        });

        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "bullets.docx",
            TraceId: "trace-2c-bullets");

        var result = _parser.Parse(command);

        var structured = result.Should().BeOfType<StructuredParseResult>().Subject;
        structured.Cv.Work.Should().HaveCount(1);
        structured.Cv.Work[0].Entry.Name.Should().Contain("Acme Corp");
        structured.Cv.Work[0].Entry.Position.Should().Contain("Developer");
        structured.Cv.Work[0].Entry.Highlights.Should().NotBeNull();
        structured.Cv.Work[0].Entry.Highlights.Should().Contain("Built microservices");
        structured.Cv.Work[0].Entry.Highlights.Should().Contain("Mentored juniors");
        structured.Cv.Work[0].Entry.Highlights.Should().NotContain(h => h.Contains('\t'));
    }

    [Fact]
    public void Parse_DocxWithTable_PreservesTableRows_AsExperienceBullets()
    {
        var bytes = DocxTestFixtures.CreateStructuredDocx(new DocxBlock[]
        {
            new DocxParagraph("EXPERIENCE"),
            new DocxParagraph("Acme Corp · Developer · 2022-2026"),
            new DocxTable(new[]
            {
                new[] { "Contoso", "Senior Engineer" },
                new[] { "Globex", "Tech Lead" },
            }),
        });

        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "table.docx",
            TraceId: "trace-2c-table");

        var result = _parser.Parse(command);

        var structured = result.Should().BeOfType<StructuredParseResult>().Subject;
        structured.Cv.Work.Should().HaveCount(1);
        structured.Cv.Work[0].Entry.Highlights.Should().NotBeNull();
        structured.Cv.Work[0].Entry.Highlights.Should().Contain(h => h.Contains("Contoso") && h.Contains("Senior Engineer"));
        structured.Cv.Work[0].Entry.Highlights.Should().Contain(h => h.Contains("Globex") && h.Contains("Tech Lead"));
        structured.Cv.Work[0].Entry.Highlights.Should().NotContain(h => h.Contains('\t'));
    }

    [Fact]
    public void Parse_EngineVersion_Is_2_0_0()
    {
        var bytes = DocxTestFixtures.CreateStructuredDocx(new DocxBlock[]
        {
            new DocxParagraph("Ana Lopez"),
            new DocxParagraph("ana@example.com"),
            new DocxParagraph("EXPERIENCE"),
            new DocxParagraph("Acme · Developer · 2020-2024"),
        });

        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "version.docx",
            TraceId: "trace-2c-version");

        var result = _parser.Parse(command);

        result.EngineVersion.Should().Be("2.0.0");
        result.Should().BeOfType<StructuredParseResult>();
    }

    [Fact]
    public void Parse_Never_Sets_Confidence_UserConfirmed()
    {
        var bytes = DocxTestFixtures.CreateStructuredDocx(new DocxBlock[]
        {
            new DocxParagraph("Maria Rodriguez"),
            new DocxParagraph("maria@example.com"),
            new DocxParagraph("+57 1 234 5678"),
            new DocxParagraph("https://maria.dev"),
            new DocxParagraph("EXPERIENCE"),
            new DocxParagraph("Acme Corp · Developer · 2022-2026"),
            new DocxParagraph("• Built microservices"),
            new DocxParagraph("EDUCATION"),
            new DocxParagraph("Universidad Nacional · Systems · 2014-2019"),
            new DocxParagraph("SKILLS"),
            new DocxParagraph("C#, .NET"),
        });

        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "no-uc.docx",
            TraceId: "trace-2c-no-uc");

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
    public void Parse_DocxWithoutSemanticStructure_Emits_Warning_DOCX_NO_SEMANTIC_STRUCTURE()
    {
        var bytes = DocxTestFixtures.CreateStructuredDocx(new DocxBlock[]
        {
            new DocxParagraph("Pedro Gomez"),
            new DocxParagraph("pedro@example.com"),
            new DocxParagraph("Un parrafo sin secciones reconocibles."),
            new DocxParagraph("Otro parrafo sin estructura."),
        });

        var command = new ImportCvCommand(
            FileBytes: bytes,
            MimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OriginalFileName: "wall.docx",
            TraceId: "trace-2c-no-structure");

        var result = _parser.Parse(command);

        var structured = result.Should().BeOfType<StructuredParseResult>().Subject;
        structured.Cv.Basics.Should().NotBeNull();
        structured.Cv.Basics.Name.Should().NotBeNullOrWhiteSpace();
        structured.Warnings.Should().Contain(w => w.Code == "DOCX_NO_SEMANTIC_STRUCTURE");
    }
}
