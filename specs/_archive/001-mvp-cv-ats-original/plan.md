# Plan Técnico — BuildCv (MVP CV/ATS)

> **Feature / rama:** `001-mvp-cv-ats`
> **Artefacto SDD:** `specs/001-mvp-cv-ats/plan.md` — describe el **CÓMO** técnico que materializa `spec.md` (QUÉ/POR QUÉ, agnóstico de tecnología). Las decisiones y sus alternativas están justificadas en `research.md` (referenciadas como **D01–D20**); el contrato HTTP/SSE/errores se congela en `contracts/api-contract.md`; el modelo de datos persistido (v1) en `data-model.md`; las tareas por hito en `tasks.md`.
> **Idioma:** español (documentación) · identificadores de código en inglés.
> **Fecha base:** 2026-06-06.
> **Reglas duras (constitución):** (1) cero invención de la IA · (2) puntaje determinista y explicable sin LLM · (3) privacidad primero (v0 no persiste) · (4) encuadre honesto · (5) la entrada del usuario es DATO, no instrucciones · (6) el backend demuestra .NET profesional · (7) v0 lanzable sin fricción + test-first del motor.
>
> **Estado del plan (2026-06-07):**
> - **M0 ✅ DONE** — scaffolding limpio, build 0 warnings, **92 tests verdes** (77 Domain + 5 Application + 10 Api IntegrationTests).
> - **M1-motor ✅ DONE** — score determinista completo (FR-001..FR-022, cascada C1–C5, gates, recomendaciones, formato JSON congelado).
> - **M1-IA ⏳ PENDING** — adaptación con LLM, puertos `IAiClient`/`IPromptStore`, SSE.
> - **M2 ⏳ PENDING** — parseo PDF/DOCX (`ICvParser`), export PDF (`IPdfExporter`).
> - **M3 ⏳ PENDING** — cuentas, consent Ley 1581, Habeas Data.
> - **M4 ⏳ PENDING** — pagos Wompi, créditos, webhooks.
>
> **Stack y layout reales (verificados en `src/`):**
> - **.NET 10 LTS** (`global.json` 10.0.100, `rollForward: latestFeature`).
> - **Solución** `BuildCv.slnx` (formato XML moderno, no `.sln`).
> - **Layout:** `src/{BuildCv.Domain, BuildCv.Application, BuildCv.Infrastructure, BuildCv.Api}` + `tests/{BuildCv.Domain.Tests, BuildCv.Application.Tests, BuildCv.Api.IntegrationTests}`. **No hay carpetas `backend/` ni `frontend/`**; este repo contiene solo el backend .NET. El frontend Next.js vive en el directorio hermano **`../BuildCv-web/`** (repositorio independiente).
> - **CI:** `dotnet-version: '10.0.x'`, `dotnet build -c Release --no-restore`, `dotnet format --verify-no-changes`, `dotnet test -c Release --no-build --collect:"XPlat Code Coverage"`.
> - **Docker:** `Dockerfile` multi-stage con imagen `mcr.microsoft.com/dotnet/aspnet:10.0`, expone `$PORT`.
> - **Hosting v0:** Render (blueprint `render.yaml`, `Ai__ApiKey` con `sync: false`).
>
> **Capas implementadas hoy (jun-2026):**
> - `BuildCv.Domain` — **PURO** (0 paquetes externos, sin IO/reloj/red). `Text/` (normalizer, stemmer, section-splitter, similarity, confusables), `Lexicon/` (gazetteer YAML), `Resumes/` (`CvAnalyzer`, `CvAnalysis`), `Jobs/` (`JobAnalyzer`, `Requirement`, `JobRequirementSet`), `Scoring/` (engine, matcher, scanner, components, recomendaciones, gates). `Common/Result.cs`.
> - `BuildCv.Application` — **solo** `Features/Scoring/{ScoreCvCommand, ScoreCvHandler, ScoreCvValidator}` + `DependencyInjection.cs`. Puertos de IO **aún no introducidos** (M1+).
> - `BuildCv.Infrastructure` — **solo** `Lexicon/skills.gazetteer.v1.yaml` (EmbeddedResource) + `DependencyInjection.cs`.
> - `BuildCv.Api` — `Endpoints/{ScoringEndpoints, HealthEndpoints}`, `Contracts/{ScoreResponse, ScoreResponseMapper}`, `Errors/`, `Filters/`, `Health/`, `Program.cs`.
>
> **Endpoints implementados (3):** `POST /api/v1/score`, `GET /health/live`, `GET /health/ready`. **Todo lo demás de este plan es aspiracional** (incluidos los archivos de `Application/Abstractions/`, `Infrastructure/{Ai,Export,Parsing,Persistence,Payments}/`, y los endpoints `adapt/stream`, `export/pdf`, `auth/*`, `payments/*`, `webhooks/*`, `consent`, `me`, `cv/parse`) **hasta que un PR los introduzca**.

---

## 1. Contexto técnico

### 1.1 Lenguajes y runtimes

| Componente | Lenguaje / Runtime | Versión | Notas |
|---|---|---|---|
| Backend (API + dominio + motor) | C# / .NET | **.NET 10 LTS** (`net10.0`) | `global.json` 10.0.100, `rollForward: latestFeature`; `Nullable enable`, `LangVersion latest`, *warnings as errors* (ver `Directory.Build.props`) |
| Frontend ✅ | TypeScript / Node | **Next.js 16** (App Router) · Node 22 | RSC por defecto; `strict: true` en `tsconfig` (en directorio hermano `../BuildCv-web/`) |
| Estilos / UI ✅ | Tailwind v4 + diseño custom | Fraunces + Geist, tema oscuro cálido | Diseño propio sin shadcn/ui (D13) |
| Infraestructura | Docker | imagen `mcr.microsoft.com/dotnet/aspnet:10.0` | Multi-stage; respeta `$PORT` (Render) |

### 1.2 Dependencias clave (NuGet — backend)

> **Leyenda:** ✅ instalado en disco (verificado en `src/<proyecto>/*.csproj` el 2026-06-07) · ⏳ planeado para el hito indicado.

