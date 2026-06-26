using System.Text.Json;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Scoring;

/// <summary>
/// Property-based determinism test para <see cref="ScoringEngine.ScoreV2"/>
/// (PR 3d de 021). Constitution Art. II exige que la función sea pura:
/// mismo <c>CvDocument + JobInput + engineVersion="2.0.0"</c> ⇒
/// <c>ScoreResultV2</c> byte-idéntico a través de 1000 ejecuciones
/// secuenciales y 1000 ejecuciones paralelas.
/// <para>
/// Este test es RED en el sentido histórico: la propiedad de determinismo
/// no estaba verificada explícitamente. Como PR 3b ya implementó
/// <c>ScoreV2</c> sin <c>DateTime.UtcNow</c> / <c>Guid.NewGuid</c> /
/// <c>Random</c> en el camino de cálculo, el test pasa en GREEN sin
/// tocar la implementación.
/// </para>
/// <para>
/// Garantía complementaria (puramente estática): <c>grep -rn
/// "DateTime.UtcNow|Guid.NewGuid|new Random" src/BuildCv.Domain/Scoring/
/// </c> debe devolver 0 hits en código de cálculo. Los 2 hits permitidos
/// son comentarios XML documentando la regla, no llamadas reales.
/// </para>
/// </summary>
public sealed class ScoringEngineV2DeterminismTests
{
    [Fact]
    public async Task ScoreV2_SameInput_ProducesByteIdenticalOutput_1000Times()
    {
        // Arrange — CV y job con datos totalmente deterministas (sin DateTime.UtcNow,
        // sin Guid.NewGuid, sin Random). Fechas fijas en formato YYYY-MM.
        var cv = CreateDeterministicCv();
        var job = CreateDeterministicJob();

        var firstResult = ScoringEngine.ScoreV2(cv, job);
        var firstJson = JsonSerializer.Serialize(firstResult, DeterminismJsonOptions.Value);

        // Act + Assert — 999 iteraciones adicionales deben producir JSON byte-idéntico.
        for (var i = 0; i < 999; i++)
        {
            var result = ScoringEngine.ScoreV2(cv, job);
            var json = JsonSerializer.Serialize(result, DeterminismJsonOptions.Value);
            json.Should().Be(firstJson, $"iteración {i} debe producir output byte-idéntico al primero");
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ScoreV2_ParallelExecution_ProducesByteIdenticalOutput()
    {
        var cv = CreateDeterministicCv();
        var job = CreateDeterministicJob();

        var firstJson = JsonSerializer.Serialize(
            ScoringEngine.ScoreV2(cv, job),
            DeterminismJsonOptions.Value);

        // Act — 1000 invocaciones paralelas vía Task.Run; todas deben
        // producir JSON byte-idéntico. Esto protege contra data races
        // (estáticos mutables, Random compartido, DateTime.UtcNow, etc.).
        var tasks = Enumerable.Range(0, 1000)
            .Select(_ => Task.Run(() => JsonSerializer.Serialize(
                ScoringEngine.ScoreV2(cv, job),
                DeterminismJsonOptions.Value)))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(json => json.Should().Be(firstJson));
    }

    [Fact]
    public void ScoreV2_NoNonDeterministicCalls_InCalcPath()
    {
        // Assert — verificación complementaria en tiempo de test: el
        // método ScoreV2 debe retornar el MISMO HashCode estructural
        // cuando se invoca dos veces seguidas (cheap fingerprint; el
        // guard real son las property-tests de arriba).
        var cv = CreateDeterministicCv();
        var job = CreateDeterministicJob();

        var result1 = ScoringEngine.ScoreV2(cv, job);
        var result2 = ScoringEngine.ScoreV2(cv, job);

        result1.OverallScore.Should().Be(result2.OverallScore);
        result1.Band.Should().Be(result2.Band);
        result1.PerSection.Experience.Should().Be(result2.PerSection.Experience);
        result1.PerSection.Education.Should().Be(result2.PerSection.Education);
        result1.PerSection.Skills.Should().Be(result2.PerSection.Skills);
        result1.PerSection.Certifications.Should().Be(result2.PerSection.Certifications);
        result1.PerSection.Contact.Should().Be(result2.PerSection.Contact);
        result1.RedFlags.Count.Should().Be(result2.RedFlags.Count);
        result1.Legacy.ContextHash.Should().Be(result2.Legacy.ContextHash);
        result1.Legacy.Overall.Should().Be(result2.Legacy.Overall);
    }

    private static readonly Lazy<JsonSerializerOptions> DeterminismJsonOptions = new(() =>
        new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

    private static CvDocument CreateDeterministicCv() => new(
        Basics: new Basics(
            Name: "Deterministic Test User",
            Email: "det.test@example.com",
            Phone: "+57 300 123 4567",
            Location: "Bogotá, Colombia",
            Url: "https://linkedin.com/in/det-test",
            Profiles: Array.Empty<ResumeProfile>(),
            Summary: "Backend determinista para tests de pureza.",
            DatosPersonales: null,
            Confidence: new BasicsConfidence(
                Name: ConfidenceMarker.Explicit,
                Email: ConfidenceMarker.Explicit,
                Phone: ConfidenceMarker.Explicit,
                Location: ConfidenceMarker.Inferred,
                Url: ConfidenceMarker.Inferred,
                Profiles: ConfidenceMarker.Inferred,
                Summary: ConfidenceMarker.Inferred,
                DatosPersonales: ConfidenceMarker.Inferred)),
        Work: new[]
        {
            new TaggedResumeWork(
                new ResumeWorkEntry(
                    Name: "TechCorp",
                    Position: "Senior Backend Developer",
                    StartDate: "2020-01",
                    EndDate: "2024-12",
                    Summary: "Diseño de sistemas distribuidos.",
                    Highlights: new[] { "Reducción de latencia 30%", "Liderazgo de equipo de 5" }),
                new WorkConfidence(
                    Name: ConfidenceMarker.Explicit,
                    Position: ConfidenceMarker.Explicit,
                    StartDate: ConfidenceMarker.Explicit,
                    EndDate: ConfidenceMarker.Explicit,
                    Summary: ConfidenceMarker.Inferred,
                    Highlights: ConfidenceMarker.Inferred)),
            new TaggedResumeWork(
                new ResumeWorkEntry(
                    Name: "StartupXYZ",
                    Position: "Backend Developer",
                    StartDate: "2018-03",
                    EndDate: "2019-12",
                    Summary: "APIs REST sobre .NET.",
                    Highlights: new[] { "Migración a microservicios" }),
                new WorkConfidence(
                    Name: ConfidenceMarker.Explicit,
                    Position: ConfidenceMarker.Explicit,
                    StartDate: ConfidenceMarker.Explicit,
                    EndDate: ConfidenceMarker.Explicit,
                    Summary: ConfidenceMarker.Inferred,
                    Highlights: ConfidenceMarker.Inferred)),
        },
        Education: new[]
        {
            new TaggedResumeEducation(
                new ResumeEducationEntry(
                    Institution: "Universidad de los Andes",
                    Area: "Ingeniería de Sistemas",
                    StudyType: "Pregrado",
                    StartDate: "2014-01",
                    EndDate: "2019-12",
                    Score: null),
                new EducationConfidence(
                    Institution: ConfidenceMarker.Explicit,
                    Area: ConfidenceMarker.Explicit,
                    StudyType: ConfidenceMarker.Explicit,
                    StartDate: ConfidenceMarker.Explicit,
                    EndDate: ConfidenceMarker.Explicit,
                    Score: ConfidenceMarker.Inferred)),
        },
        Skills: new[]
        {
            new TaggedResumeSkill(
                new ResumeSkillEntry("C#", "Senior"),
                new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred)),
            new TaggedResumeSkill(
                new ResumeSkillEntry(".NET", "Senior"),
                new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred)),
            new TaggedResumeSkill(
                new ResumeSkillEntry("SQL", null),
                new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred)),
            new TaggedResumeSkill(
                new ResumeSkillEntry("Docker", null),
                new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred)),
        },
        Projects: Array.Empty<TaggedResumeProject>(),
        Certificates: Array.Empty<TaggedResumeCertificate>(),
        Languages: Array.Empty<TaggedResumeLanguage>(),
        Meta: new CvMeta("2.0.0"));

    private static JobInput CreateDeterministicJob() => new(
        Title: "Senior Backend Developer",
        Requirements: new[] { "C#", ".NET", "SQL", "Docker" });
}
