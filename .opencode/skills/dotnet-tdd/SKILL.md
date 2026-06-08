---
name: dotnet-tdd
description: TDD con ciclo rojo-verde-refactor para BuildCv-api usando xUnit + FluentAssertions. Úsala cuando el cambio toque el motor de puntaje, scorer, matcher, normalizador, stemmer, blocklist, cascade C1–C5 o cualquier lógica pura de dominio. Triggers: "TDD", "rojo-verde-refactor", "tests primero", "test-first", "escribe el test", "implementa con TDD", "ciclo TDD".
---

# Skill · TDD con xUnit (BuildCv-api)

## Propósito

Aplicar el ciclo **rojo → verde → refactor** con xUnit + FluentAssertions al motor de puntaje y a la lógica pura de dominio, según exige el Art. VIII de la Constitución. El test describe la regla de negocio **antes** de que exista el código que la satisface.

## Cuándo invocarla

- Cambio que toca `ScoringEngine`, `SkillMatcher`, cascada C1–C5.
- Cambio que toca `SpanishTextNormalizer`, `SpanishLightStemmer`, `ConfusableBlocklist`.
- Cambio que toca `SectionSplitter`, `SkillScanner`, `JobAnalyzer`, `CvAnalyzer`.
- Cualquier corrección de bug en lógica de dominio: el test reproduce el bug **antes** del fix.
- El usuario lo pide explícitamente ("haz TDD", "tests primero", "rojo-verde-refactor").

## Procedimiento

### 1. Lee el spec / el bug

Identifica **qué regla de negocio** debe verificar el test. Si el cambio es un bug, escribe el test que **reproduce** el bug.

Fuentes:
- `specs/001-mvp-cv-ats/spec.md` (FR-005..008, FR-012, FR-016..018, FR-021, FR-029)
- `.specify/memory/constitution.md` Art. II, VIII
- El reporte de bug o la historia de usuario

### 2. ROJO — escribe el test

Colócalo en `tests/BuildCv.Domain.Tests/<Capa>/<Clase>Tests.cs` (o `BuildCv.Application.Tests/` si es un handler).

```csharp
using FluentAssertions;
using Xunit;

namespace BuildCv.Domain.Tests.Scoring;

public sealed class ScoringEngineTests
{
    private readonly ScoringEngine _engine = new(new ConfusableBlocklist(), /* ... */);

    [Fact]
    public void Should_not_confuse_java_with_javascript_in_matcher()
    {
        var cv = "Desarrollador Java con 5 años.";
        var job = "Buscamos desarrollador JavaScript Senior.";

        var result = _engine.Score(cv, job);

        result.Present.Should().BeEmpty();
        result.Missing.Should().Contain("javascript");
    }
}
```

**Convenciones del test:**
- Nombres: `Should_<verbo>_when_<condición>` o `Method_under_test_returns_expected_when_condition`.
- Arrange / Act / Assert explícito; comentarios solo si la intención no es obvia.
- FluentAssertions, no `Assert.Equal` pelado.
- Un assert conceptual por test.
- Sin `[Skip]`, sin `if (false) { ... }`.

### 3. Verifica que el test falla por la razón correcta

```bash
dotnet test --filter "FullyQualifiedName~Should_not_confuse_java_with_javascript"
```

El test **debe** fallar. Si pasa sin implementación, el test no está describiendo la regla de negocio; reescribe.

Si falla por otra razón (compilación, excepción no relacionada), ajusta hasta que el mensaje de error describa exactamente la regla que falta.

### 4. VERDE — implementa lo mínimo

Implementa el código de producción **mínimo** para que el test pase. No agregues funcionalidad extra, no optimices, no refactorices.

### 5. Verifica que pasa

```bash
dotnet test --filter "FullyQualifiedName~Should_not_confuse_java_with_javascript"
```

Ahora debe pasar. **Y** el resto de la suite debe seguir verde:

```bash
dotnet test
```

### 6. REFACTOR — limpia sin cambiar comportamiento

