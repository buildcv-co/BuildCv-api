# Research: 004-export-pdf

**Date**: 2026-06-08 | **Status**: Phase 0 complete

## 1. QuestPDF — overview

- **NuGet**: `QuestPDF` (latest stable: 2024.x).
- **Licencia**: Community License es gratis, requiere atribución visible (QuestPDF lo agrega automáticamente en el footer del PDF).
- **API**: fluida, basada en lambdas. Se ve así:

```csharp
Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(2, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(t => t.FontSize(11).FontFamily("Calibri"));

        page.Header().Element(ComposeHeader);
        page.Content().Element(ComposeContent);
        page.Footer().Element(ComposeFooter);
    });
}).GeneratePdf(stream);
```

## 2. Licencia community

**Ubicación shipped:** en el **constructor estático de `QuestPdfGenerator`** (`src/BuildCv.Infrastructure/Pdf/QuestPdfGenerator.cs:16-19`). El plan original proponía setear la licencia en `Program.cs`, pero la implementación shipped la movió al constructor estático de la clase para garantizar que esté configurada antes de la primera generación, sin depender del orden de wire-up en `Program.cs`.

```csharp
public sealed class QuestPdfGenerator : IPdfGenerator
{
    static QuestPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }
    // ...
}
```

Si no se setea, QuestPDF lanza excepción al generar (en runtime).

## 3. Layout propuesto

```
┌─────────────────────────────────────────────┐
│ Juan Pérez                          [fecha]  │  ← Header
│ Backend Developer                             │
├─────────────────────────────────────────────┤
│                                              │
│ ## Resumen                                   │  ← Content
│ Backend developer con 2 años de experiencia. │
│                                              │
│ ## Experiencia                               │
│ ▸ Acme Corp · Developer · 2024-2026         │
│   - Logré X usando Y                          │
│                                              │
│ ## Skills                                    │
│ ┌────────────┬────────────┐                 │
│ │ C#         │ .NET       │                 │
│ │ SQL        │ Azure     │                 │
│ └────────────┴────────────┘                 │
│                                              │
│ ## Educación                                 │
│ ▸ Universidad X · 2018-2022                  │
│                                              │
├─────────────────────────────────────────────┤
│ Generado por BuildCv · v0 · 2026-06-08       │  ← Footer
│ No es un puntaje ATS oficial                 │
│ Powered by QuestPDF Community                 │
└─────────────────────────────────────────────┘
```

## 4. Parsing del CV (markdown → PDF)

El `adaptedCv` viene en markdown simple (`#` para h1, `##` para h2, `-` para listas). Necesito un parser ligero:

**Opción A**: usar librería (Markdig) — overkill para v0.
**Opción B**: parser custom regex para h1, h2, listas, párrafos. ~50 líneas.
**Opción C**: renderizar markdown literal (no es ideal, pero el LLM emite markdown simple).

**Decisión**: Opción B, parser regex custom. Razones:
- Cero dependencias nuevas.
- El LLM emite markdown simple (h1, h2, listas, párrafos).
- Fácil de testear (input/output predecibles).

## 5. Generación en memoria

```csharp
var stream = new MemoryStream();
Document.Create(container => { ... }).GeneratePdf(stream);
return stream.ToArray();
```

**Importante**: `GeneratePdf` es sync. Para no bloquear el thread de ASP.NET Core, envolver en `Task.Run`:

```csharp
public Task<byte[]> GeneratePdfAsync(ExportRequest request, CancellationToken ct)
{
    return Task.Run(() => GeneratePdf(request), ct);
}
```

## 6. UTF-8 y caracteres especiales

QuestPDF soporta UTF-8 out of the box. Fuentes default (`Calibri`, `Arial`) cubren español y caracteres latinos extendidos. Para emojis necesitaríamos `Segoe UI Emoji` o `Noto Color Emoji` — **fuera de scope v0**.

## 7. Performance

- CV típico (5-10k chars) → PDF en 200-500ms.
- CV máximo (50k chars) → PDF en 2-4s.
- Tamaño PDF típico: 50-200kB.

## 8. Riesgos identificados

1. **License check en producción**: si la license expira, la lib tira excepción. Verificar setup en Program.cs.
2. **Memory pressure**: para CVs >50k chars, el MemoryStream puede llegar a >1MB. No es problema en hardware moderno.
3. **Sync vs async**: `GeneratePdf` es sync, lo wrapeo en `Task.Run` para no bloquear el thread de request.

## Next Phase

→ Phase 1: Design (data-model, contracts).
→ Phase 2: Tasks (TDD ordering).
