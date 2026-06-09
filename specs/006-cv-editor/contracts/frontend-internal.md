# Contracts: 006-cv-editor (frontend only)

**Date**: 2026-06-09 | **Status**: ✅ SHIPPED (`BuildCv-web`)

> **Este feature NO tiene implementación en el backend.** Contracts informativo para mantener el estándar de 7 artifacts.

## Backend contracts consumed (unchanged)

El editor re-utiliza los contratos existentes:

| Endpoint | Method | Contract |
|---|---|---|
| `/api/v1/score` | POST | `BuildCv-api/specs/002-score-engine/contracts/score-api.md` |
| `/api/v1/adapt` | POST | `BuildCv-api/specs/003-adapt-ia/contracts/adapt-api.md` |
| `/api/v1/import` | POST | `BuildCv-api/specs/005-cv-pdf-docx-import/contracts/import-api.md` |

No hay nuevos contratos en el backend.

## Cross-references

- **Contracts completas:** `BuildCv-web/specs/006-web-cv-editor/contracts/frontend-internal.md`
