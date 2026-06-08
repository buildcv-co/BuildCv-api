# Feature Specification: 005-cv-pdf-docx-import — Importar CV desde PDF o DOCX

**Feature Branch**: `005-cv-pdf-docx-import`
**Created**: 2026-06-09
**Status**: Draft
**Hito**: v0.5 (P0.5)
**Input**: User description: "Carga de archivos PDF/DOCX del CV (parseo server-side) para alimentar el editor 006 con texto extraído en vez de pegarlo a mano."

> **Frontend counterpart:** [../../../BuildCv-web/specs/005-web-cv-import-ui/](../../../BuildCv-web/specs/005-web-cv-import-ui/)
> **Handoff downstream:** [../../../BuildCv-api/specs/006-cv-editor/](../../../BuildCv-api/specs/006-cv-editor/) (planeado) — el editor recibe `ImportResult.text` y `ImportResult.sections` como semilla.
> **INDEX global:** [../000-INDEX.md](../000-INDEX.md)

---

## Resumen ejecutivo

v0 obliga al usuario a **pegar** su CV y la vacante. Eso es fricción: los candidatos no siempre tienen su CV en texto plano y el copiado manual introduce errores (saltos de página, símbolos pegados, pérdida de viñetas). v0.5 introduce **carga de archivos PDF/DOCX** con parseo **server-side** y devuelve texto + secciones heurísticas para alimentar el editor (006) y, en última instancia, el puntaje (002) y la adaptación (003).

El parseo vive **detrás del puerto `ICvParser`** (Constitution Art. VI v1.1.0) y está limitado por la nueva política de rate-limit `"import"` (Constitution Art. VII v1.1.0, 30/h por IP). El cliente (web) **solo sube el archivo** — el backend hace el trabajo pesado, con C# puro y dependencias OSS permisivas (PdfPig Apache-2.0, OpenXML MIT).

Esta spec es **honesta sobre el alcance v0.5**: detección de secciones por regex sobre headers en MAYÚSCULAS (EXPERIENCIA, EDUCACIÓN, HABILIDADES); v1 introducirá parsing estructural.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Cargar CV en PDF (Priority: P1)

Como usuario que tiene su CV en PDF (formato de la mayoría de hojas de vida colombianas), quiero arrastrar el archivo a la web y recibir el texto extraído en el editor — sin tener que abrir el PDF y copiar a mano, y sin tener que entender qué es Markdown.

**Why this priority**: Es el camino más común. La fricción de "abrir PDF + seleccionar todo + pegar" es la primera causa de abandono de v0. Un PDF bien parseado reduce el tiempo de "esto" de ~2 minutos a <10 segundos.

**Independent Test**: Arrastrar un PDF sintético de 2 páginas al componente `FileUpload`, recibir un `ImportResult` con `text` ≥ 80% del texto del PDF original y `sections` con al menos 1 sección detectada (Encabezado o Experiencia). El editor (006) acepta el texto como semilla.

**Acceptance Scenarios**:

1. **Given** un PDF de 2 páginas con nombre, contacto, experiencia y skills, **When** lo subo vía `FileUpload`, **Then** recibo `ImportResult` con `text` que contiene el nombre, las fechas y los skills, y `sections[]` con `{heading, start, end}` para "Experiencia" y "Educación".
2. **Given** el PDF tiene tablas, **When** lo subo, **Then** el texto se extrae en orden de lectura (no por columnas) con separadores de párrafo y NO se incluye contenido binario en `text`.
3. **Given** subo el PDF, **When** el parseo termina en <2s, **Then** la UI pasa de `loading` a `result` y abre el `ImportResultPanel` con un botón "Usar este texto en el editor".

---

### User Story 2 — Cargar CV en DOCX (Priority: P1)

Como usuario que edita su CV en Word (formato .docx), quiero subir el archivo y obtener texto plano con estructura básica — la mayoría de CVs DOCX se pierden al pegar por los estilos de Word, y este flujo los limpia.

