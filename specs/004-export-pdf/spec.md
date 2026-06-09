# Feature Specification: 004-export-pdf — Exportar CV adaptado a PDF

**Feature Branch**: `004-export-pdf`
**Created**: 2026-06-08
**Status**: ✅ SHIPPED (commit 635d688, 2026-06-09)
**Input**: User description: "Exportar el CV adaptado a PDF para descarga inmediata"

> **Frontend counterpart:** [../../../BuildCv-web/specs/004-web-export-ui/](../../../BuildCv-web/specs/004-web-export-ui/)
> **INDEX global:** [../000-INDEX.md](../000-INDEX.md)

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Descargar CV adaptado como PDF (Priority: P1)

Como usuario que ya obtuvo una adaptación con score alto, quiero descargar el CV adaptado en formato PDF listo para enviar a reclutadores, con un diseño limpio y profesional que respete el encuadre honesto.

**Why this priority**: Es el cierre del flujo de valor. Sin PDF, el usuario tiene que copiar/pegar manualmente, perdiendo el formato. Con PDF, cierra el ciclo "CV → score → adaptación → entrega".

**Independent Test**: Solicitar export de un CV adaptado, recibir un PDF descargable de <200kB con: (1) datos del candidato correctos, (2) experiencia en orden cronológico, (3) skills visibles, (4) marca de agua pequeña "BuildCv · v0 · generado 2026-06-08" (encuadre honesto, NO "ATS-certified").

**Acceptance Scenarios**:

1. **Given** un CV adaptado válido, **When** solicito export PDF, **Then** recibo `Content-Type: application/pdf` con `Content-Disposition: attachment; filename="cv-adapted-2026-06-08.pdf"`.
2. **Given** un CV con marca de agua "BuildCv · v0", **When** abro el PDF, **Then** la marca aparece en el footer (NO en header) y dice "no es un puntaje ATS oficial" en tipografía pequeña.
3. **Given** el CV tiene 50k chars, **When** solicito export, **Then** el PDF se genera en <3s y el archivo <500kB.

---

### User Story 2 — Validación de honestidad pre-export (Priority: P2)

Como usuario, si mi CV adaptado tiene **severity=Critical** con invenciones Hard, el endpoint me avisa ANTES de generar el PDF para que decida si descargo igual o regenero la adaptación.

**Why this priority**: Defensa de Art. I (cero invención). Aunque el cross-entity validator ya detecta invenciones, evitar generar PDFs de CVs con invenciones es la última barrera.

**Independent Test**: Adaptar CV con trampa que genere 1+ Hard invención → intentar export → recibir 422 con detalle de invenciones y sugerencia "regenera la adaptación con prompt más estricto".

**Acceptance Scenarios**:

1. **Given** ValidationReport con severity=Critical y ≥1 Hard invención, **When** solicito export, **Then** recibo 422 con lista de invenciones y código `EXPORT_BLOCKED_INVENTION`.
2. **Given** ValidationReport con severity=Warning (solo soft), **When** solicito export, **Then** recibo 200 con PDF + warning visible en el footer.
3. **Given** ValidationReport con severity=None, **When** solicito export, **Then** recibo 200 sin warning.

---

### User Story 3 — Rate-limit diferenciado (Priority: P3)

Como usuario consciente de costos, las exportaciones PDF están rate-limited independientemente del score y de la adaptación: máximo 20/h por IP (más permisivo que adaptación porque PDF es CPU-bound, no LLM-bound).

**Why this priority**: PDF consume CPU + memoria pero no LLM. El rate-limit protege el servidor de abuse sin afectar UX legítimo.

**Independent Test**: Hacer 21 exports en 1h → el 21º recibe 429. Mismo usuario puede seguir haciendo scores (60/min) y adaptaciones (5/h) sin restricción cruzada.

**Acceptance Scenarios**:

1. **Given** 20 exports ya hechos en 1h, **When** intento el 21º, **Then** recibo 429 con `Retry-After` y mensaje honesto.
2. **Given** el límite "export" está bloqueado, **When** hago un score, **Then** el score funciona (límites independientes).
3. **Given** intento export con validación que falla, **When** el server retorna 422, **Then** NO consume cupo del rate-limit (cuenta solo exports exitosos).

---

### Edge Cases

- **CV con caracteres especiales** (acentos, ñ, emojis): PDF debe renderizar correctamente (QuestPDF usa fuentes con soporte Unicode).
- **CV >50k chars**: rechazado en score/adapt; si llega al export, rechazado 400 con `EXPORT_TOO_LARGE`.
- **CV vacío o solo whitespace**: rechazado 400 antes de generar PDF.
- **Timeout de generación**: si tarda >10s, retornar 504 (PDF es CPU, no debería pasar en hardware normal).
- **Múltiples exports concurrentes del mismo usuario**: cada uno cuenta independiente, no se deduplican.

---

