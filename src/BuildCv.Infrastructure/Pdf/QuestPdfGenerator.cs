using BuildCv.Application.Features.Export;
using BuildCv.Domain.Export;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BuildCv.Infrastructure.Pdf;

/// <summary>
/// Implementación de IPdfGenerator usando QuestPDF (open source, MIT-style Community
/// License). Genera el PDF en memoria (sin persistir — Constitution Art. III) y
/// retorna byte[] con un layout simple: header, content, footer con marca de agua.
/// </summary>
public sealed class QuestPdfGenerator : IPdfGenerator
{
    static QuestPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stream = new MemoryStream();
        Document.Create(container => ComposeDocument(container, request))
            .GeneratePdf(stream);
        return stream.ToArray();
    }

    private static void ComposeDocument(IDocumentContainer container, ExportRequest request)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontSize(11).FontFamily(Fonts.Calibri));

            page.Header().Element(c => ComposeHeader(c, request));
            page.Content().Element(c => ComposeContent(c, request));
            page.Footer().Element(ComposeFooter);
        });
    }

    private static void ComposeHeader(IContainer container, ExportRequest request)
    {
        container.Column(col =>
        {
            col.Item().Text(request.CandidateName).FontSize(20).Bold();
            col.Item().Text($"CV adaptado · {DateTimeOffset.UtcNow:yyyy-MM-dd}").FontSize(10).FontColor(Colors.Grey.Medium);
        });
    }

    private static void ComposeContent(IContainer container, ExportRequest request)
    {
        container.PaddingTop(15).Column(col =>
        {
            col.Spacing(8);
            var sections = ParseMarkdown(request.AdaptedCv);
            foreach (var section in sections)
            {
                switch (section.Type)
                {
                    case MarkdownSectionType.H1:
                        col.Item().Text(section.Text).FontSize(16).Bold();
                        break;
                    case MarkdownSectionType.H2:
                        col.Item().Text(section.Text).FontSize(13).Bold();
                        break;
                    case MarkdownSectionType.ListItem:
                        col.Item().Text($"• {section.Text}").FontSize(11);
                        break;
                    case MarkdownSectionType.Paragraph:
                        col.Item().Text(section.Text).FontSize(11);
                        break;
                }
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            col.Item().PaddingTop(3).Text("Generado por BuildCv · v0 · " + DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC").FontSize(8).FontColor(Colors.Grey.Medium);
            col.Item().Text("No es un puntaje ATS oficial. Es una herramienta de análisis de coincidencia y legibilidad.").FontSize(8).FontColor(Colors.Grey.Medium).Italic();
            col.Item().Text("Powered by QuestPDF Community").FontSize(7).FontColor(Colors.Grey.Lighten1);
        });
    }

    private enum MarkdownSectionType { H1, H2, ListItem, Paragraph }

    private sealed record MarkdownSection(MarkdownSectionType Type, string Text);

    private static List<MarkdownSection> ParseMarkdown(string markdown)
    {
        var sections = new List<MarkdownSection>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return sections;
        }

        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("# "))
            {
                sections.Add(new MarkdownSection(MarkdownSectionType.H1, line[2..].Trim()));
            }
            else if (line.StartsWith("## "))
            {
                sections.Add(new MarkdownSection(MarkdownSectionType.H2, line[3..].Trim()));
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                sections.Add(new MarkdownSection(MarkdownSectionType.ListItem, line[2..].Trim()));
            }
            else
            {
                sections.Add(new MarkdownSection(MarkdownSectionType.Paragraph, line));
            }
        }

        return sections;
    }
}
