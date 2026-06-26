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

    public static byte[] CreateStructuredDocx(IReadOnlyList<DocxBlock> blocks)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new Body();

            foreach (var block in blocks)
            {
                switch (block)
                {
                    case DocxParagraph p:
                        body.AppendChild(new Paragraph(new Run(new Text(p.Text))));
                        break;
                    case DocxTable t:
                        body.AppendChild(BuildTable(t));
                        break;
                }
            }

            mainPart.Document = new Document(body);
            mainPart.Document.Save();
        }

        return ms.ToArray();
    }

    private static Table BuildTable(DocxTable t)
    {
        var table = new Table();
        var props = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }));
        table.AppendChild(props);

        foreach (var rowCells in t.Rows)
        {
            var row = new TableRow();
            foreach (var cellText in rowCells)
            {
                var cell = new TableCell(new Paragraph(new Run(new Text(cellText))));
                row.AppendChild(cell);
            }

            table.AppendChild(row);
        }

        return table;
    }
}

internal abstract record DocxBlock;

internal sealed record DocxParagraph(string Text) : DocxBlock;

internal sealed record DocxTable(IReadOnlyList<IReadOnlyList<string>> Rows) : DocxBlock;
