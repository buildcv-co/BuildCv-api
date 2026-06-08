# INDEX — Registro consolidado de features (BuildCv-api)

> **Este archivo es el entry point oficial al estado del producto BuildCv.**
> Cualquier agente o humano que necesite saber "qué está hecho, qué está en curso, qué falta" debe leer esto primero.

**Última actualización:** 2026-06-08

## Estado del producto (consolidado)

| # | Feature | Hito | Status | Branch | Engine version |
|---|---|---|---|---|---|
| 001 | `mvp-cv-ats` | MVP original | 🗄️ Archivado | `main` | — |
| 002 | `score-engine` | M0 | ✅ SHIPPED | `main` | `1.0.0` |
| 003 | `adapt-ia` | M1 | ✅ SHIPPED (StubAiClient) | `main` | `1.0.0` |
| 004 | `export-pdf` | M2 | ✅ SHIPPED (QuestPDF) | `main` | `1.0.0` |
| 005 | `observability` | M3 | 📋 Planeado | — | — |
| 006 | `landing-ui` (frontend) | M0.1 | 📋 Planeado (en `BuildCv-web`) | — | — |
| 007 | `web-adapt-ui` (frontend) | M1.1 | 📋 Planeado (en `BuildCv-web`) | — | — |
| 008 | `web-export-ui` (frontend) | M2.1 | 📋 Planeado (en `BuildCv-web`) | — | — |
| 009 | `auth` | v1 | 📋 Planeado (v1) | — | — |
| 010 | `persistence` | v1 | 📋 Planeado (v1) | — | — |
| 011 | `payments` | v1 | 📋 Planeado (v1) | — | — |

## Leyenda de status

- ✅ **SHIPPED** — feature cerrada, en producción, tests pasando
- 🚧 **EN CURSO** — implementación activa
- 📋 **PLANEADO** — spec/plan/tasks escritos, esperando para implementar
- 🗄️ **ARCHIVADO** — feature antigua, conservada solo para historia

## Features SHIPPED (detalle)

### 002-score-engine (M0)

- **Spec:** [specs/002-score-engine/spec.md](./002-score-engine/spec.md)
- **Plan:** [specs/002-score-engine/plan.md](./002-score-engine/plan.md)
- **Research:** [specs/002-score-engine/research.md](./002-score-engine/research.md)
- **Data model:** [specs/002-score-engine/data-model.md](./002-score-engine/data-model.md)
- **Quickstart:** [specs/002-score-engine/quickstart.md](./002-score-engine/quickstart.md)
- **Tasks:** [specs/002-score-engine/tasks.md](./002-score-engine/tasks.md)
- **Contracts:** [specs/002-score-engine/contracts/score-api.md](./002-score-engine/contracts/score-api.md)
- **Endpoint:** `POST /api/v1/score`
- **Engine version:** `1.0.0`
- **Constitution compliance:** Art. II ✅, Art. VI ✅, Art. VIII ✅
- **Tests:** 92 (cubren motor + matcher + endpoint HTTP)
- **Commit:** `eded372` "BuildCv API (.NET 10) — motor de puntaje determinista"

### 003-adapt-ia (M1)

- **Spec:** [specs/003-adapt-ia/spec.md](./003-adapt-ia/spec.md)
- **Plan:** [specs/003-adapt-ia/plan.md](./003-adapt-ia/plan.md)
- **Research:** [specs/003-adapt-ia/research.md](./003-adapt-ia/research.md)
- **Data model:** [specs/003-adapt-ia/data-model.md](./003-adapt-ia/data-model.md)
- **Quickstart:** [specs/003-adapt-ia/quickstart.md](./003-adapt-ia/quickstart.md)
- **Tasks:** [specs/003-adapt-ia/tasks.md](./003-adapt-ia/tasks.md)
- **Contracts:** [specs/003-adapt-ia/contracts/adapt-api.md](./003-adapt-ia/contracts/adapt-api.md)
- **Endpoint:** `POST /api/v1/adapt` (rate-limited 5/h por IP, política "ai")
- **Engine version:** `1.0.0`
- **Status:** v0 usa `StubAiClient` (deterministic, sin LLM real, 0 costo). M1 habilitará `AnthropicAiClient` con Claude Sonnet 4 (gate Art. IX — ZDR contractual).
- **Constitution compliance:** Art. I ✅ (CrossEntityValidator detecta invenciones), Art. V ✅ (PromptBuilder con bloques `<DATA nonce="...">`), Art. VI ✅, Art. VII ✅
- **Tests:** 14 (Domain + Application)
- **Commit:** `68baaf2` "feat(003-adapt-ia): adaptación con LLM, cero invención (Constitution Art. I)"

### 004-export-pdf (M2)