## Key Functional Requirements (FR)

| ID | Requirement |
|---|---|
| FR-032 | El sistema **MUST** generar un PDF descargable del CV adaptado con diseño limpio y profesional. |
| FR-033 | El sistema **MUST** incluir una marca de agua honesta "BuildCv · v0 · no es un puntaje ATS oficial" en el footer. |
| FR-034 | El sistema **MUST** rechazar el export si el ValidationReport tiene severity=Critical con ≥1 Hard invención (Art. I — cero invención). |
| FR-035 | El sistema **MUST** aplicar rate-limit diferenciado "export" (20/h por IP) — más permisivo que "ai" (5/h) porque PDF es CPU-bound. |
| FR-037 (ya) | El sistema **MUST** rechazar entradas que excedan 50k chars (CV) antes de generar el PDF. |
| FR-046 | El sistema **MUST** generar el PDF server-side (NUNCA en el cliente) — usa `QuestPDF` (NuGet, .NET, open source, no requiere licencia comercial). |
| FR-049 | El sistema **MUST** usar el filename `cv-adapted-{YYYY-MM-DD}.pdf` (encuadre honesto, no "cv-optimized" o "ats-ready"). |

---

## Non-Functional Requirements (NFR)

| ID | Requirement |
|---|---|
| NFR-002 | El sistema **MUST NOT** loguear el contenido del CV. Logs solo metadatos: `(cvLength, fileSize, traceId, generationTimeMs)`. |
| NFR-008 | Los secretos (QuestPDF community license key) **MUST NOT** exponerse al cliente. |
| NFR-012 | El PDF **MUST** renderizar en <3s para CVs <10k chars (P95). |
| NFR-013 | El PDF **MUST** pesar <500kB para CVs típicos (<20k chars). |
| NFR-018 | El sistema **MUST** degradar con elegancia: si QuestPDF falla, retornar 503 con fallback message (no 500). |
| NFR-020 | El copy **MUST** usar el encuadre "coincidencia + legibilidad" en la marca de agua. NUNCA "ATS-certified" o "garantiza empleo". |

---

## Success Criteria

- ✅ Un usuario puede descargar un PDF de su CV adaptado en <3s.
- ✅ El PDF respeta el encuadre honesto (NO "ATS oficial", "garantiza empleo", etc.).
- ✅ Hard invenciones bloquean el export con 422 + lista de términos problemáticos.
- ✅ Rate-limit 20/h por IP activo, diferenciado de "ai" (5/h) y "score" (60/min).
- ✅ PDF pesa <500kB para CVs típicos.
- ✅ 0% de contenido del CV en logs.

---

## Constitution Check *(mandatory)*

| Art. | Aplicación |
|---|---|
| **Art. I** — Cero invención | FR-034: Hard invenciones bloquean el export. Defensa post-validación. |
| **Art. III** — Privacidad | NFR-002: sin logs de contenido. PDF generado en memoria, no se persiste. |
| **Art. IV** — Encuadre honesto | FR-033, NFR-020: marca de agua dice "no es un puntaje ATS oficial". Filename "cv-adapted-", no "ats-ready". |
| **Art. VI** — Clean Arch | `IPdfGenerator` puerto en Application; `QuestPdfGenerator` en Infrastructure. Domain PURO. |
| **Art. VII** — Rate-limit | FR-035: política "export" 20/h, diferenciada. |
| **Art. VIII** — TDD | Tests rojos ANTES de implementación. Cover ≥85% en `QuestPdfGenerator`. |
| **Art. IX** — Habeas Data | PDF generado en memoria, no se persiste en disco. v0 sin guardado (consistente con Art. III). |

**Compliance esperado**: PASS.

---

## Out of Scope (v0)

- Múltiples templates de PDF (solo 1 diseño limpio en v0).
- Watermark con logo personalizado.
- Exportar a DOCX, TXT, HTML.
- Persistir el PDF generado para descarga posterior (v1).
- Email del PDF al usuario (v1).

---

## Open Questions (auto-resolved)

- **¿Qué librería PDF usar?**: `QuestPDF` (NuGet, open source MIT para uso no comercial, API fluida en C#, render rápido). Alternativas: iTextSharp (licencia AGPL, complicada), PdfSharp (más viejo, menos features). **Decisión: QuestPDF.**
- **¿Community license requiere key?**: SÍ, `QuestPDF.Settings.License = LicenseType.Community;` debe estar en `Program.cs`. La community license es gratis pero requiere atribución visible.
- **¿Diseñar el layout desde cero o usar plantilla?**: Layout custom con header (nombre del candidato), experiencia (lista cronológica), skills (grid 2 columnas), educación, footer con marca de agua.

---

## Next Phase

→ Phase 1: Design — `data-model.md`, `quickstart.md`, `contracts/export-api.md`.
→ Phase 2: Tasks — `/speckit.tasks` con TDD ordering.
