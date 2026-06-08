# Regla · Calidad y testing

> Esta regla opera **bajo** la Constitución (Art. II, VIII). Cita el artículo cuando la justifiques.

## Cero supresiones (regla global)

**PROHIBIDO**, sin excepciones:

```
# C#
#pragma warning disable ...
#pragma warning restore ...
[SuppressMessage(...)]
InternalsVisibleTo ...   // salvo justificación explícita en el PR

# xUnit
[Skip]
[Skip("...")]
[SkipIf(...)]
[SkipUnless(...)]
[Fact(DisplayName = "Skip:" + ...)]   // cualquier variante

# dotnet
dotnet test --filter "FullyQualifiedName!~..."   // para esconder un test roto
```

**Única excepción justificada:** `[SuppressMessage]` cuando la **regla global** y la **Constitución** estén en conflicto, con comentario que cite el artículo de la Constitución que lo justifica y aprobación en el PR.

> "Si hay un error: CORRÍGELO. Nunca lo silencies."

## TDD del motor de puntaje (Art. VIII, FR-005..008, FR-012, FR-016..018)

Ciclo **rojo → verde → refactor** con xUnit + FluentAssertions:

1. **Rojo** — escribe el test **antes** de la implementación. El test describe la regla de negocio.
   ```csharp
   [Fact]
   public void Should_not_confuse_java_with_javascript_in_matcher()
   {
       var result = _matcher.Match(cvHas: ["java"], jobAsks: ["javascript"]);
       result.Present.Should().BeEmpty();
       result.Missing.Should().Contain("javascript");
   }
   ```
2. **Verifica que falla** — `dotnet test --filter "FullyQualifiedName~Should_not_confuse_java"`.
3. **Verde** — implementa lo mínimo para que pase.
4. **Refactor** — limpia sin cambiar comportamiento; los tests siguen verdes.
5. **Versión** — si cambia la lógica del motor, bumpea `EngineVersion` y actualiza los tests afectados (Art. II).

### Tests de reproducibilidad (Art. II, FR-006)

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

El motor **debe** ser función pura: mismo input + misma versión → mismo output, **siempre**.

## Español preservado (FR-016/017)

Tests **obligatorios** en `Domain.Tests/Text/`:

```csharp
[Theory]
[InlineData("Node.js Developer", new[] { "node.js" })]
[InlineData("5 años de experiencia", new[] { "año" })]   // no "ano"
[InlineData("他知道 .NET", new[] { ".net" })]
[InlineData("Manejo de C# avanzado", new[] { "c#" })]
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

## Estilo de tests

- **Arrange / Act / Assert** explícito, con comentarios solo si la intención no es obvia.
- Nombres de método: `Should_<verbo>_when_<condición>` o `Method_under_test_returns_expected_when_condition`.
- **Un assert conceptual por test** (pueden ser varios `.Should()` que validan el mismo concepto, no asserts de cosas distintas).
- `FluentAssertions` siempre, no `Assert.Equal` pelado.
- `[Theory] + [InlineData]` para parametrizar; `[MemberData]` cuando los fixtures vienen de archivos.
- **No** `[Skip]`, **no** condicionales `if (false) { ... }` para "apagar" un test.
- Tests deterministas: no `DateTime.Now`, no `Random.Shared` en el Arrange; inyecta un `TimeProvider` o un `Func<int>` si la unidad bajo prueba lo requiere.

## Cobertura

- **Motor de puntaje + cascada + matcher + normalizador + stemmer + blocklist**: ≥ 90% line coverage (Art. VIII lo exige; el motor es lógica pura, es barato subir).
- **Endpoints / wiring**: ≥ 70% (cubierto por integration tests).
- **Adaptadores de IO** (cuando lleguen): excluidos con `ExcludeFromCodeCoverage` **solo si tienen IO real**; si son puros, también se cubren.

Verifica con:

```bash
dotnet test -c Release --collect:"XPlat Code Coverage"
```

## Pre-flight de tarea (los 4 obligatorios)

Antes de decir "listo", ejecuta y revisa:

```bash
dotnet build BuildCv.slnx -c Release          # 0 warnings (warnings-as-errors)
dotnet test                                    # 100% verde
dotnet format --verify-no-changes              # 0 cambios pendientes
dotnet list src/BuildCv.Domain reference       # solo Microsoft.NETCore.App
dotnet list src/BuildCv.Domain package references   # 0 paquetes
git status                                     # solo cambios intencionales
```

## Code style (.editorconfig ya fija las reglas)

- 4 espacios en `.cs`; 2 en `.json`/`.yml`/`.csproj`/`.slnx`.
- `csharp_style_namespace_declarations = file_scoped:warning`.
- `csharp_using_directive_placement = outside_namespace:warning`.
- `dotnet_sort_system_directives_first = true`.
- `dotnet format` valida todo; el CI corre `--verify-no-changes`.
- **No** comentes código. Si una línea necesita explicación, refactoriza para que se explique sola (Art. VI — claridad como señal de seniority).
