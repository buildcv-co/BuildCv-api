# Tareas — BuildCv (MVP CV/ATS) por hitos

> **Artefacto SDD:** `specs/001-mvp-cv-ats/tasks.md` — desglose **accionable y ordenado** de tareas para construir el MVP **completo** por hitos. Materializa `spec.md` (QUÉ/POR QUÉ), `plan.md` (CÓMO), `research.md` (decisiones D01–D20), `data-model.md` (entidades/persistencia) y `contracts/api-contract.md` (endpoints), bajo las reglas duras de `.specify/memory/constitution.md`.
>
> **Idioma:** documentación en español · identificadores de código en inglés.
> **Fecha base:** 2026-06-06.

---

## Cómo leer este documento

- **ID:** `T001`, `T002`, … numeración continua y estable entre hitos.
- **`[P]` = paralelizable:** la tarea toca **archivos independientes** y no depende de otra tarea en curso; puede ejecutarse en paralelo con otras `[P]` del mismo bloque.
- **`(TEST)`:** tarea de prueba que **se escribe y debe fallar antes** de su implementación (orden TDD — Artículo VIII de la constitución). Aplica con rigor obligatorio al motor de puntaje y su pipeline NLP.
- **Refs:** cada tarea referencia el/los `FR-ID` / `US-ID` / `NFR-ID` que implementa, el **endpoint** del contrato y/o el **archivo** del árbol de `plan.md §3`, y las **decisiones** `D01–D20`.
- **Deps:** tareas que deben completarse antes.
- **Orden TDD global por hito:** (1) pruebas de contrato y unitarias → (2) implementación → (3) integración → (4) pulido.

### Mapa de hitos (este documento) ↔ fases de `plan.md`

> El esquema de hitos de **este `tasks.md`** sigue la instrucción de planeación por entregas. `plan.md §8` agrupa de forma ligeramente distinta (separa motor e IA en M1/M2 y junta base legal con cuentas en su M3). Equivalencia:

| Hito (tasks.md) | Contenido | Fases `plan.md` | v |
|---|---|---|---|
| **M0 Setup** | Solución .NET por capas, app Next.js, CI, deploy "hola mundo" punta a punta | M0 (Fase 0) | — |
| **M1 Núcleo v0 LANZABLE** | Motor TDD + keywords + `/score` + adaptación IA/streaming `/adapt` + guardarraíles + export PDF + frontend completo + rate limiting; **sin cuentas/sin guardado**; DESPLEGAR | M1+M2 (Fase 1a+1b) | **v0** |
| **M2 Cuentas + persistencia** | Identity/JWT, EF Core + entidades v1, historial, subir PDF/DOCX (+ captura de consentimiento como puerta a persistir, FR-051) | M3 parcial (Fase 3a) | v1 |
| **M3 Créditos + pagos** | Libro mayor de créditos, Wompi checkout + webhook firmado e idempotente | M4 (Fase 3b) | v1 |
| **M4 Legal + pulido + lanzamiento** | Consentimiento Habeas Data completo (ARCO/política), a11y/móvil final, métricas, landing, lanzamiento v1 | M3 resto (Fase 3a) | v1 |

> **Nota de coherencia legal (constitución Art. IX, FR-051):** como **M2 ya persiste datos personales**, la **captura mínima de consentimiento en el registro** (`data_consents` + checkbox de finalidad y transferencia internacional) viaja **en M2** (es puerta de la persistencia). El **aparato legal completo** (política publicada, aviso de privacidad, derechos ARCO, revocación) se completa en **M4**. Esto evita persistir sin base legal.

---

# M0 — Setup / Scaffolding *(esqueleto end-to-end desplegado)*

> **Objetivo:** "hola mundo" de punta a punta (Next.js → BFF → .NET → `/health`) desplegado, con CI verde. Sin FRs de producto aún (solo NFR-018 disponibilidad).
>
> **✅ ESTADO: COMPLETADO Y VERIFICADO (2026-06-06).** Solución .NET (4 `src` + 3 test) en `.slnx`, host ASP.NET Core con Serilog/ProblemDetails/OpenAPI+Scalar/versionado/health, BFF Next.js y CI. Verificado: build 0 warnings (warnings-as-errors), **8/8 tests**, `dotnet format` limpio, `next build`+lint verdes, **imagen Docker arranca con `/health/{live,ready}` → 200 en Producción**, y camino navegador→BFF→.NET probado. **Pendiente:** solo T015 (deploy real en Render+Vercel, requiere cuentas del usuario).
>
> **Ajustes vs plan:** net10.0 (no net9), pnpm (no npm), FluentAssertions 7.0.0 (licencia libre), `.slnx`, puerto dev 5080, shadcn diferido a M1.

### Andamiaje del repositorio y solución .NET

- **T001** `[P]` Crear estructura del monorepo (`backend/`, `frontend/`, `.github/workflows/`, `README.md`) según `plan.md §3`. · *Ref:* `plan.md §3`. · *Deps:* —
- **T002** `[P]` `backend/Directory.Build.props`: `Nullable enable`, `LangVersion latest`, **warnings-as-errors**. · *Ref:* `plan.md §1.1`, Art. VI. · *Deps:* T001
- **T003** `[P]` `backend/.editorconfig` (estilo para `dotnet format --verify`). · *Ref:* `plan.md §7.2`, Art. VI. · *Deps:* T001
- **T004** Crear `BuildCv.sln` + 4 proyectos `src/` (`BuildCv.Domain`, `BuildCv.Application`, `BuildCv.Infrastructure`, `BuildCv.Api`) con la **regla de dependencias** (`Domain ← Application ← Infrastructure`; `Api` compone). · *Ref:* D03, `plan.md §4.1`, Art. VI. · *Deps:* T002
- **T005** Crear 3 proyectos de test (`BuildCv.Domain.Tests`, `BuildCv.Application.Tests`, `BuildCv.Api.IntegrationTests`) con `xunit` + `FluentAssertions`. · *Ref:* `plan.md §6`. · *Deps:* T004
- **T006** `BuildCv.Domain/Common/Result.cs` (`Result<T>` para errores de dominio sin excepciones). · *Ref:* `plan.md §3`. · *Deps:* T004

### Host ASP.NET Core (composición) y salud

