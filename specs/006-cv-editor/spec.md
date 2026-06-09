# Spec: 006-cv-editor (frontend only)

**Date**: 2026-06-09 | **Status**: ✅ SHIPPED (`BuildCv-web`, commit `748611d`)
**Hito**: v0.5 / M4 | **Engine version**: `0.5.0` (editor)

> **Este feature NO tiene implementación en el backend.** El API no recibe cambios.
> Todos los artifacts de esta carpeta son cross-references a `BuildCv-web/specs/006-web-cv-editor/`.

## Resumen

El editor de CV es 100% frontend: 8 textareas estructurados + Zod v3 (defense in depth, Constitution Art. I FR-029a) + `ICvStore` port (Art. VI v1.1.0) con `LocalStorageCvStore` (default). **Tiptap NO instalado** — deuda técnica documentada para v1. Zustand NO instalado.

## Por qué no hay backend changes

El editor solo modifica el texto en memoria del browser. El score (002) y adapt (003) ya aceptan cualquier string como input. No hay nuevo endpoint, ni dominio, ni infra necesaria en el backend.

El flujo es:
```
Browser (editor) → texto en memoria → BFF proxy → backend /api/v1/score o /api/v1/adapt
```

El backend no distingue si el texto vino del editor, de un import, o de un paste manual.

## Contraparte frontend (source of truth)

| Artifact | Path |
|---|---|
| Spec | `BuildCv-web/specs/006-web-cv-editor/spec.md` |
| Plan | `BuildCv-web/specs/006-web-cv-editor/plan.md` |
| Research | `BuildCv-web/specs/006-web-cv-editor/research.md` |
| Data model | `BuildCv-web/specs/006-web-cv-editor/data-model.md` |
| Quickstart | `BuildCv-web/specs/006-web-cv-editor/quickstart.md` |
| Tasks | `BuildCv-web/specs/006-web-cv-editor/tasks.md` |
| Contracts | `BuildCv-web/specs/006-web-cv-editor/contracts/frontend-internal.md` |

## Sub-feature: 006b-web-cv-diff-viewer

| Artifact | Path |
|---|---|
| Spec | `BuildCv-web/specs/006-web-cv-diff-viewer/spec.md` |
| Plan | `BuildCv-web/specs/006-web-cv-diff-viewer/plan.md` |
| Research | `BuildCv-web/specs/006-web-cv-diff-viewer/research.md` |
| Data model | `BuildCv-web/specs/006-web-cv-diff-viewer/data-model.md` |
| Quickstart | `BuildCv-web/specs/006-web-cv-diff-viewer/quickstart.md` |
| Tasks | `BuildCv-web/specs/006-web-cv-diff-viewer/tasks.md` |
| Contracts | `BuildCv-web/specs/006-web-cv-diff-viewer/contracts/frontend-internal.md` |

## Constitution compliance (backend perspective)

- **Art. I** ✅ — El editor no agrega entidades que el usuario no haya tipeado (Zod rechaza en round-trip)
- **Art. III** ✅ — Persistencia local EXCLUSIVAMENTE en dispositivo del usuario (`ICvStore` → `LocalStorageCvStore`), botón "Limpiar borrador" obligatorio (FR-040a/b)
- **Art. VI** ✅ — `ICvStore` es puerto oficial v1.1.0

## Commit

- `748611d` — 006-web-cv-editor (editor con 8 textareas + Zod + ICvStore)
- `4bf92b7` — 006b-web-cv-diff-viewer (diff visual con jsdiff)
