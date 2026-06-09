# Feature 007 — Enmienda de la Constitution v1.0.0 → v1.1.0

> **Status:** ✅ RATIFICADA (commit 4b3be4a, 2026-06-09) · **Tipo:** Enmienda formal, NO feature de código
> **Owner approval required:** SÍ (proceso documentado en Constitution §Gobernanza)
> **Próxima:** [`../000-INDEX.md`](../000-INDEX.md)

## Resumen

Esta NO es una feature de producto. Es un **proceso de gobernanza** documentado: la modificación formal de la Constitution v1.0.0 (ley suprema del proyecto) para permitir la persistencia local del CV en el browser del usuario.

**Por qué se necesita:**

La Constitution v1.0.0 Art. III dice:
> *"En v0, el sistema MUST procesar el CV y la vacante en memoria y NO persistirlos (FR-040, NFR-001)."*

Las nuevas features 005 (import PDF/DOCX) y 006 (editor) requieren **persistir el CV en el dispositivo del usuario** (localStorage del browser) entre sesiones de edición. La persistencia NO es server-side — sigue siendo respetuosa de Art. III en su espíritu (el CV no sale del dispositivo del usuario, no hay DB server-side).

**Lo que cambia:**

| Art. | Cambio | Razón |
|---|---|---|
| **Art. III** | Se **permite explícitamente** persistencia local en el dispositivo del usuario (localStorage, IndexedDB) para el borrador de edición. Se mantiene la prohibición de persistencia server-side hasta v1.0 con cuentas + Habeas Data. | Habilitar 005 (import) y 006 (editor) sin contradicción con la ley suprema |
| **Art. I** | Se añade regla: el editor frontend NO puede agregar entidades nuevas que el usuario no haya escrito. Defense in depth contra invención del lado cliente. | Consistencia con Art. I (cero invención) — el editor no debe inventar experiencia |
| **Art. VI** | Se añaden 2 puertos a la lista: `ICvParser` (Application, parseo de archivos) y `ICvStore` (frontend, localStorage). | Coherencia con la arquitectura: cada IO detrás de un puerto |
| **Art. VII** | Se añade política de rate-limit `"import"` (30/h por IP, CPU-bound, más permisivo que "ai"). | Proteger el servidor sin friccionar UX legítimo |
| **Art. IX** | Se añade cláusula explícita: gate ZDR para `AdaptCv` cuando M1-IA se habilite con Anthropic. El copy público NO puede decir "no entrenamiento" hasta verificación contractual. | Ya estaba en M1; se hace explícito en el texto constitucional |

**Lo que NO cambia:**

- **Art. I** (cero invención): sigue aplicando, con la nueva regla defense in depth del editor.
- **Art. II** (determinismo del score): sin cambios.
- **Art. IV** (encuadre honesto): sin cambios.
- **Art. V** (entrada como dato): sin cambios.
- **Art. VIII** (TDD): sin cambios.
- **Art. IX** (Habeas Data) en su cláusula v1.0+ con cuentas: sin cambios.

## Proceso de enmienda (siguiendo §Gobernanza de la Constitution)

### 1. Propuesta

Se redacta este change con:
- Texto nuevo de Art. III (con FR-040a, NFR-001a).
- Texto adicional en Art. I, Art. VI, Art. VII, Art. IX.
- Bump de versión: 1.0.0 → 1.1.0 (cambio MENOR — añade capacidades, no rompe compatibilidad).

### 2. Impacto declarado

| Artefacto afectado | Cambio requerido |
|---|---|
| `constitution.md` | Bump versión 1.0.0 → 1.1.0, fecha 2026-06-08 → 2026-06-09, nuevas reglas |
| `CONSTITUTION-README.md` | Actualizar la tabla comparativa Spec-Kit vs Constitution |
| `specs/000-INDEX.md` | Mover 005/006/007 de PLANEADO a EN CURSO (cuando se empiece a implementar) |
| `specs/002-score-engine/spec.md` | Sin cambios (sigue compatible) |
| `specs/003-adapt-ia/spec.md` | Sin cambios (sigue compatible, su StubAiClient sigue válido) |
| `specs/004-export-pdf/spec.md` | Sin cambios |
| `specs/005-cv-pdf-docx-import/spec.md` (NUEVO) | Se crea bajo v1.1.0 |
| `specs/006-cv-editor/spec.md` (NUEVO) | Se crea en `BuildCv-web/specs/006-web-cv-editor/` (frontend, no en el API) |
| `BuildCv-web/AGENTS.md` | Actualizar referencia a Constitution v1.1.0 |
| `BuildCv-api/AGENTS.md` | Actualizar referencia a Constitution v1.1.0 ✅ DONE (v1.0.0 → v1.1.0 en línea de Constitución + Art. VII con 4 políticas) |

### 3. Aprobación

**Owner approval required.** Esta enmienda es un cambio MENOR pero habilita features que tocan persistencia, así que requiere sign-off explícito del owner del proyecto.

### 4. Registro

- Versión: 1.0.0 → **1.1.0**
- Fecha de última enmienda: 2026-06-06 → **2026-06-09**
- Fecha de ratificación original: 2026-06-06 (NO cambia, es histórica)
- PR al constitution con este spec como justificacion

## Próximos pasos después de la enmienda

Una vez merged:
1. **005-cv-pdf-docx-import**: implementación con persistencia local habilitada
2. **006-cv-editor**: implementación con persistencia local habilitada
3. Tests del constitution-check.sh actualizados para validar la nueva regla