- **T007** `BuildCv.Api/Program.cs`: DI por capa (`AddDomain()/AddApplication()/AddInfrastructure(config)`), **Serilog** (consola dev / JSON prod, **sin contenido**), `AddProblemDetails()` + `GlobalExceptionHandler` (`IExceptionHandler`), `AddOpenApi()` + **Scalar**, versionado `/api/v1` (`Asp.Versioning.Http`), `ForwardedHeadersOptions` (X-Forwarded-For). · *Ref:* D04, `plan.md §5.1/§5.2`, Art. VI. · *Deps:* T004
- **T008** `HealthEndpoints` (`GET /health/live`, `GET /health/ready`; v0: self + presencia de `ANTHROPIC_API_KEY`). · *Ref:* `contracts §5.4`, NFR-018. · *Deps:* T007
- **T009** `[P]` `backend/Dockerfile` (imagen `mcr.microsoft.com/dotnet/aspnet:9.0`, un solo servicio, portable). · *Ref:* D16, `plan.md §1.1`. · *Deps:* T001
- **T016** `[P]` `(TEST)` Integración de salud con `WebApplicationFactory<Program>`: `/health/live` → 200, `/health/ready` → 200. · *Ref:* `plan.md §6.4`, NFR-018. · *Deps:* T005, T008

### Andamiaje Next.js + BFF

- **T010** `[P]` Scaffold Next.js 15 (App Router, RSC, `strict: true`) + Tailwind + `shadcn/ui init`. · *Ref:* D13. · *Deps:* T001
- **T011** `[P]` `frontend/lib/copy/es.ts` (todos los textos es-CO) + `t()` + `Intl` (es-CO). · *Ref:* D13, NFR-016. · *Deps:* T010
- **T012** `frontend/app/layout.tsx` (`<html lang="es-CO">`, Providers, `Toaster`, skip-link) + `globals.css` (Tailwind + tokens shadcn). · *Ref:* `plan.md §3`, NFR-012/016. · *Deps:* T010
- **T013** BFF Route Handlers **vacíos** (`app/api/{score,adapt,export,health}/route.ts`) que solo proxyean a `BACKEND_URL`. · *Ref:* D13/D14, `plan.md §3`. · *Deps:* T010

### CI y despliegue de prueba

- **T014** `.github/workflows/ci.yml`: job backend (`restore` → `build -c Release` → `dotnet format --verify-no-changes` → `test --collect coverage`) + job frontend (`npm ci` → `lint` → `test` → `next build`). · *Ref:* `plan.md §7.2`, D16. · *Deps:* T004, T010
- **T015** Deploy "hola mundo" punta a punta: backend en **Render/Railway** (Docker), frontend en **Vercel**, `BACKEND_URL` configurado, navegador → BFF `/api/health` → `.NET /health/ready` en verde. · *Ref:* D16, `plan.md §7.1`. · *Deps:* T007, T009, T013, T014

### ✅ Definition of Done — M0
- [ ] `dotnet build` + `dotnet test` + `dotnet format --verify` **verdes** en CI; `next build` + `npm test` verdes.
- [ ] Solución con 4 proyectos `src` + 3 de test, regla de dependencias respetada (Domain no referencia a nadie).
- [ ] Imagen Docker construye y arranca; `GET /health/ready` → 200 en local y en hosting.
- [ ] Frontend desplegado en Vercel llega al backend vía BFF (mismo origen) y muestra estado de salud.
- [ ] Ningún secreto en el repo (`.gitignore` cubre `*.user`, `appsettings.*.local.json`).

**Estimación relativa:** **1x** (línea base). Andamiaje mecánico; el costo está en dejar CI + deploy verdes de punta a punta.

---

# M1 — Núcleo v0 LANZABLE *(el producto entero, gratis, sin cuentas)*

> **Objetivo:** pegar CV + vacante → **puntaje explicable** → **keywords** → **recomendaciones** → **adaptación en streaming** (cero invención) → **delta de mejora** → **exportar/copiar/compartir**; en móvil; con rate limiting; **sin cuentas ni guardado**; **DESPLEGADO**.
> **Cubre:** FR-001..FR-043 (P0), FR-020 (opt.) · US-001..US-011, US-016 · NFR-001..003, 005, 006, 009..022, 025.
> **Orden TDD estricto:** primero las pruebas del motor (léxicos, matching, golden cases, determinismo).

## 1a · NLP español + léxicos (TEST-FIRST, `BuildCv.Domain.Tests`)

- **T017** `[P]` `(TEST)` `SpanishTextNormalizerTests`: `"año" ≠ "ano"`, conserva **Ñ**, protege `c#`/`.net`/`node.js`/`ci/cd`, NFKC, minúsculas `InvariantCulture`. · *Ref:* FR-016, D02, Art. VIII. · *Deps:* T006
- **T018** `[P]` `(TEST)` `ConfusableBlocklistTests` (simétrico): `java ⇎ javascript`, `c ⇎ c#`, `react ⇎ react native`, `go ⇎ mongo`, `r ⇎ ruby`. · *Ref:* FR-017, D02, Art. VIII. · *Deps:* T006
- **T019** `[P]` `(TEST)` `SpanishLemmatizerTests`: `"desarrollé" ≈ "desarrollo"`; lematización por diccionario + fallback stem (Lucene `SpanishLightStemFilter`). · *Ref:* FR-015, D02. · *Deps:* T006
- **T020** `[P]` `(TEST)` `SkillSynonymDictionaryTests`: `js=javascript`, `postgres=postgresql`, alias canónicos, bilingüe ES/EN (`pruebas unitarias=unit testing`). · *Ref:* FR-015/016, D02, NFR-017. · *Deps:* T006

### Implementación NLP (`BuildCv.Domain/Text/`)

- **T021** `ITextNormalizer` + `SpanishTextNormalizer` (orden: NFKC → proteger técnicos → minúsculas → quitar diacríticos preservando Ñ → puntuación → restaurar técnicos). *Pasa T017.* · *Ref:* FR-016, D02. · *Deps:* T017
- **T022** `ConfusableBlocklist`. *Pasa T018.* · *Ref:* FR-017, D02. · *Deps:* T018
- **T023** `ISpanishStemmer` + `SpanishLemmatizer` (`Lucene.Net.Analysis.Common` + `lemmatization-es.txt`). *Pasa T019.* · *Ref:* FR-015, D02. · *Deps:* T019
- **T024** `SkillSynonymDictionary` (`F23.StringSimilarity` para fuzzy posterior). *Pasa T020.* · *Ref:* FR-015/016, D02. · *Deps:* T020

## 1b · Diccionario de Habilidades + extracción determinista