| Área | Paquete(s) | Versión | Hito | Decisión |
|---|---|---|---|---|
| Versionado API | `Asp.Versioning.Http` | `10.0.0` ✅ | M0 ✅ | D04 |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` | `10.0.8` ✅ | M0 ✅ | D04 |
| OpenAPI UI | `Scalar.AspNetCore` | `2.14.14` ✅ | M0 ✅ | D04 |
| Logging | `Serilog.AspNetCore` | `10.0.0` ✅ | M0 ✅ | D04 |
| Logging sink | `Serilog.Sinks.Console` | `6.1.1` ✅ | M0 ✅ | D04 |
| Validación | `FluentValidation` | `12.1.1` ✅ | M1 ✅ | D04 |
| DI abstractions | `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.8` ✅ | M0 ✅ | D03 |
| YAML (gazetteer) | `YamlDotNet` | `18.0.0` ✅ | M1 ✅ | D03 |
| Tests | `xunit` | `2.9.3` ✅ | M0+ | §6 |
| Tests | `xunit.runner.visualstudio` | `2.9.3` ✅ | M0+ | §6 |
| Tests | `FluentAssertions` | `7.0.0` ✅ | M0+ | §6 |
| Tests (integration) | `Microsoft.AspNetCore.Mvc.Testing` | transitivo ✅ | M1 ✅ | §6.4 |
| IA (transporte) | `Anthropic` (SDK oficial C#) ⏳ | — | M1-IA | D06, D07 |
| IA (fallback) | `OpenRouterAiClient` sobre `HttpClient` (formato OpenAI-compat) ⏳ | — | M1-IA | D06 |
| NLP español | `Lucene.Net.Analysis.Common`, `F23.StringSimilarity` ⏳ | — | M1-IA | D02 (D02 base: stemmer y fuzzy ya implementados a mano en `Domain/Text/`) |
| Resiliencia | `Microsoft.Extensions.Http.Resilience` (Polly v8) ⏳ | — | M1-IA | D09 |
| Export PDF | `QuestPDF` ⏳ | — | M2 | D12 |
| Parseo CV (v1) | `UglyToad.PdfPig` (PDF), `DocumentFormat.OpenXml` (DOCX) ⏳ | — | M2 | D11 |
| Persistencia (v1) | `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` ⏳ | — | M3 | — |
| Auth (v1) | `Microsoft.AspNetCore.Identity.*` + JWT ⏳ | — | M3 | refuerza .NET |
| Testcontainers (v1) | `Testcontainers.PostgreSql` ⏳ | — | M3 | §6.4 |

### 1.3 Dependencias clave (npm — frontend) ✅ directorio hermano

> El frontend Next.js vive en el directorio hermano `../BuildCv-web/` (repositorio independiente); este repositorio no contiene dependencias npm.
>
> Implementado (Next.js 16, React 19, TypeScript 5, Tailwind v4, diseño custom sin shadcn/ui). Pendiente M1-IA: SDK Anthropic, QuestPDF (export PDF), prompts versionados. Parser SSE propio (~40 líneas).

### 1.4 Almacenamiento

- **v0 (M0–M2):** **sin capa de persistencia**. El CV y la vacante se procesan **en memoria** y se descartan tras responder (FR-040, NFR-001, D17). El rate limit vive en memoria (instancia única, D10). El borrador del frontend vive en `sessionStorage` (solo cliente, se borra al cerrar pestaña; nunca viaja al servidor salvo al ejecutar una operación — FR-004).
- **v1 (M3+):** **PostgreSQL** (Neon/Supabase/Railway) vía **EF Core** tras puertos (`ICvRepository`, `IUserRepository`, etc.), aislado en `Infrastructure/Persistence`. Migraciones EF Core. El modelo persistido se define en `data-model.md`.

### 1.5 Testing

- **Backend:** xUnit + FluentAssertions. **TDD obligatorio del motor de puntaje** (golden cases, determinismo). Tests de aplicación con `FakeAiClient` (sin red ni tokens). Integración con `WebApplicationFactory<Program>`; en v1, base PostgreSQL efímera con **Testcontainers** (§6).
- **Frontend:** tests del reducer puro (`analyzer-reducer`) y del parser SSE (`lib/api/sse`), más mapeo de ProblemDetails (D13/D14).

### 1.6 Plataforma destino

| Capa | v0 (lanzar barato) | v1 / "versión para el CV" |
|---|---|---|
| API .NET | Render o Railway (Docker) | **Azure App Service (B1)** con CI/CD GitHub Actions (D16) |
| Frontend | Vercel | Vercel (runtime Node / Fluid Compute para el SSE — D14) |
| Base de datos | — (no hay) | PostgreSQL gestionado (Neon/Supabase/Railway) |

La **misma imagen Docker** se promueve de Render/Azure sin cambios → criterio de costos + señal de dominio de Azure (D16).

### 1.7 Metas de rendimiento

- **NFR-009 — Análisis determinista:** percibido como inmediato; objetivo **< 300 ms** de cómputo para entradas típicas (CV ~3k palabras + vacante ~1k), excluyendo latencia de red. El motor es CPU puro y sin I/O → trivialmente rápido. *(Umbral exacto: [NECESITA ACLARACIÓN — Umbrales] de `spec.md`.)*
- **NFR-010 — Adaptación con IA:** **primer token visible** lo antes posible vía streaming SSE; no se espera al final. `MaxTokens=4000` para la salida (D07).
- **NFR-011 — Cancelación:** el `CancellationToken` corta el `await foreach` y, aguas abajo, el streaming del SDK → cerrar la pestaña detiene el gasto de tokens de inmediato (D08, §5.5).

### 1.8 Restricciones

- El **número de puntaje no puede depender del LLM** (FR-005/006, NFR-021): el motor vive en `Domain`, puro, sin red/DB/reloj/aleatoriedad.
- **v0 sin auth, sin DB, sin pagos** (FR-040; D03/D05): un solo servicio Dockerizado, lanzable sin fricción.
- **Privacidad por diseño:** prohibido loguear contenido de CV/vacante; solo metadatos (FR-041, NFR-002).
- **Encuadre honesto en el contrato:** los DTO se llaman `MatchScore`/`ReadabilityScore`, nunca "ATS oficial" (FR-009, NFR-020).
- **Copy de privacidad condicionado al gate ZDR** (FR-042, NFR-022, D19): no afirmar "retención cero" hasta confirmación contractual archivada.
- **Mínimo de caché de prompt** (Sonnet 4.6 = 2048 tokens): el system de adaptación se dimensiona > 2048 tokens o no se marca caché (D06).

---

## 2. Verificación contra la Constitución

| Principio (regla dura) | Cómo lo cumple este plan | ¿Desviación? |
|---|---|---|
| **1. Cero invención de la IA** | Guardarraíles en el system prompt (versionado) + bloques delimitados con nonce (entrada=dato) + `AdaptationGuard`/`InventionValidator` determinista que compara entidades de la salida contra whitelist del original, con severidades y reintento reforzado máx. 1 (D06, FR-024/025, US-004). | No |
| **2. Puntaje determinista y explicable, sin LLM** | `ScoringEngine` como **servicio de dominio puro** en `Domain` (sin I/O), Singleton, con léxicos inmutables inyectados como datos; produce `ScoreResult` con `ScoreBreakdown[]` + `Recommendation[]`; fórmula con renormalización por medibilidad (D01, FR-005/006/007/008, NFR-021). | No |
| **3. Privacidad primero (v0 no guarda)** | v0 **sin** capa de persistencia (procesa en memoria); logs sin contenido; minimización del texto enviado a IA; gate ZDR antes de cualquier copy de "retención cero" (D17/D19, FR-040/041/042/043, NFR-001/002/003). | No |
| **4. Encuadre honesto** | Aviso "coincidencia + legibilidad, no ATS oficial" junto al puntaje; DTO `MatchScore`/`ReadabilityScore`; banda cualitativa con número rector; copy de privacidad condicionado a verificación (FR-009/010, NFR-020/022). | No |
| **5. La entrada del usuario es DATO** | System prompt ordena tratar `<cv_usuario>`/`<vacante>` como datos; nonce aleatorio + sanitización + recordatorio final (last-instruction wins); el motor no ejecuta texto del usuario (D06, FR-026, NFR-005). | No |
| **6. El backend demuestra .NET de calidad** | Clean Architecture pragmática en 4 capas por features; Minimal APIs + `TypedResults` + endpoint filters; `IAsyncEnumerable` + `CancellationToken`; resiliencia Polly v8; rate limiting nativo; OpenAPI; ProblemDetails (RFC 9457); tests de integración con Testcontainers (D03/D04/D09, todo el §4–§6). | No |
| **7. v0 lanzable sin fricción + test-first del motor** | M0–M2 entregan v0 sin cuentas/DB/pagos; el motor se construye **test-first** con golden cases; degradación elegante mantiene el núcleo si la IA cae (D01/D03, FR-030, NFR-018, US-016). | No |

**Desviaciones declaradas:** ninguna bloqueante. Riesgos vigilados (no desviaciones): (a) **sobre-ingeniería** de 4 proyectos para ~3 casos de uso — mitigada con la lista "qué NO construir" (D05); (b) **percepción enterprise** Minimal APIs vs Controllers — mitigada porque la capa `Api` es *presentation-agnostic* y sustituible sin tocar `Application/Domain` (D04); (c) **doble reintento SDK+Polly** — se centraliza en Polly desactivando el retry interno del SDK (D06/D09, decisión a documentar en código).

---

## 3. Estructura del proyecto

> **Este repo contiene solo el backend .NET.** El frontend Next.js vive en el directorio hermano **`../BuildCv-web/`** (repositorio independiente). Los prompts versionados viajarán como **Embedded Resources** dentro del backend (M1-IA, D06) — el dominio no tiene IO.

```
BuildCv-api/                              # raíz (solo backend .NET 10)
├── .specify/
│   └── memory/
│       └── constitution.md               # reglas duras del proyecto
├── specs/
│   └── 001-mvp-cv-ats/
│       ├── spec.md                        # QUÉ/POR QUÉ (canónico)
│       ├── plan.md                        # ESTE documento (CÓMO)
│       ├── research.md                    # decisiones D01–D20
│       ├── data-model.md                  # modelo persistido (v1)
│       ├── quickstart.md
│       ├── contracts/
│       │   └── api-contract.md            # endpoints + SSE + ProblemDetails
│       └── tasks.md                       # tareas por hito M0–M4
├── PLANEACION.md                          # estrategia general (base)
├── README.md
├── AGENTS.md                              # tarjeta de identidad del repo para agentes
├── .github/
│   └── workflows/
│       └── ci.yml                         # build + format + test + coverage
├── .opencode/
│   ├── opencode.json                      # carga AGENTS.md + rules/ + skills/
│   ├── rules/                             # arquitectura · seguridad · calidad · backend-dotnet
│   ├── skills/                            # constitution-compliance · dotnet-tdd
│   └── agents/                            # backend-dotnet · dotnet-qa
│
├── BuildCv.slnx                           # solución (formato XML moderno, no .sln)
├── Directory.Build.props                  # Nullable, warnings-as-errors, LangVersion
├── Directory.Packages.props               # versiones NuGet centralizadas
├── global.json                            # SDK .NET 10.0.100, rollForward: latestFeature
├── .editorconfig                          # estilo (4 espacios .cs, 2 en .json/.yml)
├── Dockerfile                             # multi-stage, mcr.microsoft.com/dotnet/aspnet:10.0
├── render.yaml                            # blueprint Render (Docker, env Ai__ApiKey sync:false)
│
├── src/
│   ├── BuildCv.Domain/                    # PURO: 0 paquetes externos, sin IO/reloj/red
│   │   ├── Common/Result.cs               # Result<T> (errores de dominio sin excepciones)
│   │   ├── Text/
│   │   │   ├── ITextNormalizer.cs         # + SpanishTextNormalizer (NFKC, protege técnicos, conserva Ñ, D02)
│   │   │   ├── ISpanishStemmer.cs         # + SpanishLightStemmer (D02)
│   │   │   ├── SectionSplitter.cs         # heurística de secciones (contacto, experiencia, etc.)
│   │   │   ├── StringSimilarity.cs        # Jaro-Winkler / Levenshtein normalizado (D02)
│   │   │   └── ConfusableBlocklist.cs     # java⇎javascript, c⇎c#, node⇎node.js (FR-017)
│   │   ├── Lexicon/
│   │   │   ├── ISkillGazetteer.cs         # puerto: lectura del gazetteer
│   │   │   ├── SkillGazetteer.cs          # adaptador: YamlDotNet carga EmbeddedResource
│   │   │   └── SkillEntry.cs              # record: Id, Canonical, Category, Aliases, Implies, Related, Broader, ConfusableWith
│   │   ├── Resumes/
│   │   │   ├── CvAnalyzer.cs              # orquesta normalizer + scanner + section-splitter
│   │   │   └── CvAnalysis.cs              # record: Profile + SectionsPresent + HasContact + HasExperience + ActionVerbCount + QuantifiedAchievementCount + WordCount + MaxSkillRepetition
│   │   ├── Jobs/
│   │   │   ├── JobAnalyzer.cs             # extrae Requirement[] + contextHash (FR-031)
│   │   │   ├── Requirement.cs             # record: CanonicalId, Display, Category, Section, Weight (+ enum RequirementSection)
│   │   │   └── JobRequirementSet.cs       # record: Requirements + ContextHash
│   │   └── Scoring/
│   │       ├── IScoringEngine.cs          # contrato puro
│   │       ├── ScoringEngine.cs           # ALGORITMO PURO, determinista, sin I/O, Version="1.0.0" (D01)
│   │       ├── ISkillMatcher.cs           # + SkillMatcher (cascada C1: alias → C2: stem → C3: related → C4: fuzzy con blocklist)
│   │       ├── SkillScanner.cs            # detección de skills en texto del CV
│   │       ├── CvProfile.cs               # record: SkillPlacements, Tokens, Stems
│   │       ├── ScoreResult.cs             # record + enums ComponentId (C1..C5) y ScoreBand (Bajo/Medio/Bueno/Fuerte)
│   │       │                                # contiene: Overall, Band, Disclaimer, Components, Keywords, Recommendations, FormatIssues, GatesApplied, EngineVersion, LexiconVersion, ContextHash
│   │       ├── MatchResult.cs             # record + enums MatchTier (None/Exact/Alias/Lemma/Related/Fuzzy) y Placement (Prominent/Buried/NotFound)
│   │       ├── KeywordAnalysis.cs         # KeywordView + KeywordAnalysis (present/missing/partial)
│   │       └── Recommendation.cs          # record + enum RecommendationType (Surface/Rewrite/AddMetric/FixFormat/Learn)
│   │
│   ├── BuildCv.Application/
│   │   ├── DependencyInjection.cs         # AddApplication: registra IScoringEngine y los servicios de dominio
│   │   └── Features/Scoring/
│   │       ├── ScoreCvCommand.cs          # record: (string CvText, string JobText) — sin locale
│   │       ├── ScoreCvHandler.cs          # orquesta: CvAnalyzer + JobAnalyzer + ScoringEngine
│   │       └── ScoreCvValidator.cs        # FluentValidation: CV 200..20000, Job 100..20000, NotBeIdentical
│   │
│   ├── BuildCv.Infrastructure/
│   │   ├── DependencyInjection.cs         # AddInfrastructure: registra ISkillGazetteer con YAML embebido
│   │   └── Lexicon/
│   │       └── skills.gazetteer.v1.yaml   # EmbeddedResource, inmutable, "v1" sellado en ScoreResult (FR-013)
│   │
│   └── BuildCv.Api/
│       ├── Program.cs                     # composition root: AddApiVersioning, AddOpenApi, Scalar (solo Development), Serilog, RateLimit, HealthChecks
│       ├── Endpoints/
│       │   ├── ScoringEndpoints.cs        # MapGroup /api/v{version}/score, RequireRateLimiting("deterministic")
│       │   └── HealthEndpoints.cs         # /health/live, /health/ready
│       ├── Contracts/
│       │   ├── ScoreResponse.cs           # DTOs (PascalCase en C# → camelCase en JSON)
│       │   └── ScoreResponseMapper.cs     # mapea ScoreResult → ScoreResponse (enums → strings honestos)
│       ├── Errors/
│       │   └── GlobalExceptionHandler.cs  # IExceptionHandler → ProblemDetails (RFC 9457)
│       ├── Filters/
│       │   └── ValidationFilter.cs        # endpoint filter genérico FluentValidation → 400
│       ├── Health/
│       │   └── AiConfigHealthCheck.cs     # verifica presencia de Ai:ApiKey (no la muestra)
│       ├── appsettings.json               # Logging, Cors, Ai (vacio en dev)
│       └── appsettings.Development.json   # (gitignored, local)
│
└── tests/
    ├── BuildCv.Domain.Tests/              # 77 tests: TDD del motor (xUnit + FluentAssertions)
    │   ├── Text/
    │   │   ├── SpanishTextNormalizerTests.cs   # año≠ano, conserva Ñ, protege c#/.net/node.js
    │   │   ├── SpanishLightStemmerTests.cs
    │   │   ├── SectionSplitterTests.cs
    │   │   ├── StringSimilarityTests.cs        # Jaro-Winkler, Levenshtein
    │   │   └── ConfusableBlocklistTests.cs     # java⇎javascript, postgres≡postgresql
    │   ├── Lexicon/
    │   │   └── SkillGazetteerTests.cs
    │   ├── Resumes/
    │   │   └── CvAnalyzerTests.cs
    │   ├── Jobs/
    │   │   └── JobAnalyzerTests.cs
    │   └── Scoring/
    │       ├── ScoringEngineTests.cs      # C1..C5, compuertas, renormalización
    │       ├── ScoringEngineDeterminismTests.cs  # misma entrada ⇒ mismo score (FR-006)
    │       ├── SkillMatcherTests.cs       # cascada + blocklist
    │       ├── SkillScannerTests.cs
    │       └── GoldenCases/               # pares (CV, vacante) con ScoreResult esperado
    ├── BuildCv.Application.Tests/         # 5 tests: handler + validator
    │   └── Features/Scoring/
    │       ├── ScoreCvHandlerTests.cs
    │       └── ScoreCvValidatorTests.cs
    └── BuildCv.Api.IntegrationTests/      # 10 tests: WebApplicationFactory<Program>
        ├── BuildCvApiFactory.cs           # arranca el host en memoria
        ├── ScoringEndpointTests.cs        # 200 OK, 400 (cvText corto, jobText corto, idénticos), shape JSON
        ├── ScoreResponseShapeTests.cs     # alineación con ScoreResponse (camelCase, enums, GatesApplied)
        └── HealthEndpointTests.cs         # /health/live 200, /health/ready
