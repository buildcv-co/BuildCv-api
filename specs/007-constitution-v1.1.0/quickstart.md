# Quickstart: 007-constitution-v1.1.0 (cómo aplicar la enmienda)

> **Tipo:** Quickstart de governance. No hay código que correr — es el procedimiento de PR.

## Pre-requisitos

- Acceso de escritura a `BuildCv-api/` (repo `github.com/buildcv-co/BuildCv-api`)
- Permiso del owner para aprobar la enmienda (Constitution §Gobernanza)

## Procedimiento

### 1. Crear branch

```bash
cd ~/Dev/portfolio/buildCV/BuildCv-api
git checkout main
git pull origin main
git checkout -b 007-constitution-v1.1.0
```

### 2. Modificar `constitution.md`

Aplicar el diff documentado en [plan.md](./plan.md#cambios-concretos-al-texto-constitucional):

1. Header: `1.0.0 → 1.1.0`, fecha `2026-06-06 → 2026-06-09`
2. Art. III: reemplazar las 4 reglas existentes con las 5 reglas nuevas
3. Art. I: añadir la regla FR-029a
4. Art. VI: añadir `ICvParser` (ya estaba) + `ICvStore` (NUEVO)
5. Art. VII: añadir la tabla de políticas con la nueva `"import"`
6. Art. IX: añadir la cláusula del estado actual del gate ZDR

### 3. Modificar `CONSTITUTION-README.md`

Actualizar la tabla comparativa Spec-Kit vs Constitution. Añadir nota:

> **v1.1.0 (2026-06-09) — Enmienda menor:** se permite persistencia local explícitamente (Art. III). Se añaden puertos `ICvParser` y `ICvStore`. Se añade política `"import"` (Art. VII). Se refuerza gate ZDR (Art. IX). Ver [specs/007-constitution-v1.1.0/spec.md](../specs/007-constitution-v1.1.0/spec.md).

### 4. Commit

```bash
git add .specify/memory/constitution.md .specify/memory/CONSTITUTION-README.md
git commit -m "docs(constitution): enmienda v1.0.0 → v1.1.0

- Art. III: permite persistencia local EXCLUSIVAMENTE (localStorage, IndexedDB)
  para el borrador de edición. Mantiene prohibición de persistencia server-side.
- Art. I: añade regla FR-029a (defense in depth del editor contra invención).
- Art. VI: añade ICvParser y ICvStore a la lista de puertos.
- Art. VII: añade política de rate-limit 'import' (30/h por IP).
- Art. IX: añade cláusula explícita del estado del gate ZDR.

Bump: 1.0.0 → 1.1.0 (MENOR — añade capacidades, no rompe compatibilidad).
M0/M1/M2 siguen funcionando sin cambios.

Spec justificativo: specs/007-constitution-v1.1.0/spec.md
Proceso: Constitution §Gobernanza (cambio MENOR, owner approval)."
```

### 5. PR

```bash
git push origin 007-constitution-v1.1.0
gh pr create --base main --head 007-constitution-v1.1.0 \
  --title "docs(constitution): enmienda v1.0.0 → v1.1.0" \
  --body "Ver specs/007-constitution-v1.1.0/spec.md para justificacion completa.

Cambio MENOR: habilita persistencia local (frontend) sin contradecir Art. III.
Bump: 1.0.0 → 1.1.0.

Impacto: 0 cambios de código runtime. Habilita features 005 (import PDF/DOCX) y 006 (editor).

Aprobación requerida: owner (proceso formal de enmienda).
Reviewer sugerido: @owner"
```

### 6. Después del merge

```bash
# Limpiar branch
git checkout main
git pull origin main
git branch -d 007-constitution-v1.1.0
```

### 7. Tag

```bash
git tag v1.1.0-constitution
git push origin v1.1.0-constitution
```

## Verificación post-merge

1. `./scripts/constitution-check.sh` debe seguir exit 0 (las reglas nuevas no rompen los checks existentes; los checks se actualizarán en T1.4 cuando se implementen features 005/006).
2. `./scripts/preflight.sh` debe seguir exit 0.
3. `cat .specify/memory/constitution.md | head -10` debe mostrar `Versión: 1.1.0` y `Última enmienda: 2026-06-09`.

## Out of scope

- Implementación de 005/006 (siguiente fase)
- Cambios al código runtime
- Bumps de versión a v1.2.0 o v2.0.0