**Why this priority**: Mismo nivel de fricción que PDF; en LATAM, DOCX es el segundo formato más común. Si solo soportáramos PDF, dejaríamos fuera a usuarios que editan en Word/Pages/LibreOffice.

**Independent Test**: Subir un DOCX de 1 página con una sección "Experiencia" y otra "Educación"; recibir `ImportResult.sections` con ambos headings detectados, y `text` sin caracteres de control de Word.

**Acceptance Scenarios**:

1. **Given** un DOCX con headings, viñetas y negritas, **When** lo subo, **Then** el `text` es texto plano con saltos de línea entre párrafos, sin artefactos de Word, y los headings se detectan como secciones.
2. **Given** un DOCX con tablas e imágenes, **When** lo subo, **Then** las tablas se extraen como filas con `\t` separador, las imágenes se mencionan como `[imagen omitida]` en `warnings[]`, y el flujo sigue.
3. **Given** un DOCX protegido con contraseña, **When** lo subo, **Then** recibo 422 con código `IMPORT_DOCX_PROTECTED` y mensaje "Este archivo de Word está protegido. Quítale la contraseña y vuelve a subirlo."

---

### User Story 3 — Ver avisos del parseo (Priority: P2)

Como usuario, cuando el PDF/DOCX tiene partes que el parser no puede interpretar limpiamente (p. ej. una imagen de fondo, una columna lateral, un objeto incrustado), quiero ver **avisos transparentes** en el `ImportResultPanel` — no una extracción silenciosamente incompleta.

**Why this priority**: Defensa de Art. IV (encuadre honesto). Decir "todo se extrajo perfecto" cuando se omitió una imagen sería mentir. Un aviso claro ("omitimos 1 imagen") permite al usuario revisar manualmente.

**Independent Test**: Subir un PDF que contiene una imagen de fondo; recibir `warnings[]` con `{code: "IMAGE_OMITTED", message: "Se omitió 1 imagen", severity: "Info"}`. La UI muestra un toast amarillo no bloqueante.

**Acceptance Scenarios**:

1. **Given** un PDF con N imágenes, **When** lo subo, **Then** `warnings[]` contiene 1 entrada `IMAGE_OMITTED` con `count: N` y la UI la muestra con severidad `Info`.
2. **Given** un PDF escaneado (basado en imágenes, sin texto extraíble), **When** lo subo, **Then** recibo 422 con código `IMPORT_SCANNED_PDF` y mensaje honesto: "Este PDF parece un escaneo. No podemos extraer texto. Pega el contenido manualmente o usa un PDF con texto seleccionable."
3. **Given** un PDF/DOCX donde una sección no se detecta con confianza, **When** lo subo, **Then** esa sección aparece en `warnings[]` con `code: "SECTION_AMBIGUOUS"` y la UI sugiere al usuario marcarla manualmente en el editor.

---

### Edge Cases

- **PDF cifrado/con contraseña** → 422 `IMPORT_PDF_ENCRYPTED`. No se intenta crack.
- **PDF >5 MB** → 413 `IMPORT_TOO_LARGE`. Límite se aplica **antes** de leer el body completo (early-reject en el endpoint).
- **PDF >100 páginas** → 422 `IMPORT_TOO_MANY_PAGES` (defensa de CPU/memoria).
- **DOCX sin texto** (solo imágenes) → 422 `IMPORT_DOCX_NO_TEXT`.
- **DOCX con macros maliciosas (.docm)** → rechazado por MIME (415 `IMPORT_UNSUPPORTED_MEDIA`); `.docm` no es `wordprocessingml.document`.
- **MIME incorrecto declarado** (ej. `application/zip` con bytes PDF) → 415. Se valida por magic bytes (`%PDF-` y `PK\x03\x04`).
- **Encoding raro (Latin-1 vs UTF-8)** → el parser normaliza a UTF-8; advertencia `ENCODING_NORMALIZED` en `warnings[]` si hubo transformación.
- **Archivo vacío (0 bytes)** → 422 `IMPORT_EMPTY_FILE`.
- **Texto extraído con tamaño >50k chars** → se trunca a 50k y se añade `warnings[]` con `code: "TEXT_TRUNCATED"`, `originalLength`, `truncatedLength`. Coherente con FR-037 del score/adapt.
- **Petición concurrente del mismo usuario con el mismo archivo** → cada una cuenta independiente en el rate-limit (no se deduplica).
- **Prompt-injection en el PDF/DOCX** (texto que dice "ignora todas las reglas") → se trata como **dato** (Art. V): el parser extrae el texto tal cual, sin ejecutar nada. El texto extraído es inerte hasta que el usuario lo confirme y el score/adapt lo procesen con sus propios guardarraíles (nonces, etc.).
- **Archivo con virus (Eicar test string)** → fuera de alcance de v0.5; en v1 se integrará un escáner (Art. IX, deber de seguridad).

