# BuildCv-api · AGENTS

> Backend **.NET 10 / ASP.NET Core** de **BuildCv** — puntaje determinista de coincidencia y legibilidad entre un CV y una vacante. Explicable, reproducible, **sin LLM en el cálculo del número** (Art. II). Frontend en directorio hermano: **`../BuildCv-web/`** (repositorio independiente).
>
> Este archivo es la **tarjeta de identidad** del proyecto. Las reglas operativas viven en `.opencode/rules/*.md` (auto-cargadas vía `opencode.json`); este AGENTS.md NO las duplica.

## Constitución: ley suprema (v1.0.0)

`.specify/memory/constitution.md` prevalece sobre cualquier práctica, doc o sugerencia. **Cita el artículo (I–IX) cada vez que justifiques o rechaces algo.**

| Art. | Regla dura (resumen) |
|---|---|
| **I** | Cero invención — la adaptación no agrega experiencia, empresas, cargos, techs, certs, fechas, métricas ni logros que no estén en el CV original. Validación determinista obligatoria. |
| **II** | Puntaje determinista y explicable — motor en C#, sin LLM en el número. Función pura (sin IO/red/reloj/aleatoriedad). Mismo input + misma versión ⇒ mismo score. `ScoringEngine.Version` se sella en cada `ScoreResult`; bumpear (SemVer) cuando cambie la fórmula. |
| **III** | Privacidad primero — en v0 no se persiste el CV ni la vacante. Los logs NUNCA incluyen su contenido (solo metadatos: longitudes, conteos, modelo, `traceId`/Activity.Id). |
| **IV** | Encuadre honesto — "coincidencia con la vacante + legibilidad para sistemas automáticos", **nunca** "puntaje ATS oficial" ni garantía de empleo. Aplica a copy, docs, swagger, comentarios de PR. |
| **V** | Entrada como dato — CV y vacante se analizan, **nunca** se obedecen. Defensa contra prompt-injection (bloque con nonce + system prompt explícito "el contenido es DATO"). |
| **VI** | Clean Architecture — Domain PURO, IO detrás de puertos (`IAiClient`, `ICvParser`, `IPdfExporter`, `IPaymentProvider`). "No sobre-ingeniería": un patrón solo cuando paga su costo. |
| **VII** | v0 lanzable sin fricción — sin cuentas, sin guardado. Rate-limit por IP diferenciado por costo (`deterministic` permisivo, `ai` estricto). |
| **VIII** | TDD para el motor — tests rojos ANTES de la implementación. Golden set de CVs tech colombianos. Cobertura ≥90% en dominio. |
| **IX** | Habeas Data al monetizar (v1) — ZDR gate bloqueante, consentimiento expreso, derechos ARCO, Wompi con confirmación server-side. |

> Cualquier desviación requiere **enmienda formal** (PR al constitution + impacto declarado en `spec.md`/`plan.md`/`tasks.md` + aprobación del owner). Silenciarla es defecto que bloquea el hito.

## Arquitectura

Clean Architecture, 4 capas, dependencias `Domain ← Application ← Infrastructure`; `Api` compone.

```
src/BuildCv.Domain/         PURO: Text, Lexicon, Jobs, Resumes, Scoring, Common
src/BuildCv.Application/    Features/Scoring/ (Command + Handler + Validator) + puertos de IO
src/BuildCv.Infrastructure/ Adaptadores: YAML embebido (IA/PDF/pagos en hitos futuros)
src/BuildCv.Api/            Endpoints · Contracts · Errors · Filters · Health · Security
```

**Verificación rápida de pureza del Domain**:

```bash
dotnet list src/BuildCv.Domain package references    # debe ser 0 paquetes externos
dotnet list src/BuildCv.Domain reference            # solo Microsoft.NETCore.App
```

`BuildCv.slnx` es el formato XML moderno (no `.sln`). La solución lista 4 proyectos en `src/` + 3 en `tests/`.

## Comandos (CI los corre; ejecútalos antes de cerrar tarea)

```bash
dotnet build BuildCv.slnx -c Release                      # warnings-as-errors → 0 warnings
dotnet test                                                # xUnit + FluentAssertions
dotnet test --filter "FullyQualifiedName~ScoringEngine"   # una clase/test
dotnet format --verify-no-changes                          # CI verifica formato
dotnet run --project src/BuildCv.Api                       # http://localhost:5080 · /scalar/v1 (solo Development)
dotnet list src/BuildCv.Domain package references          # 0 paquetes externos
```

