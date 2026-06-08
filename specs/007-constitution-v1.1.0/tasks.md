# Tasks: 007-constitution-v1.1.0

> **Tipo:** Tasks de governance. NO hay código que escribir. Cada task es un paso del proceso de enmienda.

## Phase 0 — Pre-flight

- [ ] **T0.1** Confirmar que Constitution v1.0.0 está en `main` y sin cambios sin commitear.
- [ ] **T0.2** Confirmar que el owner del proyecto está disponible para aprobación.
- [ ] **T0.3** Confirmar que NO hay PRs abiertos que toquen la Constitución.

## Phase 1 — Aplicar el diff

- [ ] **T1.1** [DOCS] Modificar `.specify/memory/constitution.md`:
  - Header: bump versión 1.0.0 → 1.1.0, fecha 2026-06-06 → 2026-06-09
  - Art. III: reemplazar reglas con las 5 nuevas (ver [plan.md](./plan.md#cambios-concretos-al-texto-constitucional))
  - Art. I: añadir regla FR-029a
  - Art. VI: añadir `ICvStore` a la lista
  - Art. VII: añadir tabla de políticas
  - Art. IX: añadir cláusula de estado del gate ZDR
- [ ] **T1.2** [DOCS] Modificar `.specify/memory/CONSTITUTION-README.md`:
  - Añadir nota de v1.1.0
  - Actualizar tabla comparativa
  - Link al spec justificativo
- [ ] **T1.3** [VERIFY] Verificar manualmente que el diff coincide con el de [plan.md](./plan.md):
  ```bash
  git diff main -- .specify/memory/constitution.md
  ```

## Phase 2 — Tests

- [ ] **T2.1** [TEST] Actualizar `scripts/constitution-check.sh` para validar la nueva regla de persistencia local (FR-040a, FR-040b, NFR-001a). Verificar que `localStorage` no aparece en código backend.
- [ ] **T2.2** [TEST] Verificar que el script actualizado pasa: `./scripts/constitution-check.sh` → exit 0.
- [ ] **T2.3** [TEST] Verificar preflight: `./scripts/preflight.sh` → exit 0.

## Phase 3 — PR + aprobación

- [ ] **T3.1** [GIT] Commit con mensaje conventional: `docs(constitution): enmienda v1.0.0 → v1.1.0`.
- [ ] **T3.2** [GIT] Push a `origin/007-constitution-v1.1.0`.
- [ ] **T3.3** [PR] Abrir PR con título "docs(constitution): enmienda v1.0.0 → v1.1.0".
- [ ] **T3.4** [PR] Cuerpo del PR con link a `specs/007-constitution-v1.1.0/spec.md`.
- [ ] **T3.5** [PR] Asignar al owner como reviewer.
- [ ] **T3.6** [REVIEW] Esperar aprobación del owner. Si rechaza, Plan B = sessionStorage (no requiere enmienda).
- [ ] **T3.7** [GIT] Merge con squash a `main`.

## Phase 4 — Post-merge

- [ ] **T4.1** [GIT] Tag: `v1.1.0-constitution`.
- [ ] **T4.2** [DOCS] Actualizar `BuildCv-api/AGENTS.md` y `BuildCv-web/AGENTS.md`:
  - Constitución v1.0.0 → v1.1.0
  - Link al spec justificativo
- [ ] **T4.3** [DOCS] Actualizar `specs/000-INDEX.md` (ambos sub-proyectos):
  - Mover 007 a SHIPPED
  - 005/006 pasan de "BLOQUEADO por Art. III" a "PLAN READY (constitución v1.1.0)"
- [ ] **T4.4** [COMM] Anunciar en el canal del proyecto (cuando exista CHANGELOG).

## Phase 5 — Habilitar features siguientes

- [ ] **T5.1** Marcar 005-cv-pdf-docx-import como READY FOR IMPLEMENTATION.
- [ ] **T5.2** Marcar 006-cv-editor como READY FOR IMPLEMENTATION.
- [ ] **T5.3** Empezar implementación de 005 (siguiente fase).

## Critical Path

```
T0 → T1 (diff) → T2 (tests) → T3 (PR + approval) → T4 (post-merge) → T5 (habilitar 005/006)
```

## Out of Scope

- Implementación de 005-cv-pdf-docx-import
- Implementación de 006-cv-editor
- Cuentas de usuario
- DB server-side
- Pagos

## Notes

- Si el owner rechaza, no se reescribe el código — se cae al Plan B (sessionStorage). Las features 005/006 se ajustan para usar sessionStorage en lugar de localStorage. La Constitución NO se enmienda.
- El proceso de enmienda está documentado en el propio `constitution.md` §Gobernanza. Este tasks.md sigue ese proceso.