---

## Key Functional Requirements (FR)

| ID | Requirement |
|---|---|
| **FR-039** | El sistema **MUST** aceptar archivos PDF o DOCX del CV vía `POST /api/v1/import` (multipart/form-data), extraer el texto y devolverlo en un `ImportResult` JSON con `text`, `sections[]` y `warnings[]`. |
| **FR-039a** | El sistema **MUST** validar el MIME **y** los magic bytes del archivo, y rechazar con `415 Unsupported Media Type` todo lo que no sea `application/pdf` (con `%PDF-` en los primeros bytes) o `application/vnd.openxmlformats-officedocument.wordprocessingml.document` (con `PK\x03\x04` y entry `word/document.xml`). |
| **FR-039b** | El sistema **MUST** rechazar con `413 Payload Too Large` cualquier archivo cuyo `Content-Length` declarado (o suma de chunks multipart) exceda 5 MB, **antes** de alocar memoria para el parseo. |
| **FR-039c** | El sistema **MUST** aplicar la política de rate-limit `"import"` (30/h por IP, Constitución Art. VII v1.1.0), diferenciada de `"score"`, `"ai"` y `"export"`. |
| **FR-039d** | El sistema **MUST** devolver un `ImportResult` con el esquema exacto: `{ "text": string, "sections": [{ "heading": string, "start": int, "end": int, "confidence": "High"\|"Low" }], "warnings": [{ "code": string, "message": string, "severity": "Info"\|"Warning"\|"Error" }], "engineVersion": "1.0.0", "traceId": string }`. |
| **FR-039e** | El sistema **MUST** implementar el parseo tras el puerto `ICvParser` (Constitución Art. VI v1.1.0), con dos adaptadores: `PdfPigCvParser` (PDF) y `OpenXmlCvParser` (DOCX). El puerto vive en `BuildCv.Application/Features/Import/`; los adaptadores en `BuildCv.Infrastructure/Parsing/`. |
| **FR-039f** | El sistema **MUST** detectar secciones candidatas por regex sobre headers en MAYÚSCULAS (`EXPERIENCIA`, `EXPERIENCE`, `EDUCACIÓN`, `EDUCATION`, `HABILIDADES`, `SKILLS`, `PROYECTOS`, `PROJECTS`, `CONTACTO`, `CONTACT`, `PERFIL`, `PROFILE`, `RESUMEN`, `SUMMARY`, `IDIOMAS`, `LANGUAGES`, `CERTIFICACIONES`, `CERTIFICATIONS`). Cada match genera un `Section` con `confidence: High` (palabra completa en su propia línea) o `Low` (subcadena o rodeada de puntuación). |
| **FR-039g** | El sistema **MUST** sanitizar el texto extraído eliminando caracteres de control (`U+0000`–`U+001F` excepto `\n\r\t`) y normalizando a UTF-8 NFC. |
| **FR-039h** | El sistema **MUST** rechazar con `422` y código de error específico: `IMPORT_PDF_ENCRYPTED`, `IMPORT_SCANNED_PDF`, `IMPORT_DOCX_PROTECTED`, `IMPORT_DOCX_NO_TEXT`, `IMPORT_TOO_MANY_PAGES`, `IMPORT_EMPTY_FILE`. |
| **FR-039i** | El sistema **MUST** sellar `engineVersion` y `traceId` (Activity.Id) en cada `ImportResult` para reproducibilidad (paralelo a `ScoringEngine.Version`, Art. II). |

