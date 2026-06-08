# Research: 007-constitution-v1.1.0 (research governance)

> **Tipo:** Research de governance, no técnico. Documenta las **alternativas consideradas** para la enmienda y por qué se eligió v1.0.0 → v1.1.0.

## Pregunta de research

¿Cómo permitir la persistencia local del CV en el browser sin contradecir la ley suprema?

## Alternativas consideradas

### Alternativa 1 — NO enmendar, usar sessionStorage (Status quo + workaround)

**Descripción:** Mantener Constitution v1.0.0 tal cual. Las features 005/006 usan `sessionStorage` en lugar de `localStorage`. El borrador se borra al cerrar el tab del browser.

**Pros:**
- NO requiere owner approval.
- NO requiere proceso de enmienda.
- Cumple Art. III literal (sin persistencia cross-session).

**Contras:**
- El usuario pierde su trabajo al cerrar el tab (UX malo).
- El editor es prácticamente inutilizable si solo se puede usar en una sesión.
- Es un "hack" que contradice el espíritu del usuario (quiere editar su CV entre sesiones).

**Decisión:** ❌ Rechazado. UX inaceptable.

### Alternativa 2 — Enmienda v1.0.0 → v1.1.0 con persistencia local explícita

**Descripción:** Modificar Art. III para permitir EXPLÍCITAMENTE persistencia local en el dispositivo del usuario (localStorage, IndexedDB), con borrado a solicitud. Mantener la prohibición de persistencia server-side.

**Pros:**
- Cumple el spec original D11 (parseo de archivos para FormatScore completo).
- Habilita UX esperada (CV editable entre sesiones).
- Sigue siendo respetuoso de Art. III en su espíritu (el CV no sale del dispositivo del usuario).
- Documenta el proceso de governance (transparencia).
- Es un cambio MENOR (v1.0.0 → v1.1.0), no MAYOR (no rompe compatibilidad).

**Contras:**
- Requiere owner approval (proceso governance).
- Si el owner no aprueba, hay que caer al Plan B (Alternativa 1).
- Es un cambio de "interpretación" de v0 → v0.5 (la Constitución decía "v0 no persiste", ahora dice "v0.5 puede persistir local").

**Decisión:** ✅ **ELEGIDA**. Es la ruta correcta.

### Alternativa 3 — Saltear a v1.0.0 con cuentas + DB + Habeas Data (Big Bang)

**Descripción:** Implementar directamente 009-auth + 010-persistence + 011-payments en lugar de persistencia local.

**Pros:**
- Cierra el gap completo hacia SaaS.
- Constitución final desde el día 1.

**Contras:**
- Scope 3-4x más grande (3 features más, no 1).
- Requiere gate ZDR de Anthropic (no verificable aún).
- Requiere integración Wompi (no trivial).
- El usuario NO pidió cuentas — pidió "módulo de import + editor".

**Decisión:** ❌ Rechazado. Scope demasiado grande para esta iteración.

### Alternativa 4 — Persistencia local SIN enmienda (interpretación liberal)

**Descripción:** NO modificar la Constitución. Argumentar que "v0 no persiste" se refiere a "persistencia server-side" (no contradice localStorage).

**Pros:**
- Cero governance overhead.
- Funciona.

**Contras:**
- Es una **interpretación cuestionable**. La Constitución dice "NO persistirlos" sin qualifier.
- Si el owner lo lee, podría decir "no, esto no es lo que dice la Constitución".
- Crea precedente de "interpretar" la ley en lugar de enmendarla.

**Decisión:** ❌ Rechazado. La governance importa. Enmendar formalmente es más seguro.

## Decisión final

**Elegida: Alternativa 2 — Enmienda v1.0.0 → v1.1.0 con persistencia local explícita.**

## Riesgos de la enmienda

| Riesgo | Mitigación |
|---|---|
| Owner no aprueba | Plan B: sessionStorage (Alternativa 1) |
| Enmienda contradice features shipped (M0/M1/M2) | M0/M1/M2 son compatibles — no persisten nada. La enmienda no los afecta. |
| v1.0 con cuentas se demora y la persistencia local queda permanente | Art. III v1.1.0 dice EXPLÍCITAMENTE "v0.5". Cuando llegue v1.0, nueva enmienda. |
| El copy público cambia (de "no persistimos" a "persistimos localmente") | Actualizar `lib/copy/es.ts` y landing page. Honesto y verificable. |

## Out of scope de esta research

- Implementación técnica de localStorage (eso es 006-cv-editor)
- Política de retención de datos v1.0+ (eso es 010-persistence)
- Gate ZDR de Anthropic (eso es M1-IA, no parte de esta enmienda)