- **T025** `BuildCv.Domain/Lexicon/skills.gazetteer.v1.yaml` (**Embedded Resource** versionado, perfil IT): por entrada `Id/Canonical/Category/Aliases/Implies/Related/Broader/ConfusableWith`. · *Ref:* FR-013/017/018, `data-model A.11`, D01. · *Deps:* T004
- **T026** `ISkillGazetteer` + `SkillGazetteer` (carga YAML **inmutable**; `TryResolve/Related/AreConfusable`; versión sellable). · *Ref:* `data-model A.11`, FR-013. · *Deps:* T025
- **T027** Definir **records de dominio** y enums (`Resumes/`, `Jobs/`, `Scoring/`): `CvDocument`, `JobPosting`, `Requirement`, `JobRequirementSet`, `MatchResult`, `ComponentScore`, `ScoreResult`, `KeywordAnalysis`, `Recommendation`, `FormatIssue`, `Adaptation`, `HonestyVerdict`, `ScoreDelta` + enums (`InputMode`, `RequirementSection`, `SkillCategory`, `MatchTier`, `Placement`, `ComponentId`, `ScoreBand`, `RecommendationType`, `HonestyStatus`). · *Ref:* `data-model §A`, FR-007/019/021. · *Deps:* T006
- **T028** `(TEST)` `EntityExtractorTests`: detecta skills/empresas/cargos/certificaciones/métricas/experiencias del texto. · *Ref:* FR-014/025, D01. · *Deps:* T027
- **T029** `EntityExtractor` (compartido **motor ↔ guard** anti-invención). *Pasa T028.* · *Ref:* FR-014/025. · *Deps:* T028, T026
- **T030** `(TEST)` `KeywordExtractorTests`: extrae `Requirement[]`, asigna importancia por **categoría × sección × frecuencia**, `ContextHash` determinista y estable. · *Ref:* FR-014, FR-006, D01. · *Deps:* T027
- **T031** `KeywordExtractor` determinista (0 tokens) → `JobRequirementSet { Requirements, ContextHash }`. *Pasa T030.* · *Ref:* FR-014. · *Deps:* T030, T026, T029

## 1c · Motor de puntaje (TEST-FIRST — activo defendible, Art. II/VIII)

- **T032** `[P]` `(TEST)` Golden cases `Scoring/GoldenCases/`: pares (CV, vacante) + `ScoreResult` esperado, cada caso documenta el "porqué" (especificación viva de explicabilidad). · *Ref:* FR-008, Art. VIII, `plan.md §6.1`. · *Deps:* T027
- **T033** `[P]` `(TEST)` `DeterminismTests`: misma entrada + misma versión ⇒ `ScoreResult` equivalente. · *Ref:* FR-006, Art. II. · *Deps:* T027
- **T034** `(TEST)` `ScoringEngineTests`: cascada de match T0..T4, **crédito parcial por confianza**, **factor de ubicación** (prominente=pleno, enterrada=parcial), **compuertas** (sin contacto `C2≤0.5`, sin experiencia `C2≤0.4`, keyword-stuffing no infla y penaliza C5), **renormalización** `m_C4=0.5` en v0. · *Ref:* FR-011/012/015/018, D01. · *Deps:* T027
- **T035** `IScoringEngine` + `ScoringEngine` **puro** (sin IO/red/reloj/aleatoriedad), Singleton: C1 45% · C2 20% · C3 20% · C4 10% · C5 5%; fórmula `Overall = 100·Σwₖmₖsₖ / Σwₖmₖ`; cascada de match; caps; **sellado** `EngineVersion`+`gazetteerVersion`. *Pasa T032/T033/T034.* · *Ref:* FR-005/006/007/008/011/012/013, D01, Art. II. · *Deps:* T034, T031, T021, T022, T023, T024
- **T036** Generador de `Recommendation[]` **ordenadas por impacto** + separación "arreglos sin invención" (`Surface/Rewrite/AddMetric/FixFormat`) vs "brechas reales" (`Learn`/`learnAdd`, nunca fabricar). · *Ref:* FR-021/022, Art. I. · *Deps:* T035
- **T037** `KeywordAnalysis` (presentes/faltantes/parciales; **faltantes ordenadas por importancia** con razón y consejo honesto). · *Ref:* FR-019. · *Deps:* T035

## 1d · Caso de uso Scoring + endpoint `POST /api/v1/score`

- **T038** `ScoreCvCommand` + `ScoreCvValidator` (FluentValidation: `cvText` 200..20000, `jobText` 100..20000). · *Ref:* FR-002/037, `contracts §5.1`. · *Deps:* T027
- **T039** `ScoreCvHandler` (extracción + `ScoringEngine`, **sin LLM**) → `ScoreResult`. · *Ref:* FR-005, NFR-021, `contracts §5.1`. · *Deps:* T035, T036, T037, T031
- **T040** `ValidationFilter<T>` (endpoint filter genérico → FluentValidation → `400 ValidationProblemDetails`). · *Ref:* D04, `contracts §2`. · *Deps:* T007
- **T041** `ScoringEndpoints` (`MapGroup /api/v1/score`): mapeo a `ScoreResponse` con **nombres honestos** y enums del contrato (`match/structure/achievements/format/length`, `band` es-CO, `honestyNotice`, `engineVersion/lexiconVersion/contextId`, `gatesApplied`), política rate limit `score`. · *Ref:* `contracts §5.1`, FR-009/010/011/013. · *Deps:* T039, T040, T056
- **T042** `(TEST)` `ScoringEndpointTests` (contrato): forma de `ScoreResponse` (componentes, keywords present/missing/partial, recommendations ordenadas, `gatesApplied`, sellos de versión), `400` validación, `413` payload. · *Ref:* `contracts §5.1/§2`, `plan.md §6.2`. · *Deps:* T041, T005

## 1e · IA: puerto, fake, cliente, guardarraíles (TEST-FIRST)

