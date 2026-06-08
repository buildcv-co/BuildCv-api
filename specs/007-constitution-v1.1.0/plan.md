# Plan: 007-constitution-v1.1.0 (enmienda governance)

> **Tipo:** Plan de governance, NO plan de implementación de código. El "deliverable" es un PR con diff al archivo `constitution.md`.

## Approach

Esta es una enmienda a un artifact existente (`BuildCv-api/.specify/memory/constitution.md`). El flujo NO incluye:

- ❌ Crear nuevos proyectos .NET
- ❌ Nuevos NuGet packages
- ❌ Nuevas features de código
- ❌ Tests automatizados (los tests del constitution-check.sh se actualizan DESPUÉS, en T1.4)

El flujo SÍ incluye:

- ✅ Modificación al `constitution.md` (v1.0.0 → v1.1.0) con diff claro
- ✅ Actualización de `CONSTITUTION-README.md`
- ✅ PR con este spec.md como justificacion
- ✅ Aprobación del owner antes de merge

## Cambios concretos al texto constitucional

### Header del documento

```diff
- Versión: 1.0.0 · Fecha de ratificación: 2026-06-06 · Última enmienda: 2026-06-06
+ Versión: 1.1.0 · Fecha de ratificación: 2026-06-06 · Última enmienda: 2026-06-09
```

### Art. III — diff

```diff
- En v0, el sistema MUST procesar el CV y la vacante en memoria y NO persistirlos (FR-040, NFR-001).
- El sistema MUST NOT registrar en logs el contenido del CV o de la vacante; solo metadatos no sensibles (longitudes, conteos, modelo usado, identificador de traza) (FR-041, NFR-002).
- El sistema MUST minimizar los datos enviados al proveedor de IA al mínimo necesario para la tarea (FR-043, NFR-003).
- El borrador local del texto, si existe, MUST permanecer en el dispositivo del usuario, borrarse al cerrar la sesión del navegador y NO viajar al servidor salvo al ejecutar una operación solicitada (FR-004).
+ En v0.5 (fase actual), el sistema MUST procesar el CV y la vacante en memoria del servidor. La persistencia local EXCLUSIVAMENTE en el dispositivo del usuario (localStorage, IndexedDB) está permitida para el borrador de edición, con borrado explícito al logout o a solicitud del usuario (FR-040, FR-040a, NFR-001, NFR-001a). v1.0 introducirá cuentas de usuario y persistencia server-side con consentimiento expreso (Habeas Data, Art. IX).
+ El sistema MUST NOT registrar en logs el contenido del CV o de la vacante; solo metadatos no sensibles (longitudes, conteos, modelo usado, identificador de traza) (FR-041, NFR-002).
+ El sistema MUST minimizar los datos enviados al proveedor de IA al mínimo necesario para la tarea (FR-043, NFR-003).
+ El borrador local del texto, si existe, MUST permanecer en el dispositivo del usuario, borrarse al cerrar la sesión del navegador y NO viajar al servidor salvo al ejecutar una operación solicitada (FR-004).
+ El sistema frontend MUST ofrecer al usuario un mecanismo explícito de "Limpiar borrador" que purge toda persistencia local (localStorage, IndexedDB, sessionStorage) relacionada con su CV (FR-040b).
```

### Art. I — diff (añadir regla defense in depth)

```diff
- El sistema MUST comunicar al usuario el resultado de la verificación de honestidad: "sin invención" o "advertencia" con los términos potencialmente nuevos a revisar (FR-029).
+ El sistema MUST comunicar al usuario el resultado de la verificación de honestidad: "sin invención" o "advertencia" con los términos potencialmente nuevos a revisar (FR-029).
+ El editor frontend MUST NO agregar entidades nuevas (skills, certificaciones, experiencia, empresas, cargos, fechas, métricas) que el usuario no haya escrito explícitamente. El schema validado con Zod rechaza entidades nuevas en el round-trip Markdown (FR-029a, defense in depth del lado cliente).
```

### Art. VI — diff (añadir puertos)

