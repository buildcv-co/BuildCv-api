---
name: constitution-compliance
description: Verifica que un cambio cumple los nueve artículos de la Constitución de BuildCv (Art. I–IX) antes de cerrar una tarea. Úsala cuando toques el motor de puntaje, la privacidad, la honestidad de encuadre, la entrada del usuario como dato, los hitos v0/v1 o el cumplimiento Habeas Data. Triggers: "cumple la constitución", "constitution check", "verifica constitución", "esto rompe algún artículo", "compliance check", "validar contra constitución".
---

# Skill · Constitution Compliance

## Propósito

Antes de cerrar un cambio (commit, PR, fin de tarea), verificar **artículo por artículo** que el cambio es compatible con `.specify/memory/constitution.md` v1.0.0. Si viola cualquier artículo, **bloquea** el cierre y propone corrección o enmienda formal.

## Cuándo invocarla

- Cualquier cambio que toque el motor de puntaje, scorer, matcher, normalizador, stemmer, blocklist.
- Cualquier cambio que toque logging, persistencia, configuración, secretos, rate limit.
- Cualquier cambio que toque el copy público, OpenAPI descriptions, mensajes de error, ProblemDetails.
- Cualquier cambio que toque prompts de IA, entrada del usuario, bloques con nonce, sanitización.
- Cualquier cambio que toque el árbol de decisiones v0/v1 (cuentas, pagos, persistencia, consentimiento).
- El usuario lo pide explícitamente.

## Procedimiento

### 1. Identifica el cambio

Lee los archivos modificados:

```bash
git status
git diff --stat
```

Resume **una frase** qué hace el cambio (ej: "añade logging del CV recibido en el handler de scoring").

### 2. Mapea contra los nueve artículos

Recorre la Constitución y emite un veredicto por artículo:

| Art. | Pregunta de verificación | PASA / REVIERTE / JUSTIFICA |
|---|---|---|
| **I** | ¿La adaptación con IA podría inventar experiencia, empresas, cargos, fechas, métricas? | |
| **II** | ¿El cálculo del puntaje sigue siendo determinista, función pura, sin LLM? | |
| **III** | ¿Se persiste algo del CV/vacante? ¿Los logs incluyen su contenido? | |
| **IV** | ¿Copy/docs/mensajes prometen "puntaje ATS oficial" o empleo garantizado? | |
| **V** | ¿El CV/vacante se trata como dato, no como instrucción? ¿Hay defensa contra prompt-injection? | |
| **VI** | ¿El dominio sigue PURO? ¿El IO está detrás de puertos? ¿Hay sobre-ingeniería? | |
| **VII** | ¿Se introduce fricción en v0 (cuenta, login, guardado) que no debería estar? | |
| **VIII** | ¿Si toca el motor, los tests se escribieron ANTES de la implementación? | |
| **IX** | ¿Si toca v1 (cuentas, pagos, persistencia), hay consentimiento/Habeas Data/ZDR verificado? | |

### 3. Emite el veredicto final

- **PASA** — todos los artículos en verde. Procede al cierre.
- **REVIERTE** — al menos un artículo en rojo. Detente, explica el problema, propone corrección.
- **JUSTIFICA** — el cambio **necesariamente** toca un artículo (ej: enmienda la Constitución). Verifica que:
  1. Se ha propuesto una enmienda formal (PR que toque `.specify/memory/constitution.md`).
  2. La enmienda declara impacto en `spec.md`, `plan.md`, `tasks.md`.
  3. El dueño ha aprobado.

### 4. Formato de salida

```
## Veredicto de cumplimiento constitucional

**Cambio**: <una frase>
**Artículos verificados**: 9 / 9

| Art. | Estado | Nota |
|------|--------|------|
| I    | PASA / REVIERTE / JUSTIFICA | <breve> |
| II   | ... | ... |
| ...  | ... | ... |

**Veredicto final**: PASA / REVIERTE / JUSTIFICA
**Acción**: <proceder al cierre / corregir X antes de cerrar / esperar aprobación de enmienda>
```

## Reglas duras (bloquean el cierre)

Los artículos **I, II, III, V** son reglas duras de producto. Si **cualquiera** falla, el veredicto es **REVIERTE** sin posibilidad de "JUSTIFICA" salvo enmienda formal aprobada.

Los artículos **IV, VI, VII, VIII, IX** admiten "JUSTIFICA" con justificación documentada (PR con motivo, impacto declarado, aprobación).

## Anti-patrones

- ❌ "Es un cambio pequeño, no necesita check constitucional" — **todo** cambio pasa por Constitución.
- ❌ "La Constitución es para el v0; esto es para v1" — la Constitución es **ley suprema**; el versionado no la exonera.
- ❌ "Ya lo discutimos en planning" — la verificación es por cambio, no por milestone.
- ❌ "La Constitución es muy estricta, la vamos a relajar" — la vía es **enmienda formal**, no relajación silenciosa.

## Ejemplo

**Cambio**: "Log del CV completo cuando llega al endpoint de scoring para depurar."

```
## Veredicto de cumplimiento constitucional

**Cambio**: Añade `_logger.LogInformation("CV: {Cv}", cvText)` en `ScoringEndpoints`.
**Artículos verificados**: 9 / 9

| Art. | Estado | Nota |
|------|--------|------|
| III  | REVIERTE | Registra contenido del CV. Viola NFR-002 y Art. III ("los logs NUNCA incluyen su contenido"). |

**Veredicto final**: REVIERTE
**Acción**: Reemplazar el log por metadatos no sensibles: `cvText.Length`, `jobText.Length`, `traceId`, código de error si aplica.
```
