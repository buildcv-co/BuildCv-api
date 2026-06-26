using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildCv.Api.Contracts;
using BuildCv.Domain.Resumes;
using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Xunit;
using DocXBody = DocumentFormat.OpenXml.Wordprocessing.Body;
using DocXDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using DocXDocumentType = DocumentFormat.OpenXml.WordprocessingDocumentType;
using DocXParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using DocXRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using DocXText = DocumentFormat.OpenXml.Wordprocessing.Text;
using QuestPDFDocument = QuestPDF.Fluent.Document;

namespace BuildCv.Api.IntegrationTests;

internal static class ImportResponseJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

public sealed class ImportEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Should_accept_pdf_and_return_200_with_import_result()
    {
        var pdfBytes = CreatePdfWithSections();

        var content = BuildMultipart(pdfBytes, "application/pdf", "cv.pdf", engineVersion: "1.0.0");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ImportResponseDto>();
        body.Should().NotBeNull();
        body!.Text.Should().Contain("EXPERIENCIA");
        body.Text.Should().Contain("EDUCACIÓN");
        body.EngineVersion.Should().Be("1.0.0");
        body.TraceId.Should().NotBeNullOrWhiteSpace();
        body.Sections.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_accept_docx_and_return_200_with_import_result()
    {
        var docxBytes = CreateDocxWithSections();

        var content = BuildMultipart(docxBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "cv.docx", engineVersion: "1.0.0");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ImportResponseDto>();
        body.Should().NotBeNull();
        body!.Text.Should().Contain("EXPERIENCIA");
        body.Text.Should().Contain("EDUCACIÓN");
    }

    // =====================================================================
    // PR 2e — engineVersion negotiation: default v2.0.0, header/query v1.0.0,
    // unknown engineVersion → 400. Response shape changes to ImportResponseV2Dto.
    // =====================================================================

    [Fact]
    public async Task Post_With_EngineVersion_Header_2_0_0_Returns_ImportResultV2_With_CvDocument()
    {
        var pdfBytes = CreatePdfWithSections();

        var content = BuildMultipart(pdfBytes, "application/pdf", "cv.pdf", engineVersion: "2.0.0");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ImportResponseV2Dto>(ImportResponseJson.Options);
        body.Should().NotBeNull();
        body!.EngineVersion.Should().Be("2.0.0");
        body.TraceId.Should().NotBeNullOrWhiteSpace();
        body.Cv.Should().NotBeNull();
        body.Cv.Basics.Should().NotBeNull();
        body.Cv.Meta.EngineVersion.Should().Be("2.0.0");
    }

    [Fact]
    public async Task Post_Without_EngineVersion_Defaults_To_2_0_0_Returns_ImportResultV2()
    {
        var pdfBytes = CreatePdfWithSections();

        var content = BuildMultipart(pdfBytes, "application/pdf", "cv.pdf");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ImportResponseV2Dto>(ImportResponseJson.Options);
        body.Should().NotBeNull();
        body!.EngineVersion.Should().Be("2.0.0");
        body.Cv.Should().NotBeNull();
        body.Cv.Meta.EngineVersion.Should().Be("2.0.0");
    }

    [Fact]
    public async Task Post_With_EngineVersion_Header_1_0_0_Returns_Legacy_ImportResult()
    {
        var pdfBytes = CreatePdfWithSections();

        var content = BuildMultipart(pdfBytes, "application/pdf", "cv.pdf", engineVersion: "1.0.0");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ImportResponseDto>();
        body.Should().NotBeNull();
        body!.Text.Should().Contain("EXPERIENCIA");
        body.EngineVersion.Should().Be("1.0.0");
        body.Sections.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Post_With_EngineVersion_Query_1_0_0_Returns_Legacy_ImportResult()
    {
        var pdfBytes = CreatePdfWithSections();

        var content = BuildMultipart(pdfBytes, "application/pdf", "cv.pdf", engineVersion: "1.0.0");

        var response = await _client.PostAsync("/api/v1/import?engineVersion=1.0.0", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ImportResponseDto>();
        body.Should().NotBeNull();
        body!.Text.Should().Contain("EXPERIENCIA");
        body.EngineVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Post_With_Unknown_EngineVersion_Returns_400()
    {
        var pdfBytes = CreatePdfWithSections();

        var content = BuildMultipart(pdfBytes, "application/pdf", "cv.pdf", engineVersion: "9.9.9");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().Contain("IMPORT_UNSUPPORTED_ENGINE_VERSION");
    }

    [Fact]
    public async Task Should_reject_text_plain_with_415()
    {
        var text = "hola mundo"u8.ToArray();
        var content = BuildMultipart(text, "text/plain", "fake.txt");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Should_reject_file_over_5MB_with_413()
    {
        var bytes = new byte[6 * 1024 * 1024];
        Array.Fill<byte>(bytes, 0x25);
        var content = BuildMultipart(bytes, "application/pdf", "huge.pdf");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Should_reject_mismatched_mime_with_415()
    {
        var fake = "%PDF-1.4"u8.ToArray();
        var content = BuildMultipart(fake, "application/zip", "fake.zip");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Should_reject_request_without_file_with_400()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("dummy"), "other");

        var response = await _client.PostAsync("/api/v1/import", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_return_problem_details_with_code_in_body()
    {
        var text = "hola"u8.ToArray();
        var content = BuildMultipart(text, "text/plain", "fake.txt");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("code");
        body.Should().Contain("IMPORT_UNSUPPORTED_MEDIA");
    }

    [Fact]
    public async Task Should_reject_garbage_bytes_with_pdf_mime_with_415()
    {
        var bytes = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x02, 0x03, 0x04 };
        var content = BuildMultipart(bytes, "application/pdf", "fake.pdf");

        var response = await _client.PostAsync("/api/v1/import", content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    private static MultipartFormDataContent BuildMultipart(byte[] bytes, string mime, string fileName, string? engineVersion = null)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
        content.Add(fileContent, "file", fileName);
        if (engineVersion is not null)
        {
            content.Headers.TryAddWithoutValidation("X-Engine-Version", engineVersion);
        }
        return content;
    }

    private static byte[] CreatePdfWithSections()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var document = QuestPDFDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Juan Pérez").FontSize(18).Bold();
                    col.Item().Text("EXPERIENCIA").FontSize(14).Bold();
                    col.Item().Text("Acme Corp 2020-2024");
                    col.Item().Text("EDUCACIÓN").FontSize(14).Bold();
                    col.Item().Text("Universidad 2015-2019");
                });
            });
        });
        return document.GeneratePdf();
    }

    private static byte[] CreateDocxWithSections()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, DocXDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new DocXBody();
            body.AppendChild(new DocXParagraph(new DocXRun(new DocXText("EXPERIENCIA"))));
            body.AppendChild(new DocXParagraph(new DocXRun(new DocXText("Acme Corp 2020-2024"))));
            body.AppendChild(new DocXParagraph(new DocXRun(new DocXText("EDUCACIÓN"))));
            body.AppendChild(new DocXParagraph(new DocXRun(new DocXText("Universidad 2015-2019"))));
            mainPart.Document = new DocXDocument(body);
            mainPart.Document.Save();
        }
        return ms.ToArray();
    }
}