---

## Non-Functional Requirements (NFR)

| ID | Requirement |
|---|---|
| **NFR-001a** | El sistema **MUST NOT** persistir el archivo subido en disco ni en memoria más allá del response. Todo el procesamiento es en RAM; el `byte[]` se descarta tras extraer el texto (Constitución Art. III). |
| **NFR-002a** | El sistema **MUST NOT** loguear el contenido del archivo subido ni el texto extraído. Solo metadatos: `fileSize`, `mimeDeclared`, `mimeDetected`, `engineVersion`, `parseTimeMs`, `sectionsDetected`, `warningsCount`, `traceId`. |
| **NFR-005a** | El sistema **MUST** tratar el contenido del PDF/DOCX como **dato**, no como instrucción (Constitución Art. V). El texto extraído se entrega al editor como contenido inerte; cualquier "orden" incrustada se descarta. El rate-limit y los topes de tamaño (FR-039b) son la primera línea de defensa anti-abuso. |
| **NFR-007a** | El sistema **MUST** rechazar el archivo y retornar `415` si el MIME declarado o los magic bytes no coinciden (defensa contra MIME spoofing). |
| **NFR-009a** | El sistema **MUST** parsear un PDF típico de 2 páginas en <2s (P95) y un DOCX de 1 página en <1s (P95), medido en el endpoint. |
| **NFR-013a** | El uso de memoria pico del parser **MUST** ser ≤ 4× el tamaño del archivo (PdfPig carga el PDF en RAM; se rechaza >5 MB para mantener el pico ≤ 20 MB por request). |
| **NFR-018a** | El sistema **MUST** degradar con elegancia: si `PdfPig` o `OpenXml` lanzan una excepción inesperada, retornar `503 IMPORT_ENGINE_ERROR` con mensaje honesto y sin filtrar stack traces al cliente. |
| **NFR-019a** | El sistema **MUST** retornar mensajes honestos en `4xx` (códigos legibles + `detail` en español) y nunca filtrar nombres internos de archivos, paths, ni versiones de librerías. |
| **NFR-022a** | El sistema **MUST** usar el encuadre honesto de la Constitución Art. IV: el copy del frontend dice "extraer texto de tu CV", nunca "convertir a formato ATS" ni "optimizar para sistemas automáticos". |

---

## Success Criteria

- ✅ Un usuario puede subir un PDF de 2 páginas y obtener texto extraído en <2s, con ≥80% de los caracteres recuperables.
- ✅ Un usuario puede subir un DOCX de 1 página y obtener texto limpio en <1s, sin artefactos de Word.
- ✅ El rate-limit `"import"` 30/h por IP está activo, diferenciado de `"ai"` (5/h), `"score"` (60/min) y `"export"` (20/h).
- ✅ MIME spoofing detectado y rechazado con 415 (validación magic bytes, no solo header `Content-Type`).
- ✅ 0% del contenido del archivo o del texto extraído en logs.
- ✅ El editor (006) puede consumir `ImportResult` como semilla sin re-validación client-side (el contrato `ImportResult` es la fuente de verdad).

---

## Constitution Check *(mandatory — cita cada artículo aplicable)*

