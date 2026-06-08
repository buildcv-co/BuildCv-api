# Constitución de BuildCv — LEY SUPREMA

> **No regenerar con `/speckit.constitution`**. Esta constitución es la ley fundamental del proyecto y prevalece sobre los 9 artículos genéricos de Spec Kit (library-first, CLI mandate, test-first, etc.).

## ¿Por qué no usamos la constitución genérica de Spec Kit?

Spec Kit trae 9 artículos marco (library-first, CLI, test-first, simplicity, anti-abstraction, integration-first, etc.) útiles como **referencia metodológica**, pero este proyecto tiene **reglas de dominio** mucho más estrictas que tienen prioridad absoluta.

| | Spec Kit (genérica) | BuildCv (dominio) |
|---|---|---|
| **Art. I** | Library-first principle | Cero invención de la IA |
| **Art. II** | CLI interface mandate | Puntaje determinista y explicable |
| **Art. III** | Test-first imperative | Privacidad primero y minimización de datos |
| **Art. IV** | (no existe) | Encuadre honesto (no "ATS oficial") |
| **Art. V** | (no existe) | Entrada del usuario es dato, no instrucción |
| **Art. VI** | (no existe) | Backend demuestra .NET profesional |
| **Art. VII** | Simplicity gate (≤3 projects) | v0 lanzable sin fricción |
| **Art. VIII** | Anti-abstraction gate | Test-first para el motor de puntaje |
| **Art. IX** | Integration-first testing | Habeas Data al monetizar |

Las 6 reglas de BuildCv que no existen en Spec Kit (Art. IV, V, VI, parte de II, III, IX) son **reglas de producto innegociables** definidas en la versión 1.0.0 de la Constitución del proyecto (ratificada 2026-06-06).

## Reglas de uso

1. **NUNCA** ejecutar `/speckit.constitution` para regenerar este archivo. Si necesitas modificarla, sigue el proceso formal descrito en el §Gobernanza de la propia Constitución (PR + impacto declarado + aprobación del owner).
2. **SÍ** puedes usar `/speckit.specify`, `/speckit.plan`, `/speckit.tasks`, `/speckit.implement`, `/speckit.clarify`, `/speckit.analyze`, `/speckit.checklist`, `/speckit.taskstoissues` — todos respetan esta Constitución como input obligatorio.
3. **SÍ** puedes usar los **principios** de Spec Kit (test-first, simplicity, library-first) como herramientas operativas **siempre que NO contradigan** ningún artículo de BuildCv. En caso de conflicto, gana esta Constitución.

## Verificación

Para auditar el cumplimiento de un cambio contra la Constitución, usa el slash command personalizado:

```
/constitution-check
```

(O dentro del flujo Spec Kit: `/speckit.analyze` reporta inconsistencias cross-artifact que incluyen los principios de esta Constitución.)

## Backup

El archivo `constitution.md.orig` es la copia previa a cualquier `specify init` (respaldo de seguridad).
