using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace BuildCv.Infrastructure.Tests.Parsing;

internal static class DocxTestFixtures
{
    public static byte[] CreateSimpleDocx(string bodyText)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    new Paragraph(
                        new Run(
                            new Text(bodyText)))));

            mainPart.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] CreateDocxWithHeadings(params string[] headings)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new Body();

            foreach (var h in headings)
            {
                body.AppendChild(new Paragraph(new Run(new Text(h))));
                body.AppendChild(new Paragraph(new Run(new Text("Contenido de " + h))));
            }

            mainPart.Document = new Document(body);
            mainPart.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] CreateDocxWithDocumentProtection(string bodyText)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            var settings = mainPart.AddNewPart<DocumentSettingsPart>();
            settings.Settings = new Settings(
                new DocumentProtection
                {
                    Edit = DocumentProtectionValues.ReadOnly,
                    Enforcement = OnOffValue.FromBoolean(true),
                });

            mainPart.Document = new Document(
                new Body(
                    new Paragraph(
                        new Run(
                            new Text(bodyText)))));
            mainPart.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] CreateEmptyDocx()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            mainPart.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] CreateCorruptedDocxLikeBytes()
    {
        var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var padding = Encoding.ASCII.GetBytes(new string('x', 1024));
        return bytes.Concat(padding).ToArray();
    }
}
