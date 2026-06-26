# Feature 014 — Enmienda de la Constitution v1.1.0 → v1.2.0

> **Status:** [Spec] — Pending design · **Tipo:** Enmienda formal MENOR (governance, NO feature de código)
> **Owner approval required:** SÍ (proceso documentado en Constitution §Gobernanza → Proceso de enmienda)
> **Próxima:** [`sdd-design` → `design.md`](./design.md) · [`../000-INDEX.md`](../000-INDEX.md)

## Resumen

Esta NO es una feature de producto. Es un **proceso de gobernanza** documentado: la modificación formal de la Constitution v1.1.0 → v1.2.0 (ley suprema del proyecto) para:

1. **Ratificar formalmente** `next-auth@^4.24.7` como dependencia aprobada (Art. VI) — verbalmente aprobado por el owner durante el ciclo de SHIP de 013.2-web-jwt-cookie pero nunca formalizado en el texto constitucional.
2. **Documentar explícitamente** el límite v0/v1 introducido por 009-auth en:
   - **Art. III** (privacidad / persistencia): qué se persiste en v0 vs v1, y qué NUNCA se persiste (CV ni vacante).
   - **Art. VII** (entrega por hitos / auth): qué endpoints son anónimos vs autenticados, cómo se aplica el rate-limit por endpoint.
3. **Cross-referenciar** Art. IX (Habeas Data) a las implementaciones existentes (`IUserDataStore`, `CreditLedgerEntry`, `DeleteUserDataHandler.AnonymizeAsync`, `PrivacyPolicyQueryHandler`) para trazabilidad.

**Por qué se necesita:**

- **WARNING pre-existente** del verify de 013.2-web-jwt-cookie: "Art. VI no lista `next-auth` como dependencia aprobada; la enmienda se hizo verbalmente durante SHIP pero no quedó registrada en el texto constitucional". Esta formalización cierra ese WARNING.
- **WARNING pre-existente** del verify de 009-auth (desde 2026): "Art. III menciona persistencia local pero no explicita el límite v0/v1 para persistencia server-side (introducida en 009-auth)"; "Art. VII no explicita la frontera de auth entre v0 (anónimo) y v1 (autenticado)". Esta formalización cierra ambos WARNINGs.
- **Trazabilidad**: cross-referenciar Art. IX a las features que lo implementan para que cualquier auditor pueda verificar cumplimiento sin leer 4 specs separadas.

**Lo que cambia:**

| Art. | Cambio | Razón |
|---|---|---|
| **Header** | Versión 1.1.0 → 1.2.0; fecha de última enmienda 2026-06-09 → 2026-06-25; nota de enmienda añadida. | Bump semver MENOR documenta el cambio formal. |
| **Art. III** | **MODIFICADO** — se añade bloque `> **v0/v1 boundary (added v1.2.0):**` que documenta: v0 no persiste nada server-side; v1 (009-auth) puede persistir identidad (email, name, OAuth provider ID) y credit balance/ledger bajo controles Habeas Data; CV y job content NUNCA se persisten server-side; referencias a `IUserDataStore` y `CreditLedgerEntry`. | Resuelve WARNING pre-existente de 009-auth verify. v1 persistence es implícita en el código pero necesita documentación constitucional explícita. |
| **Art. VI** | **MODIFICADO** — se añade bloque `> **Approved external dependencies (added v1.2.0):**` que lista: `diff@^5`, `zod@^3` (shared utilities); `web-vitals@^4`, `react-error-boundary@^5`, `next-auth@^4.24.7` (ratificado 2026-06-25 por owner en 013.2-web-jwt-cookie). Regla: `next-auth@^4.x` es la ÚNICA librería web-side de auth aprobada; futuras dependencias de auth requieren enmienda explícita. | Resuelve WARNING pre-existente de 013.2-web-jwt-cookie verify. Formal ratification de la aprobación verbal del owner durante el SHIP. |
| **Art. VII** | **MODIFICADO** — se añade bloque `> **v0/v1 boundary (added v1.2.0):**` que lista los endpoints v0 (anónimos, rate-limited por IP) y v1 (autenticados via `RequireAuthorization()`, rate-limited por user+IP); declara que la frontera es per-endpoint vía middleware; nota de migración. | Resuelve WARNING pre-existente de 009-auth verify. v0/v1 split es implícito en el routing pero necesita documentación constitucional explícita. |
| **Art. IX** | **CROSS-REFERENCE** — texto constitucional sin cambio; se añade bloque `> **Implementation references (added v1.2.0):**` con paths a `IUserDataStore`, `CreditLedgerEntry`, `DeleteUserDataHandler.AnonymizeAsync`, `PrivacyPolicyQueryHandler`. | Trazabilidad: cualquier auditor puede verificar cumplimiento Habeas Data siguiendo las referencias. |
| **§Gobernanza** | **APPEND** — fila 1.2.0 añadida a la tabla "Historial de enmiendas" con fecha 2026-06-25, tipo MENOR, resumen y spec. | Mantiene el historial constitucional actualizado (regla de §Gobernanza paso 4). |