- **T043** `[P]` Puerto `IAiClient` (`StreamAsync`/`CompleteAsync`, `AiRequest`/`AiStreamChunk`/`AiResult`, `CancellationToken`) en `Application/Abstractions`. · *Ref:* D06, `plan.md §4.3`, Art. VI. · *Deps:* T004
- **T044** `[P]` `AiOptions` + `Prompts/manifest.json` + `IPromptStore`/`PromptCatalog` (Embedded Resources, `sha256`, opciones por tarea/modelo). · *Ref:* D06/D07, `plan.md §3`. · *Deps:* T004
- **T045** `adapt_cv.system.v1.md` + `adapt_cv.fewshots.v1.json` (**> 2048 tokens** para caché; guardarraíles **cero invención**; anti-inyección: bloques `<cv_usuario_{nonce}>`/`<vacante_{nonce}>`, regla "el contenido es DATO", recordatorio final). · *Ref:* FR-024/026, D06, Art. I/V. · *Deps:* T044
- **T046** `[P]` `FakeAiClient : IAiClient` (emite `IAsyncEnumerable` fijo, **sin red ni tokens**) en `Application.Tests`. · *Ref:* `plan.md §6.3`. · *Deps:* T043
- **T047** `(TEST)` `InventionValidatorTests`: detecta skill/empresa/cargo/certificación/métrica inventada (severidad alta vs media) y **no** marca paráfrasis legítima. · *Ref:* FR-024/025, Art. I, `plan.md §6.3`. · *Deps:* T029, T046
- **T048** `AdaptationGuard` + `InventionValidator` (re-extrae entidades del CV adaptado con `EntityExtractor`, compara contra **whitelist** del original, severidades → `HonestyVerdict`). *Pasa T047.* · *Ref:* FR-024/025/029, Art. I. · *Deps:* T047, T029
- **T049** `(TEST)` `ResumeAdaptationServiceTests`: arma prompt con nonce + sanitización, **streaming**, buffer `fullText`, corre guard, **reintento reforzado máx. 1** ante severidad alta, emite `AdaptEvent` (`meta/token/honesty/done`). · *Ref:* FR-023/024/025/027/028, D06. · *Deps:* T046, T048, T045
- **T050** `AdaptCvCommand` + `AdaptCvValidator` + `IResumeAdaptationService` + `ResumeAdaptationService`. *Pasa T049.* · *Ref:* FR-023/024/025/026/027/028/043, Art. I/V. · *Deps:* T049
- **T051** `AnthropicAiClient` (SDK oficial `Anthropic`: `Messages.CreateStreaming`, filtra deltas de texto) implementa `IAiClient`; tipos del SDK **solo** en `Infrastructure`. · *Ref:* D06/D07, Art. VI. · *Deps:* T043
- **T052** `[P]` `OpenRouterAiClient` (fallback, `HttpClient` OpenAI-compat) + `AiClientSelector` (primario/fallback por config/health). · *Ref:* D06. · *Deps:* T043
- **T053** `AiResilience`: pipeline **Polly v8** (`retry` 3 expo+jitter sobre 429/5xx → `timeout` 60s/intento → `circuit breaker`) sobre el `HttpClient` del SDK; **desactivar el retry interno del SDK** (control único). · *Ref:* D09, `plan.md §1.2`. · *Deps:* T051
- **T054** `GlobalExceptionHandler` (mapeo ProblemDetails): `413` payload, `429` rate-limit, `503` `BrokenCircuitException` (+`Retry-After`), `504` `TimeoutRejectedException`, `499` cancelación, `500` genérico. · *Ref:* `contracts §2`, D09, FR-030/037. · *Deps:* T007, T053

## 1f · Streaming SSE `POST /api/v1/adapt/stream` + rate limiting

- **T055** `Streaming/ServerSentEvents.cs` (`Sse.Frame`: `event:`/`data:` + `\n\n`, escape multilínea de `token`, encode UTF-8). · *Ref:* `plan.md §4.4`, `contracts §5.2`. · *Deps:* T007
- **T056** `Security/RateLimiting.cs`: políticas **por IP** `score` (20/min) y `adapt` (5/hora) + `GlobalLimiter` (50, sin cola); headers `X-RateLimit-{Limit,Remaining,Reset}`; `429` + `Retry-After`. · *Ref:* D10, FR-036/038, `contracts §3`. · *Deps:* T007
- **T057** `AdaptationEndpoints` (`MapPost /api/v1/adapt/stream`): headers `text/event-stream` + `Cache-Control: no-cache, no-transform` + `X-Accel-Buffering: no`, **`FlushAsync` por frame**, `ct = HttpContext.RequestAborted`, eventos `meta → token* → honesty → done`/`error`, heartbeat `:`, política `adapt`. · *Ref:* `contracts §5.2`, FR-027/028/029/030, NFR-010/011, D08. · *Deps:* T055, T056, T050, T054
- **T058** `(TEST)` `AdaptationStreamingTests` (integración con `FakeAiClient`): framing SSE, `event: done` con `fullText`, `event: honesty`, `429`+`Retry-After`, `503/504` (fake falla en handshake), cancelación → `499`/sin `error`. · *Ref:* `plan.md §6.4`, `contracts §5.2`. · *Deps:* T057, T046

## 1g · Export PDF `POST /api/v1/export/pdf`

- **T059** `[P]` Puerto `IPdfExporter` + `ExportPdfCommand`/`ExportPdfValidator` (`adaptedText` 200..40000, `fileName` saneado, `template` `ats-clean`) + `ExportPdfHandler`. · *Ref:* FR-033, `contracts §5.3`. · *Deps:* T004, T027
- **T060** `QuestPdfExporter` (template `ats-clean`: sin tablas/columnas que rompan el parseo; fuentes con Ñ/acentos en contenedor Linux) implementa `IPdfExporter`. · *Ref:* D12, FR-033. · *Deps:* T059
- **T061** `ExportEndpoints` (`MapPost /api/v1/export/pdf` → `application/pdf` blob, `Content-Disposition`, **no persiste**, política `score`). · *Ref:* `contracts §5.3`, FR-033/040/041. · *Deps:* T060, T056

## 1h · Frontend del flujo completo (`/analizar`)

