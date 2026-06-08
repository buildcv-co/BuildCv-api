# Data Model: 007-constitution-v1.1.0 (cambios al texto constitucional)

> **Tipo:** Data model del diff de texto. No es un modelo de dominio de código, es el diff de las Reglas que se modifican.

## Reglas añadidas o modificadas

### Art. III — Reglas (modificado)

| ID | Regla | Status | Razón |
|---|---|---|---|
| FR-040 (v1.0) | "En v0, el sistema MUST procesar el CV y la vacante en memoria y NO persistirlos." | **MODIFICADO** | Ahora cubre solo persistencia server-side. |
| **FR-040 (v1.1)** | "En v0.5, el sistema MUST procesar el CV y la vacante en memoria del servidor." | **NUEVO (reemplazo)** | Habilita persistencia local. |
| **FR-040a** | "La persistencia local EXCLUSIVAMENTE en el dispositivo del usuario (localStorage, IndexedDB) está permitida para el borrador de edición." | **NUEVO** | Aclara el alcance. |
| **FR-040b** | "El sistema frontend MUST ofrecer al usuario un mecanismo explícito de 'Limpiar borrador' que purge toda persistencia local relacionada con su CV." | **NUEVO** | Cumple el espíritu de Art. III. |
| NFR-001a | "El usuario puede solicitar el borrado inmediato de su borrador en cualquier momento." | **NUEVO** | User control. |
| FR-041 (sin cambios) | "Logs no contienen contenido del CV." | OK | No afectado. |
| FR-042 (sin cambios) | "Gate ZDR para IA provider." | OK | Reforzado en Art. IX. |
| FR-043 (sin cambios) | "Minimizar datos al provider IA." | OK | No afectado. |
| FR-004 (sin cambios) | "Borrador local permanece en dispositivo del usuario." | OK | Ya estaba bien. |

### Art. I — Reglas (añadida)

| ID | Regla | Status | Razón |
|---|---|---|---|
| FR-029 (sin cambios) | "Comunicar resultado de verificación de honestidad." | OK | No afectado. |
| **FR-029a** | "El editor frontend MUST NO agregar entidades nuevas (skills, certificaciones, experiencia, empresas, cargos, fechas, métricas) que el usuario no haya escrito explícitamente. El schema validado con Zod rechaza entidades nuevas en el round-trip Markdown." | **NUEVO** | Defense in depth del lado cliente. |

### Art. VI — Puertos (modificado)

```
Antes (v1.0):
- IAiClient (Application)
- ICvParser (Application)            <-- ya planeado
- IPdfExporter (Application)
- PaymentProvider (Application)

Después (v1.1):
- IAiClient (Application)
- ICvParser (Application)            <-- ahora con PdfPig + OpenXml
- IPdfExporter (Application)
- IPaymentProvider (Application)
- ICvStore (Frontend, localStorage)  <-- NUEVO
```

### Art. VII — Rate limits (modificado)

| Política | Endpoint | Límite | Razón |
|---|---|---|---|
| `"score"` | POST /api/v1/score | 60/h por IP | Deterministic, CPU-cheap |
| `"ai"` | POST /api/v1/adapt | 5/h por IP | LLM-bound, presupuesto |
| `"export"` | POST /api/v1/export | 20/h por IP | CPU-bound, PDF |
| `"import"` | POST /api/v1/import | **30/h por IP** (NUEVO) | CPU-bound, parseo archivos |

### Art. IX — Gate ZDR (añadida)

```
Antes (v1.0):
- ZDR es un gate bloqueante.

Después (v1.1):
- ZDR es un gate bloqueante.
- Estado actual (2026-06-09): Anthropic ZDR solo Enterprise → NO se puede garantizar
  → copy público dice "puede retenerse según política del proveedor"
- Cuando Anthropic Enterprise se habilite: PR contractual + bump v1.2.0
```

## Versioning

| Campo | Antes (v1.0.0) | Después (v1.1.0) |
|---|---|---|
| Versión | 1.0.0 | 1.1.0 |
| Fecha de ratificación | 2026-06-06 | 2026-06-06 (NO cambia) |
| Fecha de última enmienda | 2026-06-06 | 2026-06-09 |
| Estado | Vigente (ratificada) | Vigente (ratificada) |

**Tipo de bump:** MENOR (1.0.0 → 1.1.0). Razón: añade capacidades, no rompe compatibilidad. M0/M1/M2 siguen funcionando sin cambios.

## Out of scope

- Cambios al código de runtime (M0/M1/M2 sin cambios)
- Implementación de features 005/006 (siguiente fase, después de merge)
- Cuentas v1.0+ (es 009-auth)