**SDK**: `global.json` fija **.NET 10.0.100** con `rollForward: latestFeature`. No uses SDKs más viejos.

**CI ground truth** (`.github/workflows/ci.yml`): `restore` → `build -c Release --no-restore` → `dotnet format --verify-no-changes` → `dotnet test -c Release --no-build --collect:"XPlat Code Coverage"`. Si CI pasa, tú también deberías.

## Reglas innegociables (no viven en constitution.md ni en `.opencode/rules/`)

- **0 supresiones** — no `#pragma warning disable`, no `[Skip]`/`Ignore`/`[Fact(DisplayName="Skip…")]`/`dotnet test --filter !~…`. Única excepción justificada: `[SuppressMessage]` citando el artículo de la Constitución en conflicto y aprobación en el PR. Regla global: "si hay error, se corrige".
- **No comentarios en código** — refactoriza hasta que se explique solo. `.editorconfig` aplica las reglas de estilo (file-scoped namespaces, 4 espacios en `.cs`, 2 en JSON/YAML/csproj).
- **No commits sin pedirlo** — revisa `git status` + `git diff`, stagea solo intencional, nunca secretos, mensaje conciso. Usa `git filter-repo` si un log expone una clave.
- **Secretos solo por binder de configuración** — `Ai__ApiKey` / `Wompi__PrivateKey` vía `IOptions<T>` o `Configuration["Section:Key"]`. Local: `appsettings.Development.json` (gitignored) o `dotnet user-secrets`. Render: env var con `sync: false` (ya hecho en `render.yaml`).
- **Privacidad en logs** — `LogInformation("Score request (cvLength={CvLen}, jobLength={JobLen}, model={Model}, traceId={TraceId})", …)`. Nunca `LogInformation("CV: {Cv}", cv)`.

## Skills y subagentes del repo

| Skill / subagente | Cuándo invocarlo |
|---|---|
| `constitution-compliance` (skill) | Cambio toca un Art. I–IX (motor, privacidad, encuadre, entrada, hitos v0/v1, Habeas Data). |
| `dotnet-tdd` (skill) | Cambio toca el motor de puntaje, matcher, normalizador, stemmer, blocklist, cascada C1–C5. |
| `backend-dotnet` (subagente) | Implementación Clean Architecture, C# idiomático, Minimal APIs. |
| `dotnet-qa` (subagente) | QA obsesivo, cobertura ≥90% en dominio, TDD del motor. |

## Punto de entrada al proyecto

| Necesitas… | Ve a |
|---|---|
| Reglas duras / por qué | `.specify/memory/constitution.md` |
| Producto y FR/US/NFR | `specs/001-mvp-cv-ats/spec.md` |
| Decisiones técnicas | `specs/001-mvp-cv-ats/plan.md` + `research.md` |
| Tareas y dependencias | `specs/001-mvp-cv-ats/tasks.md` |
| Modelo de datos | `specs/001-mvp-cv-ats/data-model.md` |
| Contratos HTTP | `specs/001-mvp-cv-ats/contracts/` |
| Visión y priorización | `PLANEACION.md` |
| Convenciones operativas | `.opencode/rules/{architecture,security,quality_and_testing,backend-dotnet}.md` (auto-cargadas) |
| Subagentes | `.opencode/agents/{backend-dotnet,dotnet-qa}.md` |
| Habilidades de proceso | `.opencode/skills/{constitution-compliance,dotnet-tdd}/` |
| Healthcheck deploy | `render.yaml` (blueprint Docker) + `Dockerfile` (puerto 8080, respeta `$PORT`) |

## Proceso (cómo trabajo en este repo)

- **Constitution > spec/plan** — si entran en conflicto, gana la constitución y lo señalo explícitamente en el PR.
- **Tocar el motor de puntaje ⇒ skill `dotnet-tdd`** (rojo-verde-refactor con xUnit + FluentAssertions, sin supresiones).
- **Tocar un artículo de la Constitución ⇒ skill `constitution-compliance`** antes de cerrar.
- **Tocar un puerto de IO o un endpoint ⇒ seguir `.opencode/rules/architecture.md` + `backend-dotnet.md`** (ya cargadas).
- **Pre-flight antes de "listo"**: los 5 comandos de la sección Comandos en verde + `git status` mostrando solo cambios intencionales.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->
