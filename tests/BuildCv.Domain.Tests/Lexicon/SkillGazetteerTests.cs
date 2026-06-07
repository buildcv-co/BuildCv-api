using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Text;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Lexicon;

public sealed class SkillGazetteerTests
{
    private static SkillGazetteer Build()
    {
        var entries = new[]
        {
            new SkillEntry(
                "csharp", "C#", SkillCategory.HardSkill,
                Aliases: ["c sharp", "csharp"], Implies: ["dotnet"], Related: ["dotnet"],
                Broader: [], ConfusableWith: ["c", "cpp"]),
            new SkillEntry(
                "postgresql", "PostgreSQL", SkillCategory.Tool,
                Aliases: ["postgres", "psql"], Implies: ["sql"], Related: ["sql"],
                Broader: ["sql"], ConfusableWith: []),
        };

        return new SkillGazetteer("2026.06.0", entries, new SpanishTextNormalizer());
    }

    [Fact]
    public void Resuelve_por_canonical_normalizado()
    {
        Build().TryResolve("c#", out var entry).Should().BeTrue();
        entry!.Id.Should().Be("csharp");
    }

    [Fact]
    public void Resuelve_por_alias()
    {
        Build().TryResolve("postgres", out var entry).Should().BeTrue();
        entry!.Id.Should().Be("postgresql");
    }

    [Fact]
    public void No_resuelve_un_token_desconocido()
    {
        Build().TryResolve("cobol", out _).Should().BeFalse();
    }

    [Fact]
    public void Related_e_Implies_devuelven_las_relaciones()
    {
        var gazetteer = Build();
        gazetteer.Related("csharp").Should().Contain("dotnet");
        gazetteer.Implies("postgresql").Should().Contain("sql");
    }

    [Fact]
    public void AreConfusable_es_simetrico_y_no_marca_iguales()
    {
        var gazetteer = Build();
        gazetteer.AreConfusable("csharp", "c").Should().BeTrue();
        gazetteer.AreConfusable("c", "csharp").Should().BeTrue();
        gazetteer.AreConfusable("csharp", "postgresql").Should().BeFalse();
        gazetteer.AreConfusable("csharp", "csharp").Should().BeFalse();
    }

    [Fact]
    public void Sella_la_version_del_recurso()
    {
        Build().Version.Should().Be("2026.06.0");
    }
}