- **Spec:** [specs/004-export-pdf/spec.md](./004-export-pdf/spec.md)
- **Plan:** [specs/004-export-pdf/plan.md](./004-export-pdf/plan.md)
- **Research:** [specs/004-export-pdf/research.md](./004-export-pdf/research.md)
- **Data model:** [specs/004-export-pdf/data-model.md](./004-export-pdf/data-model.md)
- **Quickstart:** [specs/004-export-pdf/quickstart.md](./004-export-pdf/quickstart.md)
- **Tasks:** [specs/004-export-pdf/tasks.md](./004-export-pdf/tasks.md)
- **Contracts:** [specs/004-export-pdf/contracts/export-api.md](./004-export-pdf/contracts/export-api.md)
- **Endpoint:** `POST /api/v1/export` (rate-limited 20/h por IP, política "export")
- **Engine version:** `1.0.0` (ScoreEngine), `004-export-pdf` (PdfMetadata.ModelVersion)
- **Status:** QuestPDF con Community License, layout con header/content/footer, marca de agua honesta "No es un puntaje ATS oficial".
- **Constitution compliance:** Art. I ✅ (ValidationGate bloquea Hard invenciones con 422), Art. III ✅ (PDF en memoria, sin persistencia), Art. IV ✅ (filename "cv-adapted-", watermark honesto), Art. VI ✅, Art. VII ✅
- **Tests:** 16 (Domain + Application)
- **Commit:** `635d688` "feat(004-export-pdf): export CV adaptado a PDF (Constitution Art. I, IV)"

## Features ARCHIVADAS

### 001-mvp-cv-ats (MVP original)

- **Status:** 🗄️ Archivado. La spec original (378 líneas) cubría scoring + adapt + export en un solo bloque. Se rompió en 002/003/004 para tracking granular.
- **Archive:** [specs/_archive/001-mvp-cv-ats-original/](./_archive/001-mvp-cv-ats-original/)
- **Razón del archivo:** scope demasiado grande, specs pequeñas son más testeables y revisables.

## Features PLANEADAS (sin implementar)

### 005-observability (M3)

- **Planeado:** Métricas con Prometheus, tracing distribuido con OpenTelemetry, health checks más detallados, structured logging mejorado.
- **Blocker:** Ninguno. Spec aún no escrito.

### 006-landing-ui (M0.1, frontend)

- **Planeado:** Landing page con hero, sección "cómo funciona", honesty note prominente, CTA al analizador.
- **Bloqueado por:** nada. Es trabajo puro de UI.
- **Spec:** se creará en `BuildCv-web/specs/006-landing-ui/`.

### 007-web-adapt-ui (M1.1, frontend)

- **Planeado:** UI para consumir `POST /api/v1/adapt`. Panel con el CV adaptado, delta de mejora trazado, indicador de severidad (verde/amarillo/rojo), streaming visual (M1.5).
- **Bloqueado por:** nada. Spec se creará en `BuildCv-web/specs/007-web-adapt-ui/`.

### 008-web-export-ui (M2.1, frontend)

- **Planeado:** Botón "Descargar PDF" en la UI, integración con `POST /api/v1/export`, manejo de 422 (Hard invenciones) con mensaje "regenera la adaptación".
- **Bloqueado por:** nada. Spec se creará en `BuildCv-web/specs/008-web-export-ui/`.

### 009-auth (v1)

- **Planeado:** Cuentas de usuario, OAuth con Google/LinkedIn, historial de scores y adaptaciones.
- **Bloqueado por:** gate ZDR (Art. IX) debe estar verificado contractualmente.

### 010-persistence (v1)

- **Planeado:** PostgreSQL con EF Core, migraciones automáticas, datos del usuario (CVs adaptados, scores históricos, exports).
- **Bloqueado por:** gate Habeas Data (Art. IX) — consentimiento expreso del usuario, derechos ARCO.

### 011-payments (v1)

- **Planeado:** Wompi Colombia (PSE, Nequi, Daviplata), créditos por uso, facturación conforme a DIAN.
- **Bloqueado por:** gates Art. IX (ZDR + Habeas Data) + servidor de pagos con confirmación server-side.

## Próximos pasos (recomendados)

1. **006-landing-ui** (frontend) — Trabajo puro de UI, sin dependencias del backend. Aumenta conversión.
2. **007-web-adapt-ui** (frontend) — Habilita el flujo de adaptación end-to-end desde el navegador.
3. **008-web-export-ui** (frontend) — Habilita descarga de PDF desde el navegador.
4. **005-observability** (backend) — Métricas para tomar decisiones (cuántos scores/día, cuántos exports, etc.).

## Reglas de mantenimiento

1. **Cada feature nueva DEBE tener los 7 artifacts** (spec, plan, research, data-model, quickstart, tasks, contracts). Sin excepción.
2. **El INDEX se actualiza AL COMMITEAR** el commit que cierra la feature. Status pasa de 🚧 a ✅.
3. **Features archivadas** mantienen sus archivos en `_archive/` con un README explicando por qué se archivó.
4. **Las Constitution compliance** se audita con `./scripts/constitution-check.sh` antes de marcar ✅ SHIPPED.
5. **Los tests** deben pasar 100% con `./scripts/preflight.sh` antes de marcar ✅ SHIPPED.

## Convenciones de naming

- `NNN-kebab-case-name/` — NNN es el número secuencial (3 dígitos), kebab-case para el nombre.
- Ejemplos: `002-score-engine/`, `003-adapt-ia/`, `004-export-pdf/`.
- `000-INDEX.md` (este archivo) es la única excepción al patrón numérico.

## Links externos

- **Constitution:** `BuildCv-api/.specify/memory/constitution.md` (v1.0.0, ley suprema)
- **AGENTS.md:** `BuildCv-api/AGENTS.md` (tarjeta de identidad del sub-proyecto)
- **Frontend counterpart:** `BuildCv-web/specs/` (mismo patrón, ID correlativo)
- **Spec-kit oficial:** `BuildCv-api/.specify/` (CLI, scripts bash, plantillas)
