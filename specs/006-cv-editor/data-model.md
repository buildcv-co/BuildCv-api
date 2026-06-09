# Data Model: 006-cv-editor (frontend only)

**Date**: 2026-06-09 | **Status**: ✅ SHIPPED (`BuildCv-web`)

> **Este feature NO tiene implementación en el backend.** Data model informativo para mantener el estándar de 7 artifacts.

## Backend types involved (unchanged)

El editor re-utiliza los types existentes del backend:

| Type | Location | Usage |
|---|---|---|
| `ScoreCvCommand` | `BuildCv.Application/Features/Scoring/` | Editor envía texto para scoring |
| `AdaptCvCommand` | `BuildCv.Application/Features/Adapt/` | Editor envía texto para adaptación |
| `ImportResult` | `BuildCv.Application/Features/Import/` | Import result se usa como semilla del editor |

No hay nuevos types en el backend.

## Cross-references

- **Data model completa:** `BuildCv-web/specs/006-web-cv-editor/data-model.md`
