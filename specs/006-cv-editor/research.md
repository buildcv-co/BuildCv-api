# Research: 006-cv-editor (frontend only)

**Date**: 2026-06-09 | **Status**: ✅ SHIPPED (`BuildCv-web`)

> **Este feature NO tiene implementación en el backend.** Research informativo para mantener el estándar de 7 artifacts.

## Key decision: why no backend changes

El editor opera 100% en el browser:
- Texto en memoria (8 textareas)
- Persistencia local via `ICvStore` → `LocalStorageCvStore` (Constitution Art. III)
- Zod v3 validation en cliente (Constitution Art. I FR-029a)

El backend ya acepta cualquier string como input en `/api/v1/score` y `/api/v1/adapt`. No hay necesidad de un nuevo endpoint o tipo de dominio.

## Cross-references

- **Research completa:** `BuildCv-web/specs/006-web-cv-editor/research.md`