**Lo que NO cambia:**

- **Art. I** (cero invención): texto sin cambios; sigue aplicando tal cual.
- **Art. II** (puntaje determinista): texto sin cambios.
- **Art. IV** (encuadre honesto): texto sin cambios.
- **Art. V** (entrada como dato): texto sin cambios.
- **Art. VIII** (TDD): texto sin cambios.
- **Art. IX** (Habeas Data): reglas sin cambios; solo se añaden referencias de implementación.
- **§Gobernanza proceso**: el proceso de enmienda (propuesta → impacto → aprobación → registro) NO se modifica.
- **Código de producto**: CERO cambios de código. CERO migraciones. CERO tests nuevos.
- **APIs existentes**: sin cambios. Sin breaking changes.

## Proceso de enmienda (siguiendo §Gobernanza de la Constitution)

### 1. Propuesta

Se redacta este change (`specs/014-constitution-v1.2.0/proposal.md`) con:
- Texto nuevo para los 4 bloques añadidos (Art. III, VI, VII, IX cross-references).
- Bump de versión: 1.1.0 → 1.2.0 (cambio MENOR — clarifica ratified amendment + addresses WARNINGs).
- Justificación: formalizar aprobación verbal de 013.2 + resolver 2 WARNINGs de verificación.

### 2. Impacto declarado

| Artefacto afectado | Cambio requerido |
|---|---|
| `BuildCv-api/.specify/memory/constitution.md` | Bump versión 1.1.0 → 1.2.0, fecha 2026-06-09 → 2026-06-25, 4 bloques nuevos, historial actualizado. |
| `BuildCv-api/.specify/memory/CONSTITUTION-README.md` | Actualizar fila de tabla comparativa con la entrada v1.2.0. |
| `BuildCv-api/specs/000-INDEX.md` | Marcar 014 como `[Spec] Pending design` (este documento) y promover status de "PROPOSAL COMPLETE" a "SPEC COMPLETE". |
| `BuildCv-api/AGENTS.md` | Actualizar referencia a Constitution v1.2.0 (header + columna de Art. VI). |
| `BuildCv-web/AGENTS.md` | Actualizar referencia a Constitution v1.2.0 (header). |
| Código backend | **CERO** cambios. |
| Código frontend | **CERO** cambios. |
| Tests | **CERO** nuevos. 1454/1454 existentes deben seguir pasando (verificación de no-regresión). |
| `specs/002-score-engine/spec.md` | Sin cambios (sigue compatible). |
| `specs/009-auth/spec.md` | Sin cambios (es la fuente de la clarificación de Art. III/VII; la clarificación solo documenta lo que ya está implementado). |
| `specs/013-credit-consumption/spec.md` | Sin cambios (es la fuente de la clarificación de Art. III/IX). |
| `specs/013-credit-consumption-followups/013.2-web-jwt-cookie-design.md` | Sin cambios (es la fuente de la ratificación de Art. VI). |
| `specs/013-credit-consumption-followups/013.2-web-jwt-cookie-verify-report.md` | Sin cambios (es la fuente de los 2 WARNINGs que esta enmienda cierra). |

### 3. Aprobación

**Owner approval required.** Esta enmienda es un cambio MENOR pero formaliza una aprobación verbal y resuelve 2 WARNINGs de verificación, así que requiere sign-off explícito del owner del proyecto per §Gobernanza paso 3.