| Art. | Aplicación a esta feature |
|---|---|
| **Art. III** — Privacidad | **REGLA DURA**: el archivo se procesa en RAM, NUNCA se persiste. Logs sin contenido (NFR-002a). Coherente con v0 (sin guardado server-side) y v0.5 (borrador local solo en dispositivo del usuario). |
| **Art. V** — Entrada como dato | **REGLA DURA**: el contenido del PDF/DOCX es dato, no instrucción (NFR-005a). El parser extrae texto inerte. La defensa anti-prompt-injection del flujo 003 (nonces, bloques delimitados) aplica aguas abajo cuando el usuario somete el texto al score/adapt. |
| **Art. VI** — Clean Arch | El parseo vive **detrás del puerto `ICvParser`** (FR-039e, añadido en v1.1.0). `PdfPigCvParser` y `OpenXmlCvParser` son los adaptadores en `Infrastructure/Parsing/`. El Domain NO referencia ningún SDK de parseo. |
| **Art. VII** — Rate-limit | Política `"import"` 30/h por IP, **nueva en v1.1.0**. Diferenciada de `"ai"` (5/h, estricto, LLM), `"export"` (20/h, CPU) y `"score"` (60/min, determinista). El import es CPU-bound, no LLM-bound → más permisivo que `ai` pero menos que `score` porque parsear 5 MB cuesta. |
| **Art. VIII** — TDD | Tests rojos ANTES de implementación. Cobertura ≥90% en `PdfPigCvParser` y `OpenXmlCvParser`. Golden samples: 1 PDF de 2 páginas con secciones conocidas + 1 DOCX de 1 página. |
| **Art. IX** — Habeas Data | ZDR no aplica a este flujo (no enviamos a IA). El contenido **no sale del backend**, se queda en RAM del server del usuario. Sin gate contractual bloqueante. |
| **Art. I** — Cero invención | El import NO toca la IA. El texto extraído es **lo que el usuario ya escribió**; no se inventa nada. La regla se delega al editor (006) y al flujo 003 cuando se adapte. |
| **Art. II** — Determinismo | `ImportResult` es determinista para la misma entrada y versión del parser: `engineVersion` se sella (FR-039i). |

**Compliance esperado**: PASS. Esta feature es la primera que introduce carga de archivos (v0.5) y define el patrón canónico para futuros uploads (fotos, certificados en v1). El puerto `ICvParser` es **el precedente** que demuestra Clean Architecture en el dominio de archivos.

---

## Out of Scope (v0.5)

- OCR de PDFs escaneados (v1, vía servicio externo como Tesseract/Azure Read).
- Detección de tablas con estructura (filas/columnas) — v0.5 extrae texto plano; la estructura se infiere por la heurística de secciones.
- Soporte de `.rtf`, `.odt`, `.pages`, `.txt` (v1, si hay demanda).
- Múltiples CVs por usuario, historial de imports (v1 con cuentas).
- Persistencia del archivo o del texto extraído server-side (Art. III, hasta v1 con consentimiento).
- Compresión / conversión de PDF (v1).
- Extracción de imágenes (v1, opcional).
- Integración directa con ATS externos (v1+, fuera del producto).

---

## Open Questions (a resolver en `/speckit.clarify`)

- **¿Se necesita extracción de imágenes?** v0.5 las omite con `IMAGE_OMITTED`. Si el editor (006) necesita mostrar una imagen del CV original, requerirá un endpoint separado (v1).
- **¿Detección de idioma?** v0.5 no detecta idioma. La heurística de secciones soporta español e inglés (palabras comunes). Si el CV es 100% en otro idioma, las secciones no se detectan y aparece `SECTION_AMBIGUOUS` en warnings. ¿Subir una versión de la heurística con francés/portugués? (Pospuesto a v1.)
- **¿Soporte para `.doc` (formato Word 97-2003, no .docx)?** No en v0.5: el formato es binario legacy, requiere otra librería (`NPOI` con Apache-2.0 pero complejidad extra). Pospuesto a v1.
- **¿Tamaño máximo flexible?** 5 MB es conservador; muchos CVs de 10 años de experiencia pesan <2 MB. Si los usuarios se quejan, subir a 8 MB en v0.5.1.
- **¿Reintento automático si el parser lanza excepción no esperada?** No en v0.5 (defensa de CPU). 503 honesto, el usuario reintenta.

---

## Next Phase

→ Phase 1: Design — `plan.md`, `data-model.md`, `contracts/import-api.md`, `quickstart.md`, `tasks.md`.
→ Phase 2: Tasks — `/speckit.tasks` con TDD ordering.
→ Handoff: el editor (006) consume `ImportResult`; el score (002) y adapt (003) operan sobre el texto ya en cliente.