- **T062** `[P]` `lib/api/`: `client.ts`, `types.ts` (alineado al contrato), `endpoints.ts`, `sse.ts` (**parser propio**, framing `\n\n`, `event:`/`data:`/comentarios `:`). · *Ref:* D13/D14, `contracts §8`. · *Deps:* T011
- **T063** `[P]` `(TEST)` `analyzer-reducer.test` (transiciones `idle→…→done`, nunca estado inconsistente) + `sse.test` (framing) + mapeo de ProblemDetails. · *Ref:* `plan.md §6.2`, D13/D14. · *Deps:* T010
- **T064** `lib/state/`: `analyzer-reducer.ts` (**puro**), `AnalyzerContext`, `use-adapt-stream.ts` (`fetch` + `ReadableStream` + `AbortController`). *Pasa T063.* · *Ref:* D13/D14, FR-028. · *Deps:* T063, T062
- **T065** BFF reales: `score` (proxy JSON), `adapt` (**passthrough SSE**, runtime Node, sin bufferizar), `export` (blob proxy); propagan headers de rate-limit. · *Ref:* D13/D14, `contracts §8`. · *Deps:* T013, T064
- **T066** `[P]` `input-step.tsx` + `paste-area.tsx` (dos entradas independientes, validación de longitud en cliente antes de enviar). · *Ref:* FR-001/002/037, US-001. · *Deps:* T010
- **T067** `[P]` `score-dashboard.tsx` + `score-gauge.tsx` (SVG) + `component-bars.tsx`/`component-bar.tsx` (peso+valor+a11y) + `honesty-notice.tsx`. · *Ref:* FR-007/008/009/010, US-001, NFR-013. · *Deps:* T010
- **T068** `[P]` `keyword-chips.tsx` (presentes/faltantes ordenadas) + `fix-list.tsx` ("qué arreglar" priorizado, distingue arreglo vs brecha real). · *Ref:* FR-019/021/022, US-002/003. · *Deps:* T010
- **T069** `[P]` `before-after.tsx` + `streaming-output.tsx` (`aria-live="polite"`, token a token) + `improvement-delta.tsx` (62→89 (+27) animado, requisitos resueltos vs faltantes). · *Ref:* FR-027/031/032, US-005/006, NFR-014. · *Deps:* T010
- **T070** `[P]` `action-bar.tsx` (PDF/Copiar) + `share-improvement.tsx` (tarjeta antes/después **sin PII**) + `rate-limit-notice.tsx` + `empty-states.tsx`. · *Ref:* FR-033/034/035/038, US-007/011. · *Deps:* T010
- **T071** `analyzer.tsx` + `app/analizar/page.tsx` (Server shell + cliente) cableando el flujo: pegar → `/score` → adaptar (stream) → re-`/score` con **mismo `contextId`** → **delta** (cómputo en cliente, FR-031/032) → exportar/copiar. · *Ref:* US-001..007, FR-031/032, `contracts §5.2 nota`. · *Deps:* T064, T065, T066, T067, T068, T069, T070
- **T072** `[P]` `lib/utils/demo-data.ts` (CV + vacante de ejemplo, perfil tecnología) + botón "probar con un ejemplo". · *Ref:* FR-003, US-010. · *Deps:* T011
- **T073** `[P]` Borrador local en `sessionStorage` (solo cliente; se borra al cerrar pestaña; **no** viaja al servidor salvo al ejecutar operación). · *Ref:* FR-004, US-008, Art. III. · *Deps:* T064
- **T074** Pase **móvil + a11y**: operable por teclado, foco gestionado, `prefers-reduced-motion`, contraste, color no como único canal, antes/después conmutable en móvil. · *Ref:* US-009, NFR-012/013/014/015. · *Deps:* T071

## 1i · Anti-abuso de borde + privacidad v0

- **T075** `[P]` `frontend/middleware.ts` (rate-limit de borde + headers de seguridad + **Turnstile** invisible + honeypot + debounce + tope de longitud ~20.000 chars). · *Ref:* FR-037/039, D10, US-011. · *Deps:* T010
- **T076** `[P]` Páginas marketing: `(marketing)/privacidad/page.tsx` (copy **condicionado al gate ZDR**), `como-funciona`, `preguntas-frecuentes` + `privacy-banner`. · *Ref:* FR-042, D19, NFR-025. · *Deps:* T011
- **T077** **Gate ZDR (bloqueante del copy):** resolver el checklist D19 (confirmar/archivar retención y no-entrenamiento de Anthropic para la cuenta) y fijar el copy **exacto** de FR-042; integrar el aviso de **transferencia internacional** en la UI. · *Ref:* FR-042, NFR-022, Art. IX, D19. · *Deps:* T076
- **T078** Verificación de privacidad v0 (auditoría): **sin logs de contenido** (solo metadatos), **sin persistencia** (en memoria), **minimización** del texto enviado a IA. · *Ref:* FR-040/041/043, NFR-001/002/003, Art. III. · *Deps:* T039, T050, T057
- **T080** `(TEST)` `RateLimitTests` (integración): `429` + `Retry-After` + headers `X-RateLimit-*` en `score` y `adapt`. · *Ref:* `contracts §3`, `plan.md §6.4`. · *Deps:* T056, T041, T057

## 1j · (Opcional P1) Enriquecimiento de keywords con IA

- **T081** `[P]` *(opcional, SHOULD)* `KeywordEnricher` (Haiku, `keywords.system.v1`) que añade `aiSuggestedKeywords[]` marcados `"source": "ai"` **separados del número determinista**. · *Ref:* FR-020, `contracts §7`, NFR-021. · *Deps:* T037, T051

## 1k · Despliegue v0

- **T079** Desplegar **v0 completo** (backend Render/Railway + frontend Vercel): verificar SSE en vivo (sin buffering del proxy), rate limit por IP tras proxy (`ForwardedHeaders`), flujo extremo a extremo en producción, degradación elegante si la IA cae. · *Ref:* D16, `plan.md §7.1`, FR-030, US-016. · *Deps:* T042, T058, T061, T071, T074, T078, T080

### ✅ Definition of Done — M1 (v0 LANZABLE)
- [ ] Flujo completo en prod **sin cuenta**: pegar CV+vacante → puntaje → keywords → recomendaciones → adaptación en streaming → delta → exportar/copiar/compartir.
- [ ] **Motor test-first**: golden cases + determinismo (FR-006) verdes; `"año"≠"ano"`, Ñ conservada, técnicos protegidos, `java⇎javascript` cubiertos por pruebas.
- [ ] El **número** lo produce solo el `ScoringEngine` (sin LLM en la ruta crítica); cada componente es explicable y sellado con `engineVersion`+`lexiconVersion` (FR-005/008/013, NFR-021).
- [ ] Adaptación con **cero invención verificable** (`AdaptationGuard` determinista) y `event: honesty` emitido; prompt-injection tratada como dato (FR-024/025/026/029).
- [ ] SSE real (flush por frame), **cancelación** detiene el gasto de tokens; **degradación elegante**: `/score` funciona con IA caída (FR-028/030, US-016).
- [ ] Export PDF descargable; sin persistencia ni logs de contenido (FR-040/041); copy de privacidad **coincide con el gate ZDR** (FR-042).
- [ ] Rate limiting por IP con headers + `429`/`Retry-After`; móvil + a11y operables (US-009, US-011, NFR-012..015).
- [ ] CI verde (build + format + test + coverage); contrato HTTP/SSE alineado con `contracts/api-contract.md`.