### 4. Registro

- Versión: 1.1.0 → **1.2.0**
- Fecha de última enmienda: 2026-06-09 → **2026-06-25**
- Fecha de ratificación original: 2026-06-06 (NO cambia, es histórica)
- PR al constitution con este spec como justificación
- Fila 1.2.0 añadida a §Gobernanza → Historial de enmiendas

## Changelog (per article)

### Header — Version bump

**Before** (v1.1.0):
```
# Constitución del Proyecto — BuildCv

> **Artefacto SDD:** `.specify/memory/constitution.md` — ley fundamental del proyecto al estilo Spec Kit.
> **Versión:** 1.1.0 · **Fecha de ratificación:** 2026-06-06 · **Última enmienda:** 2026-06-09
> **Estado:** Vigente (ratificada). Enmienda menor sobre v1.0.0 — ver §Gobernanza → Historial de enmiendas.
```

**After** (v1.2.0):
```
# Constitución del Proyecto — BuildCv

> **Artefacto SDD:** `.specify/memory/constitution.md` — ley fundamental del proyecto al estilo Spec Kit.
> **Versión:** 1.2.0 · **Fecha de ratificación:** 2026-06-06 · **Última enmienda:** 2026-06-25
> **Estado:** Vigente (ratificada). Enmienda menor sobre v1.1.0 (014-constitution-v1.2.0) — ver §Gobernanza → Historial de enmiendas.
```

**Rationale**: Bump semver MENOR documenta el cambio formal. La fecha de ratificación original NO cambia (regla §Gobernanza).

### Art. III — Privacidad primero (modify)

**Current text** (v1.1.0):
```
**III.** Privacidad primero y minimización de datos — en v0 no se persiste el CV ni la vacante. Los logs NUNCA incluyen su contenido (solo metadatos: longitudes, conteos, modelo, `traceId`/Activity.Id).
```

**Modified text** (v1.2.0):
```
**III.** Privacidad primero y minimización de datos — en v0 no se persiste el CV ni la vacante. Los logs NUNCA incluyen su contenido (solo metadatos: longitudes, conteos, modelo, `traceId`/Activity.Id).

> **v0/v1 boundary (added v1.2.0):**
> v0 procesa en memoria y NO persiste NADA server-side (no cuentas, no CV, no job content). v1 (introducido en 009-auth) PUEDE persistir identidad de usuario (email, name, OAuth provider ID) y balance/ledger de créditos bajo controles Habeas Data (Art. IX). **CV y job content NUNCA se persisten server-side, independientemente de la versión.** Ver `BuildCv.Application/Features/Auth/IUserDataStore` (009-auth) y `BuildCv.Domain/Credits/CreditLedgerEntry` (013-credit-consumption) para la superficie de persistencia v1.
```

**Rationale**: Resuelve WARNING pre-existente del verify de 013.2-web-jwt-cookie. La persistencia v1 es implícita en el código (existe desde 009-auth) pero necesita documentación constitucional explícita para que auditores futuros entiendan QUÉ se persiste y QUÉ NUNCA.

### Art. VI — Clean Architecture (add exception)

**Current text** (v1.1.0):
```
**VI.** El backend demuestra .NET profesional (es portafolio) — Domain PURO, IO detrás de puertos (`IAiClient`, `ICvParser`, `IPdfGenerator`, `IPaymentProvider`, `ICvStore` en frontend). "No sobre-ingeniería": un patrón solo cuando paga su costo.
```

**Modified text** (v1.2.0):
```
**VI.** El backend demuestra .NET profesional (es portafolio) — Domain PURO, IO detrás de puertos (`IAiClient`, `ICvParser`, `IPdfGenerator`, `IPaymentProvider`, `ICvStore` en frontend). "No sobre-ingeniería": un patrón solo cuando paga su costo.

> **Approved external dependencies (added v1.2.0):**
> - **Backend** (shared utilities): `diff@^5`, `zod@^3`
> - **Frontend** (ratified 2026-06-25 by owner in 013.2-web-jwt-cookie, see `BuildCv-api/specs/013-credit-consumption-followups/013.2-web-jwt-cookie-design.md` §Art. VI Amendment): `web-vitals@^4`, `react-error-boundary@^5`, `next-auth@^4.24.7`
> - **`next-auth@^4.x` es la ÚNICA librería web-side de auth aprobada.** Futuras dependencias relacionadas con auth (sessions, OAuth, JWT en cliente, etc.) requieren enmienda constitucional explícita.
```