```diff
- Los proveedores externos (IA, parseo, export, pagos) MUST estar tras puertos/abstracciones (`IAiClient`, `ICvParser`, `IPdfExporter`, `PaymentProvider`, …) para ser sustituibles sin tocar el núcleo (materializa FR-030 y la portabilidad de hitos).
+ Los proveedores externos (IA, parseo de archivos, export PDF, pagos) MUST estar tras puertos/abstracciones (`IAiClient`, `ICvParser`, `IPdfExporter`, `IPaymentProvider`, `ICvStore` para localStorage, …) para ser sustituibles sin tocar el núcleo.
```

### Art. VII — diff (añadir rate-limit "import")

```diff
- El sistema MUST limitar el uso por origen con políticas diferenciadas por costo (más estricta para la adaptación con IA que para el análisis determinista) para proteger el presupuesto de IA sin fricción para usuarios legítimos (FR-036, FR-038, US-011).
+ El sistema MUST limitar el uso por origen con políticas diferenciadas por costo (más estricta para la adaptación con IA que para el análisis determinista o el import de archivos) para proteger el presupuesto de IA y CPU sin fricción para usuarios legítimos (FR-036, FR-038, FR-039a, US-011).
+
+ Políticas de rate-limit (Art. VII) — referencia:
+ - `"score"` (deterministic): 60/h por IP
+ - `"ai"` (adaptación con LLM): 5/h por IP
+ - `"export"` (PDF generation, CPU-bound): 20/h por IP
+ - `"import"` (PDF/DOCX parsing, CPU-bound): 30/h por IP (NUEVO en v1.1.0)
```

### Art. IX — diff (añadir cláusula gate ZDR para M1-IA)

```diff
- Antes de prometer públicamente "retención cero / no entrenamiento" del proveedor de IA, el sistema MUST verificarlo contractualmente; mientras no esté confirmado, el copy público MUST comunicar honestamente que el contenido se envía al proveedor y puede retenerse según su política. ZDR es un gate bloqueante, no una suposición (FR-042, NFR-022).
+ Antes de prometer públicamente "retención cero / no entrenamiento" del proveedor de IA, el sistema MUST verificarlo contractualmente; mientras no esté confirmado, el copy público MUST comunicar honestamente que el contenido se envía al proveedor y puede retenerse según su política. ZDR es un gate bloqueante, no una suposición (FR-042, NFR-022, NFR-022a).
+
+ Estado actual del gate ZDR (a fecha de enmienda v1.1.0, 2026-06-09): Anthropic acepta ZDR solo en cuentas Enterprise. La cuenta de BuildCV es estándar → ZDR NO se puede garantizar → copy público dice "el contenido se envía al proveedor y puede retenerse según su política". Cuando Anthropic Enterprise se habilite, hacer PR con diff contractual + bumpear a v1.2.0.
```

## Estrategia de merge

1. Branch: `007-constitution-v1.1.0`
2. PR con:
   - diff del `constitution.md` (mostrado arriba)
   - diff del `CONSTITUTION-README.md`
   - este spec.md como justificacion
3. Reviewer: owner
4. Merge con squash a `main`
5. Tag: `v1.1.0-constitution`
6. Comunicación: nota en CHANGELOG del proyecto (cuando exista)

## Riesgos

| Riesgo | Mitigación |
|---|---|
| Owner no aprueba la enmienda | Plan B: features 005/006 funcionan con sessionStorage en lugar de localStorage (borrado al cerrar tab) — sigue siendo Art. III-compatible sin enmienda. |
| Enmienda introduce ambigüedad en el texto | Diff claro con `+`/`-`, revisión por Constitutional Check después. |
| Enmienda contradice 003-adapt-ia (M1) | El M1 sigue siendo compatible (su copy honesto sobre ZDR se mantiene). El gate ZDR se hace explícito pero no cambia el comportamiento actual. |
| v1.0 con cuentas se demora y la persistencia local se queda como "permanente" indefinidamente | El Art. III v1.1.0 dice EXPLÍCITAMENTE que la persistencia local es para v0.5, no para v1.0+. Cuando llegue v1.0, se hará una nueva enmienda. |

## Out of Scope

- Implementación de 005-cv-pdf-docx-import (es la siguiente feature, no parte de 007)
- Implementación de 006-cv-editor (es la siguiente feature)
- Cuentas de usuario (es 009-auth, v1.0+)
- DB server-side (es 010-persistence, v1.0+)
- Pagos (es 011-payments, v1.0+)