```

### 3.1 Lo que NO está todavía (planeado, bloqueado por hitos futuros)

> **Esta lista describe el diseño aspiracional de v1.** Cada bloque entra al repo en el hito indicado (M1-IA / M2 / M3 / M4) mediante su PR correspondiente.

- `BuildCv.Application/Abstractions/{IAiClient, ICvParser, IPdfExporter, IPromptStore}.cs` (M1-IA, M2) — puertos de IO.
- `BuildCv.Application/Features/{Adaptation, Keywords, Export}/*` (M1-IA, M2).
- `BuildCv.Application/Prompts/{adapt_cv.system.v1.md, adapt_cv.fewshots.v1.json, manifest.json, …}` (M1-IA) — prompts como Embedded Resources con `sha256`.
- `BuildCv.Application/Configuration/AiOptions.cs` (M1-IA) — model IDs, effort, MaxTokens, temperatura.
- `BuildCv.Infrastructure/{Ai, Export, Parsing, Persistence, Payments}/*` (M1-IA, M2, M3, M4).
- `BuildCv.Api/Endpoints/{AdaptationEndpoints, ExportEndpoints, PaymentEndpoints, AuthEndpoints, ConsentEndpoints, WebhooksEndpoints, MeEndpoints, CvParseEndpoints}.cs` (M1-IA, M2, M3, M4).
- `BuildCv.Api/{Filters, Streaming, Security, Habeas}/*` extendidos (M1-IA, M3).
- Repositorio `BuildCv-web` (Next.js 16 + BFF Route Handlers + máquina de estados `useReducer` + SSE passthrough) — directorio hermano, repositorio independiente.

---

## 4. Arquitectura

### 4.1 Vista general

**Clean Architecture pragmática en 4 capas** (D03), con la regla de dependencias apuntando hacia adentro:

```
        Domain  ←  Application  ←  Infrastructure
                        ↑                ↑
                        └──── Api ────────┘     (composición / arranque)
```

- `Domain` no referencia a nadie (contiene el `ScoringEngine` puro y los léxicos).
- `Application` solo referencia `Domain`; define **puertos** (`IAiClient`, `ICvParser`, `IPdfExporter`, `IPromptStore`; en v1 `ICvRepository`, `IPaymentProvider`) y los casos de uso, **organizados por feature** (`Features/Scoring`, `Features/Adaptation`, `Features/Keywords`, `Features/Export`).
- `Infrastructure` implementa los puertos (SDK Anthropic, QuestPDF; en v1 EF Core, PdfPig/OpenXML, Wompi).
- `Api` solo compone (DI + endpoints Minimal API) y no contiene lógica de negocio.

**Estilo de API (D04):** Minimal APIs con `MapGroup` por feature, `TypedResults`, *endpoint filters* para validación, OpenAPI integrado + UI Scalar, versionado por segmento (`/api/v1/...`). DI nativo con un método de registro por capa (`AddDomain()` / `AddApplication()` / `AddInfrastructure(config)`) encadenados en `Program.cs`. Sin MediatR/CQRS/AutoMapper en v0 (D05).

**Flujo extremo a extremo (v0):**

```
Frontend /analizar (máquina de estados useReducer)
   │  POST /api/score (BFF) ──▶ .NET POST /api/v1/score (determinista, sin LLM)
   │       └─ ScoreCvHandler → EntityExtractor + ScoringEngine → ScoreResult (JSON)
   │  POST /api/adapt (BFF, passthrough SSE) ──▶ .NET POST /api/v1/adapt/stream
   │       └─ ResumeAdaptationService → IAiClient.StreamAsync (token a token)
   │            → AdaptationGuard (cero invención) → event honesty + done
   │  (al terminar el stream) POST /api/score de nuevo → delta de mejora (FR-031/032)
   │  POST /api/export (BFF) ──▶ .NET POST /api/v1/export/pdf → QuestPDF (blob)
```

### 4.2 Motor de puntaje como dominio puro (núcleo defendible)

`ScoringEngine` (en `Domain`) es el activo más testeado y la pieza que **demuestra .NET de calidad**. Es **puro**: entra data, sale data; sin red, DB, reloj ni aleatoriedad → **determinista** y trivial de testear con TDD (D01, FR-005/006).

```csharp
public interface IScoringEngine
{
    // Entradas ya analizadas; salida explicable. Sin async: es CPU puro.
    ScoreResult Evaluate(CvAnalysis cv, JobRequirementSet job);
}
```

- **Componentes ponderados** (FR-007, D01): C1 Match keywords/skills **45%** · C2 Estructura parseable **20%** · C3 Verbos de acción/logros **20%** · C4 Formato seguro **10%** · C5 Longitud/densidad **5%**. Cada componente devuelve subpuntaje + peso + **medibilidad `m_c`** + evidencia.
- **Fórmula con renormalización** (FR-011): `Overall = 100 × ( Σ wₖ·mₖ·sₖ ) / ( Σ wₖ·mₖ )`. En v0, con solo texto pegado, `m_C4 = 0.5` (formato parcialmente observable) y el resto `1.0`; así no se premia ni castiga lo no evaluado, y habilita el upsell honesto a v1 (archivo → `m_C4 = 1.0`).
- **Matching inteligente** (FR-015/016/017/018, D02): cascada de 4 niveles (T0 exacto → T1 alias/`implies` → T2 lema/stem → T3 relacionado → T4 fuzzy) con **crédito parcial por confianza** y **factor de ubicación** (prominente = pleno; enterrada = parcial). Colaboran `SpanishTextNormalizer` (conserva Ñ, protege `c#`/`.net`), `SpanishLemmatizer`, `SkillSynonymDictionary` y `ConfusableBlocklist` (java⇎javascript). El gazetteer es un Embedded Resource inmutable → el motor sigue puro y Singleton.
- **Compuertas (caps)** (FR-012): sin contacto → `C2 ≤ 0.5`; sin experiencia detectable → `C2 ≤ 0.4`; keyword stuffing no infla y penaliza C5; se comunica en las recomendaciones.
- **Explicabilidad** (FR-008): `ScoreResult` incluye `ScoreBreakdown[]` y `Recommendation[]` priorizadas por impacto ponderado, separadas en "arreglos sin invención" vs "brechas reales / aprende-añade" (FR-021/022). El LLM **no** calcula ni explica el número.
- **Sellado de versión** (FR-013): cada `ScoreResult` lleva la versión del motor y del gazetteer/léxicos → reproducibilidad y comparaciones válidas en el tiempo.
- **Recalcular tras adaptar** (FR-031): el **mismo** motor se ejecuta sobre el CV adaptado reutilizando el mismo contexto (mismos requisitos extraídos, misma versión) → la mejora "62 → 89" es honesta y comparable.

### 4.3 IA tras abstracción (`IAiClient` + `IResumeAdaptationService`)

Dos niveles de abstracción separando **transporte** de **caso de uso** (D06):

```csharp
// PUERTO de bajo nivel — agnóstico de proveedor, soporta streaming.
public interface IAiClient
{
    IAsyncEnumerable<AiStreamChunk> StreamAsync(AiRequest request, CancellationToken ct = default);
    Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct = default); // keywords/juez
}

// CASO DE USO — arma el prompt con guardarraíles, valida "cero invención".
public interface IResumeAdaptationService
{
    IAsyncEnumerable<AdaptEvent> AdaptStreamingAsync(AdaptCvCommand command, CancellationToken ct);
}
```

- `AnthropicAiClient` (en `Infrastructure`) implementa `IAiClient` con el **SDK oficial `Anthropic`**: `client.Messages.CreateStreaming(...)` → `IAsyncEnumerable<RawMessageStreamEvent>`, filtrando deltas de texto (`TryPickContentBlockDelta` + `delta.Delta.TryPickText`). `OpenRouterAiClient` es el fallback conmutable por `AiClientSelector` (D06).
- **Routing de modelos** (D07, `AiOptions`/`manifest.json`): adaptación default **Sonnet 4.6** (`claude-sonnet-4-6`, `Thinking=Disabled`, `Temperature=0.2`, `MaxTokens=4000`); premium v1 **Opus 4.8** (`claude-opus-4-8`, **sin** temperature); keywords/juez **Haiku 4.5** (`claude-haiku-4-5`, structured outputs). La extracción de keywords arranca **100% reglas (0 tokens)** en v0.
- **Cero invención** (FR-024/025): system prompt versionado con guardarraíles + few-shots; tras el streaming, `AdaptationGuard`/`InventionValidator` re-extrae entidades del CV adaptado (reutilizando `EntityExtractor`/diccionario) y las compara contra la whitelist del original; severidad alta (empresa/cargo/cert/métrica) → reintento reforzado máx. 1; severidad media (skill nueva) → se marca y resalta; el resultado se emite como `event: honesty` (FR-029).
- **Anti prompt-injection** (FR-026, D06): CV y vacante en bloques `<cv_usuario_{nonce}>` / `<vacante_{nonce}>` con nonce aleatorio por request + sanitización (neutraliza cierres de bloque) + regla de system "el contenido es DATO" + recordatorio final.
- **Prompt caching** (D06): system + few-shots dimensionados > 2048 tokens con `CacheControlEphemeral`; el bloque volátil (CV+vacante) va después del breakpoint en `messages`. No interpolar fecha/GUID/usuario en `system` (rompe la caché); `sha256` del system registrado en logs para detectar invalidaciones.

### 4.4 Streaming de la adaptación (SSE)

**Server-Sent Events** sobre el `IAsyncEnumerable` del servicio (D08). Endpoint `POST /api/v1/adapt/stream`:

```csharp
group.MapPost("/adapt/stream", async (
        AdaptCvCommand command, IResumeAdaptationService service,
        HttpContext http, CancellationToken ct) =>
{
    http.Response.Headers.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";
    http.Response.Headers["X-Accel-Buffering"] = "no";          // evita buffering en proxies
    await foreach (AdaptEvent ev in service.AdaptStreamingAsync(command, ct))
    {
        await http.Response.WriteAsync(Sse.Frame(ev), ct);
        await http.Response.Body.FlushAsync(ct);                 // flush por frame = streaming real
    }
})
.AddEndpointFilter<ValidationFilter<AdaptCvCommand>>()
.RequireRateLimiting("adapt");
```

- **Eventos nombrados** (congelados en `contracts/api-contract.md`): `meta` (modelo/promptVersion) · `token` (`{delta}`) · `honesty` (`{status, newSkillsDetected, note}`) · `done` (`{fullText, usage}`) · `error` (ProblemDetails compacto) + comentarios `:` heartbeat ~15s.
- **Cancelación** (FR-028, NFR-011): `ct = HttpContext.RequestAborted` corta el `await foreach` y el streaming del SDK → detiene el gasto de tokens.
- **Recolección final:** el servicio acumula un buffer; `event: done` lleva el `fullText` canónico para validar/exportar (el cliente no depende solo de los deltas).
- **Errores a mitad de stream:** se emiten como `event: error` y se cierra (no se reintenta para no duplicar contenido); el retry de Polly cubre solo el handshake/headers antes del primer token (D08/D09).
- **Cliente (D14):** el frontend consume el SSE con `fetch` + `ReadableStream` (no `EventSource`, que es GET-only) porque necesita `POST` con cuerpo; el BFF (`app/api/adapt/route.ts`, runtime Node) hace **passthrough sin bufferizar**.

### 4.5 Parseo de CV (v1, M3)

v0 procesa **solo texto pegado** (sin capa de parseo — privacidad + build más rápido, D11). v1 introduce subida de archivos tras el puerto `ICvParser`: **PdfPig** (`UglyToad.PdfPig`) para PDF y **OpenXML SDK** (`DocumentFormat.OpenXml`) para DOCX (FR-054). Solo con archivo la medibilidad de formato sube a `m_C4 = 1.0` → evaluación de formato completa que detecta columnas, tablas, imágenes y capas (FR-055). PDFs basados en imagen (OCR) quedan fuera de alcance; fallback honesto "pega el texto".

### 4.6 Export PDF (v0)

**QuestPDF** tras `IPdfExporter` (`QuestPdfExporter`, D12). Endpoint `POST /api/v1/export/pdf` → `application/pdf` (blob). Genera un PDF limpio y "ATS-friendly" (sin tablas/columnas que rompan el parseo), coherente con lo que el producto recomienda. Verificar la Community License al monetizar (riesgo abierto, research.md).

---

## 5. Transversales

### 5.1 Logging (Serilog)

`Serilog.AspNetCore`: consola legible en dev, **JSON compacto** en prod. `UseSerilogRequestLogging()` para una línea por request. **Privacidad (FR-041, NFR-002, D04):** prohibido loguear `cvText`/`jobText`/CV adaptado; solo metadatos (longitudes, conteo de keywords, `modelId`, `promptVersion`, `sha256`, tokens in/out/cache, `stopReason`, `findingsCount`, latencia, `traceId`, `request_id` del proveedor). Enriquecimiento con `TraceId`/`CorrelationId` para correlacionar SSE de larga duración. Métricas observadas: tasa de refusals, tasa de invenciones detectadas, costo/operación, cache hit rate (D06/D07).

### 5.2 Manejo de errores (ProblemDetails)

`AddProblemDetails()` → todos los errores siguen **RFC 7807/9457** (`application/problem+json`). `IExceptionHandler` global (`GlobalExceptionHandler`) mapea sin filtrar detalles internos, con `traceId` (D04):

| Condición | HTTP | Notas |
|---|---|---|
| Validación FluentValidation | `400` | `ValidationProblemDetails` con `errors` |
| Payload excede tope | `413` | rechazo **antes** de costo de IA (FR-037) |
| `BrokenCircuitException` (IA caída) | `503` | con `Retry-After` (D09) |
| `TimeoutRejectedException` (IA lenta) | `504` | timeout por intento 60s |
| Rate limit | `429` | lo emite el middleware + `Retry-After` (D10) |
| `OperationCanceledException` | `499` | cancelación del cliente |
| No controlada | `500` | mensaje genérico + `traceId` |

El BFF de Next.js normaliza estos ProblemDetails y los entrega al cliente, que los mapea a mensajes humanos (D13). **Degradación elegante (FR-030, NFR-018, US-016):** si la IA falla, el análisis determinista (puntaje + keywords + recomendaciones) sigue disponible y la UI no se rompe.

### 5.3 Seguridad

- **Anti prompt-injection (FR-026, NFR-005):** ver §4.3 — bloques con nonce + sanitización + regla de system + recordatorio final; la entrada del usuario nunca se interpreta como instrucción.
- **CORS:** política nombrada que permite el origen del frontend (Vercel) y `localhost` en dev (orígenes desde configuración). El **BFF same-origin** (D13) elimina el preflight problemático del SSE; el SSE real lo sirve el BFF, no el navegador contra .NET directo.
- **Rate limiting nativo (D10, FR-036/038):** middleware `RateLimiter` particionado **por IP** con políticas por costo: `score` (20/min, barato) y `adapt` (5/hora, caro), más un `GlobalLimiter` de concurrencia (50, sin cola). `429` con `Retry-After` + headers `X-RateLimit-*`. **`ForwardedHeadersOptions` (`X-Forwarded-For`)** configurado para obtener la IP real tras proxy (Render/Railway/Azure) — crítico para que el límite funcione. Defensa de borde adicional en el BFF/`middleware.ts`: honeypot, **Cloudflare Turnstile** invisible, debounce, tope de longitud (~20.000 chars) validado en cliente (FR-037/039).
- **Endurecimiento:** HTTPS redirection, HSTS en prod, `X-Content-Type-Options`, límites de tamaño de request body.
- **Webhooks Wompi (v1, M4, FR-048/049, D15):** firma de integridad **SHA256 generada server-side** (el *integrity secret* nunca toca el cliente). El **webhook verifica la firma** (`signature.properties` leído **dinámicamente** del payload + `timestamp` + *events secret*), valida **antes** de tocar la BD, responde 200 rápido y confirma contra `GET /v1/transactions/{id}`. Acreditación **idempotente** (clave por `transaction.id` + estado) — **nunca** por el `redirect-url` del navegador. Secretos/llaves independientes por ambiente (Sandbox/Producción).

### 5.4 Configuración y secretos (D16)

- **Options pattern** con validación al arranque (`AiOptions` con `ValidateDataAnnotations().ValidateOnStart()`).
- **Dev:** `dotnet user-secrets` para `ANTHROPIC_API_KEY` (nunca en `appsettings.json` ni en git).
- **Prod:** variables de entorno del hosting (Render/Railway/Azure App Service settings; Key Vault opcional en Azure v1). `BACKEND_URL` y secretos del BFF como env vars de Vercel.
- `.gitignore` cubre `*.user`, `appsettings.*.local.json`. Nada de secretos en el repositorio.

### 5.5 `CancellationToken` end-to-end (NFR-011)

Propagación obligatoria: endpoint (`HttpContext.RequestAborted`) → handler/servicio de `Application` → `IAiClient` → `CreateStreaming(...).WithCancellation(ct)` → (v1) EF Core (`SaveChangesAsync(ct)`). Cerrar la pestaña libera recursos y **detiene el gasto de tokens** de inmediato.

---

## 6. Estrategia de pruebas (TDD)

**Stack:** xUnit + FluentAssertions. **Política test-first para el motor de puntaje** (regla dura 7).

### 6.1 Unitarias del motor (`BuildCv.Domain.Tests`)

- **Golden cases** (`Scoring/GoldenCases/`): pares (CV, vacante) con `ScoreResult` esperado; cada caso documenta el "por qué" del puntaje → especificación viva y defensa de explicabilidad (FR-008).
- **Determinismo (FR-006):** `Evaluate` con la misma entrada ⇒ `ScoreResult` equivalente (el motor no toca reloj/red/aleatoriedad).
- **Matching inteligente (FR-015/016/017, D02):** "desarrollé"≈"desarrollo", "Postgres"≈"PostgreSQL", "JS"≈"JavaScript"; "año"≠"ano" y conservación de la Ñ; protección de `c#`/`.net`/`ci/cd`; **blocklist** "Java"⇎"JavaScript". `[Theory]`/`[InlineData]` para sinónimos y confundibles.
- **Compuertas y renormalización (FR-011/012):** sin contacto/sin experiencia/keyword stuffing aplican caps; `m_C4=0.5` en v0 renormaliza correctamente.

### 6.2 Contract tests (motor↔frontend, SSE, ProblemDetails)

- Los DTO de respuesta (`ScoreResponse`, `AdaptEvent` ⏳, `ProblemDetails`) se validan contra `contracts/api-contract.md`: campos (`overallScore`, `band`, `honestyNotice`, `engineVersion`, `lexiconVersion`, `contextId`, `components[]`, `keywordAnalysis.{present,missing,partial}`, `recommendations[]`, `formatIssues[]`, `gatesApplied[]`), eventos SSE `meta/token/honesty/done/error` ⏳, headers `X-RateLimit-*` + `Retry-After`, errores `application/problem+json`. El documento OpenAPI generado por .NET 10 es la fuente del cliente TS y debe permanecer sincronizado.
- **Frontend ⏳ (repo `BuildCv-web`):** tests del `analyzer-reducer` (transiciones válidas idle→…→done, imposible quedar inconsistente) y del parser `lib/api/sse` (framing `\n\n`, líneas `event:`/`data:`, comentarios `:`), y del mapeo de ProblemDetails (D13/D14).

### 6.3 Casos de uso (`BuildCv.Application.Tests`)

- En **M0–M1**: los 5 tests cubren el `ScoreCvHandler` y `ScoreCvValidator` (entrada, normalización, validación de longitudes, identidad cvText == jobText, scoring end-to-end con el motor real). El handler no depende de IO externo.
- **M1-IA ⏳:** `FakeAiClient : IAiClient` emite un `IAsyncEnumerable` fijo → prueba `ResumeAdaptationService` y, sobre todo, `InventionValidator`/`AdaptationGuard` (un CV adaptado con una skill inventada **debe** detectarse) **sin gastar tokens ni red**.

### 6.4 Integración (`BuildCv.Api.IntegrationTests`)

- **`WebApplicationFactory<Program>`** levanta el host real en memoria; `ConfigureTestServices` sustituye `IAiClient` por el fake (sin red ni tokens en CI).
- Pruebas del **endpoint SSE** (consumir el stream y verificar `data: …\n\n`, `event: done`), `400` (validación), `429` (rate limit), `503`/`504` (resiliencia simulando fallos del fake).
- **Testcontainers (`Testcontainers.PostgreSql`) → v1 (M3):** cuando exista `Persistence`, los tests levantan un **PostgreSQL efímero en Docker**, aplican migraciones y prueban repositorios/EF Core contra una DB real (no in-memory). El runner de GitHub trae Docker → corre en CI sin pasos extra.

---

## 7. Despliegue y CI

### 7.1 Hosting (D16)

| Fase | API .NET | Frontend | DB |
|---|---|---|---|
| v0 (lanzar barato) | Render o Railway (Docker) | Vercel | — |
| v1 / "para el CV" | **Azure App Service (B1)** + CD GitHub Actions | Vercel (runtime Node para SSE) | PostgreSQL gestionado |

**Dockerizar desde el día 1** (imagen portable, un solo servicio) → promover Render→Azure sin cambios. El SSE de larga duración debe sobrevivir a límites de la plataforma (verificar buffering/timeout en cada hosting; `X-Accel-Buffering: no` + `no-transform`).

### 7.2 CI (GitHub Actions, `.github/workflows/ci.yml`)

```yaml
name: ci
on: [push, pull_request]
jobs:
  build-test:
    runs-on: ubuntu-latest          # incluye Docker → Testcontainers funciona en v1
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet restore BuildCv.slnx
      - run: dotnet build BuildCv.slnx --no-restore -c Release
      - run: dotnet format BuildCv.slnx --verify-no-changes   # estilo (.editorconfig)
      - run: dotnet test BuildCv.slnx --no-build -c Release --collect:"XPlat Code Coverage"
```

- Job separado para el frontend (`npm ci` + `npm run lint` + `npm test` + `next build`).
- **CD opcional** a Azure App Service (`azure/webapps-deploy`) tras pasar tests en `main`. Frontend con preview deployments automáticos de Vercel por PR.

---

## 8. Fases de implementación (hitos M0–M4)

> Mapeo de los hitos del producto a fases ejecutables, alineado con `PLANEACION.md` (Fase 0–4) y las decisiones `research.md`. **v0 = M0–M2** (P0, lanzable, gratis, sin cuentas/DB/pagos) · **v1 = M3–M4** (P1, comercial).

### M0 — Cimientos / scaffolding *(Fase 0)*
**Objetivo:** esqueleto end-to-end desplegado ("hola mundo" de punta a punta).
- Solución .NET (4 proyectos + 3 de test), `Directory.Build.props`, `.editorconfig`, Dockerfile (D03).
- `Program.cs`: DI por capa, Serilog, ProblemDetails + `IExceptionHandler`, OpenAPI/Scalar, versionado `/api/v1`, ForwardedHeaders, HealthChecks (`/health/live`, `/health/ready`) (D04).
- Scaffold Next.js 16 + Tailwind v4 + diseño custom + `lib/copy/es.ts` + BFF Route Handlers vacíos (D13).
- CI GitHub Actions (build + format + test); deploy de prueba Render/Railway + Vercel (D16).
- **Entrega:** infra; sin FRs de producto aún.

### M1 — Motor determinista (núcleo, test-first) *(Fase 1a)*
**Objetivo:** el gancho gratis que cuesta $0 en tokens.
- **TDD** `SpanishTextNormalizer` + `SpanishLemmatizer` + `SkillSynonymDictionary` + `ConfusableBlocklist` (D02).
- `SkillGazetteer` (YAML embebido, versionado) + `EntityExtractor` + `KeywordExtractor` determinista (D01).
- `ScoringEngine` puro con C1–C5, cascada de match, crédito parcial, factor de ubicación, compuertas, renormalización; golden cases (D01).
- Endpoint `POST /api/v1/score` (política rate limit `score`) + `ScoreCvValidator`.
- **FRs:** FR-005..FR-019, FR-021, FR-022 · **NFR:** NFR-009, NFR-017, NFR-021 · **US:** US-001, US-002, US-003 (parcial backend).

### M2 — IA + experiencia completa → **v0 LANZABLE** *(Fase 1b)*
**Objetivo:** pegar CV+vacante → puntaje → adaptar (streaming) → mejora → exportar; sin cuentas; en móvil.
- `IAiClient` + `AnthropicAiClient` (SDK oficial, streaming) + `AiClientSelector` (+ fallback OpenRouter conmutable) (D06/D07).
- `PromptCatalog`/`manifest.json` + `adapt_cv.system.v1` (> 2048 tokens, anti-inyección, few-shots) (D06).
- `ResumeAdaptationService` (prompt + nonce + buffer) + `AdaptationGuard`/`InventionValidator` + reintento reforzado (D06).
- Endpoint SSE `POST /api/v1/adapt/stream` (política `adapt`, eventos `meta/token/honesty/done/error`, cancelación) (D08).
- Resiliencia Polly v8 (retry→timeout→breaker) + mapeo 503/504 (D09); rate limiting nativo + Turnstile en BFF (D10).
- `QuestPdfExporter` + `POST /api/v1/export/pdf` (D12).
- Frontend completo: máquina de estados `useReducer`, `useAdaptStream` (fetch+ReadableStream), `ScoreGauge`/`ComponentBar`/`KeywordChips`/`FixList`, `honesty-notice`, `improvement-delta`, `action-bar`, `share-improvement` (sin PII), demo data .NET, móvil + a11y (D13/D14).
- Gate **ZDR** resuelto/archivado **antes** del copy de privacidad; consentimiento de transferencia internacional en UI (D19/D17).
- **FRs:** FR-001..FR-004, FR-020(opt), FR-023..FR-043 · **NFR:** NFR-001..003, 005, 006, 010..016, 018..022, 025 · **US:** US-004..US-011, US-016. **→ v0 ships.**

### M3 — v1: cuentas, historial, archivos, base legal *(Fase 3a)*
**Objetivo:** persistencia y cumplimiento Habeas Data.
- `Infrastructure/Persistence` (EF Core + PostgreSQL, migraciones) tras `ICvRepository`/repos; Testcontainers en tests (§6.4).
- Auth: ASP.NET Core Identity + JWT (FR-044); historial consultable (FR-045).
- `ICvParser` + `PdfPigCvParser`/`OpenXmlCvParser`; subida de archivos + evaluación de formato completa `m_C4=1.0` (FR-054/055, D11).
- Consentimiento informado previo + transferencia internacional, política de tratamiento, aviso de privacidad, derechos ARCO + revocación (FR-051/052/053; NFR-004/023/024, D17).
- **FRs:** FR-044, FR-045, FR-051..FR-055 · **US:** US-012, US-014, US-015.

### M4 — v1: monetización *(Fase 3b)*
**Objetivo:** cobrar por adaptación con medios locales, de forma idempotente y conforme.
- Sistema de **créditos** (1 crédito = 1 adaptación) + `MovimientoDeCredito` auditable (FR-046).
- `IPaymentProvider` + `WompiProvider`: Web Checkout, firma SHA256 server-side, **webhook firmado idempotente** (`signature.properties` dinámico), confirmación server-to-server (FR-047/048/049, D15).
- Flujo tributario: RUT + Régimen SIMPLE + factura electrónica DIAN antes de cobrar (FR-050, D18).
- **FRs:** FR-046..FR-050 · **NFR:** NFR-007, NFR-008 · **US:** US-013 · **Métrica:** M-07.
- *(Post-M4 — Fase 4 LATAM, fuera de este MVP: `MercadoPagoProvider`, multimoneda, ruta `[locale]`/`next-intl`, plantillas, carta de presentación.)*

---

**Referencias:** `spec.md` (FR/US/NFR canónicos) · `research.md` (decisiones D01–D20 con alternativas y riesgos) · `contracts/api-contract.md` (contrato a congelar) · `data-model.md` (modelo v1) · `tasks.md` (desglose por hito) · `.specify/memory/constitution.md` (reglas duras) · `PLANEACION.md` (estrategia base).
