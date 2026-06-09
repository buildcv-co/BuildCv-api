using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BuildCv.Infrastructure.Tests.Parsing;

internal static class PdfTestFixtures
{
    static PdfTestFixtures()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] CreateSimplePdf(string content)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.Content().Text(content).FontSize(12);
            });
        });

        return document.GeneratePdf();
    }

    public static byte[] CreateMultiPageCvPdf()
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Juan Pérez").FontSize(18).Bold();
                    col.Item().Text("Backend Developer");
                    col.Item().Text("");
                    col.Item().Text("EXPERIENCIA").FontSize(14).Bold();
                    col.Item().Text("Acme Corp · Senior Developer · 2022-2026");
                    col.Item().Text("Lideré migración de monolito a microservicios.");
                    col.Item().Text("Reduje latencia P95 en 40%.");
                    col.Item().Text("");
                    col.Item().Text("EDUCACIÓN").FontSize(14).Bold();
                    col.Item().Text("Universidad Nacional · Ingeniería de Sistemas · 2014-2019");
                });
            });

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("HABILIDADES").FontSize(14).Bold();
                    col.Item().Text("C#, ASP.NET Core, SQL Server, Docker, Kubernetes, Azure");
                    col.Item().Text("");
                    col.Item().Text("CONTACTO").FontSize(14).Bold();
                    col.Item().Text("juan.perez@example.com");
                    col.Item().Text("+57 300 123 4567");
                });
            });
        });

        return document.GeneratePdf();
    }

    public static byte[] CreatePdfWithoutExtractableText()
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.Content().Image(Placeholders.Image(200, 200));
            });
        });

        return document.GeneratePdf();
    }

    public static byte[] CreateGarbageBytesWithPdfHeader()
    {
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var random = new byte[2048];
        System.Security.Cryptography.RandomNumberGenerator.Fill(random);
        return header.Concat(random).ToArray();
    }
}