**Estimación relativa:** **5x–6x** respecto a M0. Es el grueso del MVP (motor TDD + IA/streaming + guardarraíles + frontend completo + deploy). El motor de puntaje y sus pruebas concentran el mayor esfuerzo.

---

# M2 — Cuentas + persistencia *(v1 — primera capa comercial)*

> **Objetivo:** Identity/JWT, EF Core + PostgreSQL, entidades v1, **historial**, **subir PDF/DOCX**. Incluye la **captura mínima de consentimiento en el registro** (puerta legal a la persistencia, FR-051; el aparato legal completo va en M4).
> **Cubre:** FR-044, FR-045, FR-054, FR-055 (+ FR-051 captura en registro) · US-012, US-014 · NFR-004.

## 2a · Persistencia EF Core + PostgreSQL (TEST con Testcontainers)

- **T082** `[P]` NuGet de persistencia/auth (`Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `Microsoft.AspNetCore.Identity.*`, `JwtBearer`, `Testcontainers.PostgreSql`). · *Ref:* `plan.md §1.2`. · *Deps:* T079
- **T083** `ApplicationUser : IdentityUser<Guid>` + entidades persistidas (`CvDocument`, `JobPosting`, `AnalysisResult`, `Adaptation`, `DataConsent`) con sus atributos de `data-model B.1–B.4/B.8`. · *Ref:* `data-model B.1–B.4/B.8`, FR-045/051. · *Deps:* T082, T027
- **T084** `BuildCvDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` + `IEntityTypeConfiguration<T>` por tabla (snake_case, **UUIDv7**, `timestamptz`, `jsonb` `.ToJson()` para desgloses, `CHECK`, índices, `xmin`). · *Ref:* `data-model B.0/§4`. · *Deps:* T083
- **T085** Puertos de repositorio (`ICvRepository`, `IJobRepository`, `IAnalysisRepository`, `IAdaptationRepository`, `IUserRepository`, `IConsentRepository`) en `Application/Abstractions`. · *Ref:* `plan.md §4.1`, D03. · *Deps:* T083
- **T086** Migraciones EF Core: `AddIdentity` + `AddDomain` (script idempotente `--idempotent`; advisory lock al migrar en arranque). · *Ref:* `data-model §4`. · *Deps:* T084
- **T087** `(TEST)` Repositorios contra **PostgreSQL real (Testcontainers)**: `CHECK (overall BETWEEN 0 AND 100)`, longitud de `raw_text`, índices, desgloses `jsonb`, **supresión real** (no soft-delete). · *Ref:* `data-model §4/§5`, `plan.md §6.4`. · *Deps:* T086
- **T088** Implementaciones de repositorios EF Core (`Infrastructure/Persistence/Repositories`). *Pasa T087.* · *Ref:* `data-model §B`. · *Deps:* T087
- **T089** `AddInfrastructure` wiring de persistencia + `/health/ready` añade chequeo PostgreSQL (`AddNpgSql`). · *Ref:* `contracts §5.4`, NFR-018. · *Deps:* T088

## 2b · Auth (Identity + JWT) y consentimiento en el registro

- **T090** `(TEST)` Integración auth: `register` **exige consent**, `login` (JWT), `refresh`, `409` email duplicado, `400` consentimiento ausente. · *Ref:* `contracts §6.1/§6.2`, FR-044/051. · *Deps:* T087
- **T091** Identity + JWT (access ~15 min + refresh) + `AuthEndpoints` `register/login/refresh`; en `register` se **crea `DataConsent`** (versión política, finalidades, transferencia internacional) — puerta a persistir (FR-051); regalo de bienvenida (placeholder de créditos, materializado en M3, D20). *Pasa T090.* · *Ref:* `contracts §6.1/§6.2`, FR-044/051, Art. IX. · *Deps:* T090
- **T092** Middleware JWT + `[Authorize]` en endpoints v1 + `401` con `WWW-Authenticate: Bearer`; `403` sin consentimiento vigente. · *Ref:* `contracts §4.2`, FR-051. · *Deps:* T091

## 2c · Historial (FR-045)

- **T093** Persistir, para usuario autenticado con consentimiento vigente, `AnalysisResult` y `Adaptation` (foto sellada + `jsonb` de desgloses). · *Ref:* FR-045, `data-model B.4/B.9`. · *Deps:* T088, T092
- **T094** `(TEST)` Integración historial: `GET /api/v1/adaptations` (paginado) y `GET /{id}` (`403` si no es dueño, `404` inexistente). · *Ref:* `contracts §6.5`, FR-045. · *Deps:* T093
- **T095** Endpoints de historial (`GET /api/v1/adaptations[?page&pageSize]`, `GET /api/v1/adaptations/{id}` con delta + honesty completos). *Pasa T094.* · *Ref:* `contracts §6.5`, FR-045, US-012. · *Deps:* T094
- **T096** Frontend cuentas: registro/login (con **checkbox de consentimiento previo**), vista de historial; introducir **TanStack Query** (cache de servidor v1). · *Ref:* D13, US-012, FR-051. · *Deps:* T095

## 2d · Subir CV en archivo (PDF/DOCX) + formato completo

- **T097** `[P]` `(TEST)` Parseo: `PdfPigCvParser` extrae texto+layout; `OpenXmlCvParser` (DOCX); `415` archivo corrupto/protegido/no soportado. · *Ref:* FR-054, D11. · *Deps:* T082
- **T098** Puerto `ICvParser` + `PdfPigCvParser` (`UglyToad.PdfPig`) + `OpenXmlCvParser` (`DocumentFormat.OpenXml`) en `Infrastructure/Parsing`. *Pasa T097.* · *Ref:* FR-054, D11. · *Deps:* T097
- **T099** Evaluación de **formato completa** con archivo: `m_C4 = 1.0` (detecta columnas/tablas/imágenes/capas → `formatObservations`). · *Ref:* FR-055, D11, `data-model A.1`. · *Deps:* T098, T035
- **T100** `CvParseEndpoint` (`POST /api/v1/cv/parse`, `multipart/form-data`, `413` tamaño, `415` no soportado, `inputMode: fileUpload`); el `cvText` extraído alimenta `/score` y `/adapt`. · *Ref:* `contracts §6.9`, FR-054/055, US-014. · *Deps:* T099, T092
- **T101** Frontend subir CV (PDF/DOCX) con **fallback** a pegar texto si falla la extracción. · *Ref:* US-014, FR-054. · *Deps:* T100, T071

### ✅ Definition of Done — M2
- [ ] Migraciones aplican sobre PostgreSQL; suite de **Testcontainers** verde (CHECK, índices, `jsonb`, supresión real).
- [ ] Registro/login/refresh con JWT; **no se persiste nada sin `DataConsent` vigente** (FR-051 como puerta).
- [ ] Historial consultable (CVs/vacantes/adaptaciones con puntaje y fecha); aislamiento por usuario (`403` ajeno).
- [ ] Subida PDF/DOCX extrae texto y habilita **formato completo `m_C4=1.0`**; `415` honesto con fallback a pegar (US-014).
- [ ] El motor y el contrato de v0 **no se rompen**: `/score` y `/adapt` siguen funcionando igual (capas aisladas, Art. VI).

**Estimación relativa:** **3x** respecto a M0. EF Core + Identity + Testcontainers + parseo de archivos; trabajo sustancial pero acotado por puertos ya existentes.

---

# M3 — Créditos + pagos *(v1 — monetización)*

> **Objetivo:** **libro mayor de créditos** auditable (1 adaptación = 1 crédito) + **Wompi** (Web Checkout con firma server-side + **webhook firmado e idempotente**). Única fuente de verdad = el webhook, **nunca** el redirect del navegador.
> **Cubre:** FR-046, FR-047, FR-048, FR-049, FR-050 · US-013 · NFR-007, NFR-008 · M-07.

## 3a · Libro mayor de créditos (TEST con Testcontainers)

- **T102** `[P]` Entidades `CreditLedgerEntry` (**append-only**) + `PaymentTransaction` con sus columnas/CHECK (`data-model B.6/B.7`). · *Ref:* `data-model B.6/B.7`, FR-046/047. · *Deps:* T083
- **T103** Migración `AddCreditsAndPayments` con **índices únicos de idempotencia**: `UX_credit_ledger_reason_reference`, `UX_payment_transactions_provider_reference`. · *Ref:* `data-model B.6/B.7/§4`, FR-048. · *Deps:* T102, T086
- **T104** `(TEST)` Ledger idempotente: doble `INSERT` mismo `(reason, reference)` → no-op; consumo en **una transacción** con `xmin`; `CHECK (credit_balance >= 0)` rechaza saldo negativo; saldo `= Σ delta`. · *Ref:* `data-model B.5/B.6`, FR-046/048, NFR-007. · *Deps:* T103
- **T105** `ICreditLedger` + servicio de créditos (`AddEntry`/`GetBalance`/consumo 1 crédito; **créditos de bienvenida** al registrarse, D20). *Pasa T104.* · *Ref:* FR-046, D20, `data-model B.5`. · *Deps:* T104
- **T106** `GET /api/v1/credits` (saldo + movimientos `purchase/gift/consumption`). · *Ref:* `contracts §6.3`, FR-046, US-013. · *Deps:* T105, T092
- **T107** Adaptación con créditos: `/adapt/stream` en v1 requiere JWT, `402 insufficient-credits` **antes del costo de IA**, descuenta 1 crédito al iniciar, **reembolso** (movimiento compensatorio) si falla antes del primer token; persiste la `Adaptation`. · *Ref:* `contracts §6.4`, FR-046. · *Deps:* T105, T057, T093

## 3b · Pagos Wompi (firma server-side + webhook firmado idempotente)

- **T108** `[P]` Puerto `IPaymentProvider` (`createCheckout`/`verifyWebhook`/`getStatus`) + catálogo de paquetes en **COP** (centavos). · *Ref:* D15, `contracts §6.6`, D20. · *Deps:* T004
- **T109** `WompiProvider.createCheckout`: **firma de integridad SHA256 server-side** (`SHA256("<reference><amountInCents><currency><integritySecret>")`), crea `PaymentTransaction` `pending`, **no acredita**; el `integritySecret` nunca viaja al cliente. · *Ref:* FR-047/049, D15, NFR-008, `data-model B.7`. · *Deps:* T108, T102
- **T110** `POST /api/v1/payments/wompi/checkout` (JWT): devuelve `publicKey` + `signatureIntegrity` + `checkoutUrl`, **sin secretos**. · *Ref:* `contracts §6.6`, FR-047/049. · *Deps:* T109, T092
- **T111** `(TEST)` Webhook: verificación de firma con `signature.properties` **leído dinámicamente del payload** + `timestamp` + **events secret**; firma inválida → `401` (evento descartado, sin tocar BD); **idempotencia** por `transaction.id` (reintentos no acreditan dos veces). · *Ref:* FR-048, D15, NFR-007. · *Deps:* T104
- **T112** `WompiProvider.verifyWebhook` + `POST /api/v1/webhooks/wompi`: verifica firma **antes** de la BD, confirma contra `GET /v1/transactions/{id}`, acredita **idempotente** (`reason='Purchase'`) solo si `APPROVED`, responde `200` rápido; `DECLINED/VOIDED/ERROR` registran estado sin acreditar. *Pasa T111.* · *Ref:* `contracts §6.7`, FR-048, D15, Art. IX. · *Deps:* T111, T105
- **T113** Frontend checkout: selección de paquete → redirección a Web Checkout → página de retorno (**no** acredita, informa estado) → saldo actualizado tras webhook. · *Ref:* US-013, D15. · *Deps:* T110, T106
- **T114** `[P]` *(externo, FR-050 SHOULD)* Flujo tributario antes de cobrar: RUT (CIIU TI) + Régimen SIMPLE + **factura electrónica DIAN** (elegir proveedor habilitado) + vinculación Wompi/Nequi. · *Ref:* FR-050, D18. · *Deps:* —

### ✅ Definition of Done — M3
- [ ] Libro mayor **append-only** con saldo `= Σ delta`; consumo y compra **idempotentes** (índices únicos atrapan duplicados).
- [ ] 1 adaptación = 1 crédito; `402` sin saldo **antes** del costo de IA; reembolso si falla antes del primer token.
- [ ] Checkout con **firma SHA256 server-side**; `integritySecret`/events secret **solo en servidor** (NFR-008).
- [ ] Webhook **verifica firma** (`properties` dinámico) y es la **única** fuente de acreditación; reintentos de Wompi no duplican créditos (NFR-007).
- [ ] Pruebas de idempotencia y de firma (válida/ inválida) verdes en Testcontainers.
- [ ] Flujo tributario decidido/confirmado con contador antes de cobrar en producción (FR-050, D18).

**Estimación relativa:** **2.5x** respecto a M0. Concentrado en correctitud transaccional (idempotencia, firma, conciliación) más que en volumen de código.

---

# M4 — Legal + pulido + lanzamiento *(v1 — cierre y go-live)*

> **Objetivo:** completar el cumplimiento **Habeas Data** (consentimiento, política, derechos ARCO/revocación), **a11y/móvil** final de v1, **métricas** y **landing**, y lanzar.
> **Cubre:** FR-051 (completar), FR-052, FR-053 · US-015 · NFR-004, NFR-023, NFR-024, NFR-025 · M-01..M-07.

## 4a · Consentimiento, derechos ARCO y política (Habeas Data)

- **T115** `(TEST)` Integración consentimiento/ARCO: `consent` + `consent/revoke` (estado `vigente→revocado`, constancia), `GET /me/data` (export), `DELETE /me` (supresión real + constancia de consentimiento). · *Ref:* `contracts §6.8`, FR-051/052, NFR-004. · *Deps:* T091
- **T116** Endpoints `POST /api/v1/consent` + `POST /api/v1/consent/revoke` (índice único parcial **un `Active` por usuario**; revocar **deja constancia**, no borra). *Pasa T115.* · *Ref:* `contracts §6.8`, FR-051/052, `data-model B.8`, Art. IX. · *Deps:* T115
- **T117** Derechos ARCO: `GET /api/v1/me/data` (acceso/export de CVs/vacantes/adaptaciones) + `DELETE /api/v1/me` (**supresión real** de contenido personal, conservando traza de consentimiento). · *Ref:* `contracts §6.8`, FR-052, NFR-004, `data-model §5`. · *Deps:* T088, T091
- **T118** `[P]` Publicar **Política de Tratamiento de Datos** + **aviso de privacidad** conforme a la Ley 1581 (páginas accesibles + enlaces en registro/consentimiento); ajustar copy al estado verificado del proveedor (FR-042/053). · *Ref:* FR-053, NFR-023/025, Art. IX, D17. · *Deps:* T077
- **T119** Frontend consentimiento/derechos: checkbox previo enlazando la política; panel "mis datos" (acceso/rectificar/suprimir/revocar) con confirmaciones. · *Ref:* FR-052, US-015. · *Deps:* T116, T117

## 4b · Pulido, métricas, landing y lanzamiento

- **T120** `[P]` Instrumentar **métricas M-01..M-07** sin PII (activación, mejora promedio de puntaje, retención, costo IA/adaptación, tasa de invención detectada, disponibilidad del núcleo, conversión a pago). · *Ref:* `spec §6`, `plan.md §5.1`, M-01..M-07. · *Deps:* T079
- **T121** `[P]` Pase **a11y/móvil final de v1** (registro, historial, checkout, panel de datos): WCAG (contraste, teclado, foco, `prefers-reduced-motion`, `aria-live` del streaming) extremo a extremo. · *Ref:* NFR-012/013/014/015, US-009. · *Deps:* T096, T101, T113, T119
- **T122** `[P]` Landing pulida + SEO: `page.tsx`, `opengraph-image.tsx` (antes/después dinámica), `robots.ts`/`sitemap.ts`/`manifest.ts`, JSON-LD, hero + before-after-showcase + FAQ. · *Ref:* D13, `plan.md §3`, NFR-016. · *Deps:* T012
- **T123** *(opcional)* Promover la **misma imagen Docker** a **Azure App Service (B1)** + CD GitHub Actions (`azure/webapps-deploy`) tras tests en `main` — la URL "para el CV". · *Ref:* D16, `plan.md §7.1`, Art. VI. · *Deps:* T079
- **T124** **Verificación final de constitución** (9 artículos) + cierre/registro de los `[NECESITA ACLARACIÓN]` resueltos (ZDR, tributario, umbrales, topes, datos sensibles) + **lanzamiento v1**. · *Ref:* `constitution` Gobernanza, `spec §7.3`, `research` Apéndice. · *Deps:* T114, T119, T120, T121, T122

### ✅ Definition of Done — M4 (lanzamiento v1)
- [ ] Consentimiento informado, previo y expreso antes de persistir; **un consentimiento `Active` por usuario**; revocación deja constancia y detiene tratamiento (FR-051/052).
- [ ] Derechos **ARCO** operativos: acceso/export y **supresión real** del contenido personal (NFR-004).
- [ ] **Política de tratamiento** y aviso de privacidad publicados y coherentes con el estado verificado del proveedor (FR-042/053, NFR-022/025).
- [ ] Métricas M-01..M-07 capturadas sin PII; a11y/móvil de v1 verificadas; landing + SEO publicados.
- [ ] **Repaso de los 9 artículos** sin desviaciones bloqueantes; marcadores `[NECESITA ACLARACIÓN]` cerrados o explícitamente diferidos.

**Estimación relativa:** **2x** respecto a M0. Mayormente cumplimiento legal, UI de derechos, instrumentación y pulido; bajo riesgo técnico, alta exigencia de exactitud legal.

---

## Resumen de trazabilidad (hito → cobertura)

| Hito | FR principales | US | Endpoints | Entrega |
|---|---|---|---|---|
| **M0** | — (NFR-018) | — | `/health/*` | Esqueleto desplegado |
| **M1** | FR-001..FR-043 (+FR-020 opt) | US-001..011, 016 | `/score`, `/adapt/stream`, `/export/pdf` | **v0 lanzado** |
| **M2** | FR-044, FR-045, FR-051(captura), FR-054, FR-055 | US-012, US-014 | `auth/*`, `adaptations[/{id}]`, `cv/parse` | Cuentas + historial + archivos |
| **M3** | FR-046..FR-050 | US-013 | `credits`, `payments/wompi/checkout`, `webhooks/wompi` | Monetización |
| **M4** | FR-051(completar), FR-052, FR-053 | US-015 | `consent[/revoke]`, `me/data`, `DELETE me` | Legal + lanzamiento v1 |

> **Regla de oro de orden (todos los hitos):** pruebas de contrato/unitarias **antes** de implementar (TDD, Art. VIII), con el **motor de puntaje** como caso más estricto; luego implementación, luego integración (Testcontainers en v1), luego pulido. Las tareas `[P]` solo se paralelizan si tocan archivos independientes.

---

**Archivos relacionados:** `spec.md` (FR/US/NFR canónicos) · `plan.md` (CÓMO técnico, árbol de archivos §3) · `research.md` (decisiones D01–D20) · `data-model.md` (entidades/persistencia) · `contracts/api-contract.md` (endpoints/SSE/ProblemDetails) · `.specify/memory/constitution.md` (reglas duras).