**Rationale**: Formal ratification de la aprobación verbal del owner durante el SHIP de 013.2-web-jwt-cookie. Cierra el WARNING pre-existente "¿está `next-auth` oficialmente aprobado?" y previene que se agreguen otras librerías de auth sin enmienda.

### Art. VII — v0 lanzable sin fricción (modify)

**Current text** (v1.1.0):
```
**VII.** v0 lanzable sin fricción — sin cuentas, sin guardado. Rate-limit por IP diferenciado por costo: `score` (60/min), `ai` (5/h), `export` (20/h), `import` (30/h).
```

**Modified text** (v1.2.0):
```
**VII.** v0 lanzable sin fricción — sin cuentas, sin guardado. Rate-limit por IP diferenciado por costo: `score` (60/min), `ai` (5/h), `export` (20/h), `import` (30/h).

> **v0/v1 boundary (added v1.2.0):**
> - **v0 endpoints** (anónimos, sin auth requerida): `/api/v1/score`, `/api/v1/adapt`, `/api/v1/export`, `/api/v1/import`, `/api/v1/health/*`. Rate-limited por IP.
> - **v1 endpoints** (introducidos en 009-auth, requieren auth): `/api/v1/auth/*`, `/api/v1/user/*`, `/api/v1/payments/*`, `/api/v1/credits/*`. Rate-limited por usuario autenticado + IP fallback.
> - La frontera es **per-endpoint**, declarada vía middleware `RequireAuthorization()`. Ambas políticas de rate-limit (IP y user) aplican acumulativamente según el rol del endpoint.
> - **Migration note**: endpoints v0 pueden migrar a v1 en versiones futuras; la migración requiere una enmienda separada.
```

**Rationale**: Resuelve WARNING pre-existente del verify de 009-auth. El split v0/v1 es implícito en el routing (`RequireAuthorization()` middleware) pero necesita documentación constitucional explícita para que la política de rate-limit quede clara por endpoint.

### Art. IX — Habeas Data (cross-reference, no text change)

**Current text** (v1.1.0): sin cambios.

**Add note** (v1.2.0):
```
**IX.** Cumplimiento Habeas Data al monetizar (v1) — ZDR gate bloqueante, consentimiento expreso, derechos ARCO, Wompi con confirmación server-side.

> **Implementation references (added v1.2.0):**
> - User identity persistence: `BuildCv.Application/Features/Auth/IUserDataStore` (009-auth)
> - Credit ledger: `BuildCv.Domain/Credits/CreditLedgerEntry` (013-credit-consumption)
> - ARCO anonymize pattern: `BuildCv.Application/Features/Auth/DeleteUserDataHandler.AnonymizeAsync` (013-credit-consumption)
> - Privacy policy v2: `BuildCv.Application/Features/Consent/PrivacyPolicyQueryHandler` (013-credit-consumption fix-verify-blockers)
```

**Rationale**: El texto del Art. IX no cambia; solo se añaden cross-references a las features que lo implementan para trazabilidad. Un auditor puede verificar cumplimiento Habeas Data siguiendo las 4 referencias en lugar de leer 4 specs separadas.

### §Gobernanza — Historial de enmiendas (append)

**Append** (v1.2.0):
```
| **1.2.0** | 2026-06-25 | MENOR | (a) Art. III documenta v0/v1 boundary de persistencia (v0 nada; v1 identidad + ledger; CV/job nunca). (b) Art. VI ratifica `next-auth@^4.24.7` como ÚNICA librería web-side de auth aprobada. (c) Art. VII documenta v0/v1 auth boundary per-endpoint (v0 anónimos por IP; v1 autenticados por user+IP). (d) Art. IX cross-references a implementaciones (`IUserDataStore`, `CreditLedgerEntry`, `DeleteUserDataHandler.AnonymizeAsync`, `PrivacyPolicyQueryHandler`). Cierra 2 WARNINGs pre-existentes de 009-auth y 013.2-web-jwt-cookie verifies. Sin cambio MAYOR ni eliminación de principios. | `specs/014-constitution-v1.2.0/` |
```

**Rationale**: Mantiene el historial constitucional actualizado per §Gobernanza paso 4 (Registro).

## Diff summary

| Section | Lines added | Lines modified | Lines deleted |
|---------|-------------|----------------|---------------|
| Header | 1 | 2 | 0 |
| Art. III | 3 | 0 | 0 |
| Art. VI | 5 | 0 | 0 |
| Art. VII | 6 | 0 | 0 |
| Art. IX | 5 | 0 | 0 |
| §Gobernanza | 1 (fila tabla) | 0 | 0 |
| **TOTAL** | **~21** | **~2** | **0** |

(Nota: el conteo exacto se confirma en `design.md` con el diff markdown literal.)

## Acceptance criteria

- [ ] Header actualizado: versión 1.1.0 → 1.2.0, fecha de última enmienda 2026-06-09 → 2026-06-25
- [ ] Art. III: bloque `> **v0/v1 boundary (added v1.2.0):**` añadido con referencias a `IUserDataStore` y `CreditLedgerEntry`
- [ ] Art. VI: bloque `> **Approved external dependencies (added v1.2.0):**` añadido con `next-auth@^4.24.7` ratificado y regla "única librería web-side de auth aprobada"
- [ ] Art. VII: bloque `> **v0/v1 boundary (added v1.2.0):**` añadido con listas de endpoints v0/v1, rate-limit policies, y migration note
- [ ] Art. IX: bloque `> **Implementation references (added v1.2.0):**` añadido con 4 referencias de implementación (sin cambiar el texto del artículo)
- [ ] §Gobernanza: fila 1.2.0 añadida a la tabla "Historial de enmiendas"
- [ ] `BuildCv-api/.specify/memory/CONSTITUTION-README.md` actualizado con entrada v1.2.0
- [ ] `BuildCv-api/AGENTS.md` actualizado para referenciar v1.2.0 (header + columna Art. VI)
- [ ] `BuildCv-web/AGENTS.md` actualizado para referenciar v1.2.0 (header)
- [ ] `BuildCv-api/specs/000-INDEX.md` actualizado: 014 promovido de "PROPOSAL COMPLETE" a "SPEC COMPLETE"
- [ ] `preflight.sh` (o equivalente) corre verde: 1454/1454 tests pasan, 0 warnings, formato OK
- [ ] `git diff` muestra SOLO los archivos intencionales (constitution.md, CONSTITUTION-README.md, 2 AGENTS.md, 000-INDEX.md)
- [ ] sdd-verify confirma: 0 conflictos semánticos con artículos existentes, 0 breaking changes

## Out of scope

- No nuevos artículos (Art. X, XI, etc.) — esta es una enmienda de clarificación, no de expansión.
- No breaking changes a artículos existentes — todo es ADDED (texto nuevo debajo del existente).
- No cambios al proceso de §Gobernanza mismo — solo se aplica el proceso existente.
- No nuevas reglas de governance (e.g., approval workflows, voting, multi-owner) — fuera de alcance.
- No refactor de código — esta es una enmienda governance-only.
- No nuevos tests — esta enmienda no cambia comportamiento observable.

## Compliance check

### Art. I–IX cumplimiento post-enmienda

- **Art. I** (Cero invención): N/A — esta enmienda no toca contenido.
- **Art. II** (Puntaje determinista): N/A — esta enmienda no toca el motor.
- **Art. III** (Privacidad primero): ✅ v0/v1 boundary ahora explícito. **MEJORA**.
- **Art. IV** (Encuadre honesto): N/A — esta enmienda no toca copy público.
- **Art. V** (Entrada como dato): N/A — esta enmienda no toca input handling.
- **Art. VI** (Clean Architecture): ✅ `next-auth` ratificado como única auth lib aprobada. **MEJORA**.
- **Art. VII** (Rate limits): ✅ v0/v1 auth boundary ahora explícito. **MEJORA**.
- **Art. VIII** (TDD): N/A — esta enmienda no agrega código ni tests.
- **Art. IX** (Habeas Data): ✅ trazabilidad vía implementation references. **MEJORA**.

### §Gobernanza proceso cumplimiento

- [x] **Paso 1 — Propuesta**: `specs/014-constitution-v1.2.0/proposal.md` escrito (103 líneas).
- [x] **Paso 2 — Impacto declarado**: tabla en sección "Proceso de enmienda → 2. Impacto declarado" lista todos los artefactos afectados (incluyendo CERO cambios de código).
- [ ] **Paso 3 — Aprobación**: pendiente owner sign-off (post-spec).
- [ ] **Paso 4 — Registro**: bump de versión + fila en historial se aplican durante `sdd-apply`.

## Affected features

| Feature | Status | Affected |
|---------|--------|----------|
| 001-mvp-cv-ats | 🗄️ ARCHIVED | No change |
| 002-score-engine | ✅ SHIPPED | No change |
| 003-adapt-ia | ✅ SHIPPED | No change |
| 004-export-pdf | ✅ SHIPPED | No change |
| 005-cv-pdf-docx-import | ✅ SHIPPED | No change |
| 006-cv-editor (frontend) | ✅ SHIPPED | No change |
| 007-constitution-v1.1.0 | ✅ RATIFICADA | No change (precedente de esta enmienda) |
| 008-observability | ✅ SHIPPED | No change |
| 009-auth | ✅ SHIPPED | Sin código; la clarificación de Art. III/VII documenta lo que ya está implementado |
| 010-persistence | ✅ SHIPPED | No change |
| 011-factus | ✅ SHIPPED | No change |
| 012-wompi | ✅ SHIPPED | No change |
| 013-credit-consumption | ✅ SHIPPED + ARCHIVED | Sin código; cross-reference de Art. III/IX a `CreditLedgerEntry` y `AnonymizeAsync` |
| 013.1-arco-legal-review | 📋 PLANEADO | No change (independiente) |
| 013.2-web-jwt-cookie | ✅ SHIPPED + ARCHIVED | Sin código; cross-reference de Art. VI a la ratification verbal que se formaliza aquí |
| 013.3-refund-midstream-test | ✅ SHIPPED | No change |
| **014-constitution-v1.2.0** | 📝 SPEC | **THIS DOCUMENT** |

## Próximos pasos después de la enmienda

Una vez merged y archivada (sdd-archive):

1. **009-auth + 013-credit-consumption + 013.2-web-jwt-cookie** siguen SHIPPED sin cambio de código; las clarificaciones constitucionales solo documentan lo implementado.
2. Cualquier nueva feature de auth (sessions, OAuth providers nuevos, JWT en cliente, etc.) requiere **enmienda constitucional explícita** per la nueva regla de Art. VI.
3. Cualquier nueva feature que persista datos server-side más allá de identidad + credit ledger requiere **enmienda constitucional explícita** per la nueva clarificación de Art. III.
4. El **constitution-check.sh** (cuando exista) debe extenderse para validar las nuevas clarificaciones: (a) verificar que `next-auth` es la única auth lib usada en `BuildCv-web/package.json`; (b) verificar que ningún endpoint v0 tiene `RequireAuthorization()`; (c) verificar que ningún endpoint v1 omite `RequireAuthorization()`.

## Referencias cruzadas

- **Proposal**: [`./proposal.md`](./proposal.md)
- **Constitution actual**: [`../../.specify/memory/constitution.md`](../../.specify/memory/constitution.md) (v1.1.0, ley suprema antes de esta enmienda)
- **Precedente de enmienda**: [`../007-constitution-v1.1.0/spec.md`](../007-constitution-v1.1.0/spec.md) + [`../007-constitution-v1.1.0/contracts/constitution-diff.md`](../007-constitution-v1.1.0/contracts/constitution-diff.md)
- **WARNING fuente 1**: [`../013-credit-consumption-followups/013.2-web-jwt-cookie-verify-report.md`](../013-credit-consumption-followups/013.2-web-jwt-cookie-verify-report.md) (Art. VI ratification)
- **WARNING fuente 2**: [`../009-auth/verify-report.md`](../009-auth/verify-report.md) (Art. III/VII boundary)
- **INDEX maestro**: [`../000-INDEX.md`](../000-INDEX.md)