- Renombra variables para que se expliquen solas.
- Extrae métodos duplicados.
- Mejora nombres de tipos.
- **Nunca** cambies el comportamiento ni la versión del motor sin bumpear `EngineVersion` (Art. II).

Los tests siguen verdes después de cada refactor.

### 7. Versión + cobertura

- Si cambiaste la lógica del motor, bumpea `EngineVersion` en `ScoringEngine` (SemVer).
- Actualiza los tests de reproducibilidad con la nueva versión esperada.
- Verifica la cobertura:

```bash
dotnet test -c Release --collect:"XPlat Code Coverage"
```

Umbral motor + cascada + matcher + normalizador + stemmer + blocklist: **≥ 90%**.

### 8. Pre-flight de cierre

```bash
dotnet build BuildCv.slnx -c Release          # 0 warnings
dotnet test                                    # 100% verde
dotnet format --verify-no-changes              # limpio
dotnet list src/BuildCv.Domain reference       # solo Microsoft.NETCore.App
dotnet list src/BuildCv.Domain package references   # 0 paquetes
```

## Tests de reproducibilidad (Art. II)

```csharp
[Theory]
[InlineData("cv-senior-dotnet.md", "vacante-tech-co.md", 78)]
[InlineData("cv-jr-frontend.md", "vacante-tech-co.md", 52)]
public void Same_input_same_score(string cvFixture, string jobFixture, int expected)
{
    var (cv, job) = LoadFixtures(cvFixture, jobFixture);
    var first  = _engine.Score(cv, job);
    var second = _engine.Score(cv, job);
    first.Score.Should().Be(second.Score).And.Be(expected);
}
```

Fixtures en `tests/BuildCv.Domain.Tests/Fixtures/` (cuando existan). Cargados con `MemberData` o helper de lectura.

## Español preservado y confundibles (FR-016/017)

Tests **obligatorios** en `Domain.Tests/Text/`:

```csharp
[Theory]
[InlineData("Node.js Developer", new[] { "node.js" })]
[InlineData("5 años de experiencia", new[] { "año" })]
[InlineData("Manejo de C# avanzado", new[] { "c#" })]
[InlineData("他知道 .NET", new[] { ".net" })]
public void Normalizer_preserves_technical_tokens_and_spanish_diacritics(string input, string[] expected)
{
    var tokens = _normalizer.Tokenize(input);
    tokens.Should().Contain(expected);
}

[Theory]
[InlineData("java", "javascript")]
[InlineData("c", "c#")]
[InlineData("node", "node.js")]
[InlineData("postgres", "postgresql")]   // ESTE sí es equivalente
public void Confusables_blocklist_keeps_distinct_skills_separated(string cvSkill, string jobSkill)
{
    _blocklist.AreConfusable(cvSkill, jobSkill).Should().BeTrue();
}
```

## Anti-patrones

- ❌ Escribir el código de producción primero y el test después. **Siempre test primero.**
- ❌ Modificar la implementación en el mismo commit que introduce el test. Rojo y verde van en commits separados (o al menos, claramente identificables).
- ❌ `Assert.Equal` pelado. Usa `result.Should().Be(...)`.
- ❌ `[Skip]` para "apagar" un test rojo. Si es rojo, arréglalo.
- ❌ Tests que dependen de `DateTime.Now` o `Random.Shared` en Arrange. Inyecta `TimeProvider` o `Func<int>`.
- ❌ Cambiar la lógica del motor sin bumpear `EngineVersion` y actualizar tests de reproducibilidad.

## Salida esperada al cerrar

```
## TDD ciclo cerrado

**Regla de negocio**: <una frase>
**Test**: <ruta>:<línea>  —  <nombre del test>
**Pasos**:
  1. ROJO  → falló por <motivo correcto>
  2. VERDE → pasa tras implementar <cambio mínimo>
  3. REFACTOR → <qué se limpió>
**EngineVersion**: <antes> → <después> (si aplica)
**Cobertura**: <capa> = <%> (umbral = <%>)
**Pre-flight**: build 0 warnings · test 100% verde · format limpio
```
