using BuildCv.Application.Features.Jobs;
using BuildCv.Application.Features.Scoring;
using BuildCv.Domain.Resumes;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BuildCv.Application.Tests.Features;

public sealed class ScoreCvValidatorTests
{
    private readonly ScoreCvValidator _sut = new();

    [Fact]
    public void Rechaza_un_cv_demasiado_corto_en_v1()
    {
        var command = new TextScoreCommand(CvText: "muy corto", JobText: new string('a', 150));

        _sut.Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rechaza_una_vacante_demasiado_corta_en_v1()
    {
        var command = new TextScoreCommand(CvText: new string('a', 300), JobText: "corta");

        _sut.Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Acepta_textos_dentro_de_los_limites_en_v1()
    {
        var command = new TextScoreCommand(CvText: new string('a', 300), JobText: new string('b', 150));

        _sut.Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EngineVersion_fuera_del_enum_se_rechaza_con_codigo_desconocido()
    {
        // Creamos un command con EngineVersion inválido (vía cast a la base).
        var command = new TextScoreCommand("cv", "job") with { EngineVersion = "0.9.0" };
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.EngineVersion)
            .WithErrorCode("ENGINE_VERSION_UNKNOWN");
    }

    [Fact]
    public void Rama_v2_rechaza_comando_v1_mezclado_con_VERSION_MISMATCH()
    {
        // El engineVersion es v2 pero la forma es TextScoreCommand → VERSION_MISMATCH.
        var command = new TextScoreCommand(new string('a', 300), new string('b', 150))
            with
        { EngineVersion = "2.0.0" };
        var result = _sut.TestValidate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rama_v1_rechaza_comando_v2_mezclado_con_VERSION_MISMATCH()
    {
        // CvDocument y JobSpec válidos pero engineVersion v1 → VERSION_MISMATCH.
        var cv = BuildMinimalCv();
        var job = BuildValidJobSpec();
        var command = new StructuredScoreCommand(cv, job) with { EngineVersion = "1.0.0" };
        var result = _sut.TestValidate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rama_v2_acepta_un_StructuredScoreCommand_con_JobSpec_válido()
    {
        var cv = BuildMinimalCv();
        var job = BuildValidJobSpec();
        var command = new StructuredScoreCommand(cv, job);
        var result = _sut.TestValidate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rama_v2_rechaza_un_JobSpec_invalido_a_través_del_validator_anidado()
    {
        var cv = BuildMinimalCv();
        var jobInvalido = BuildValidJobSpec() with { Title = new string('a', 201) };
        var command = new StructuredScoreCommand(cv, jobInvalido);
        var result = _sut.TestValidate(command);
        result.IsValid.Should().BeFalse();
    }

    private static CvDocument BuildMinimalCv() => new(
        Basics: new Basics(
            Name: "Ada Lovelace",
            Email: "ada@example.com",
            Phone: null,
            Location: null,
            Url: null,
            Profiles: Array.Empty<ResumeProfile>(),
            Summary: null,
            DatosPersonales: null,
            Confidence: new BasicsConfidence(
                Name: ConfidenceMarker.Explicit,
                Email: ConfidenceMarker.Explicit,
                Phone: ConfidenceMarker.Inferred,
                Location: ConfidenceMarker.Inferred,
                Url: ConfidenceMarker.Inferred,
                Profiles: ConfidenceMarker.Inferred,
                Summary: ConfidenceMarker.Inferred,
                DatosPersonales: ConfidenceMarker.Inferred)),
        Work: Array.Empty<TaggedResumeWork>(),
        Education: Array.Empty<TaggedResumeEducation>(),
        Skills: Array.Empty<TaggedResumeSkill>(),
        Projects: Array.Empty<TaggedResumeProject>(),
        Certificates: Array.Empty<TaggedResumeCertificate>(),
        Languages: Array.Empty<TaggedResumeLanguage>(),
        Meta: new CvMeta(EngineVersion: "2.0.0"));

    private static JobSpec BuildValidJobSpec() => new(
        Title: "Senior Backend Engineer",
        Company: "Acme S.A.",
        Description: "Buscamos ingeniero backend con experiencia en .NET 10.",
        Location: "Bogotá, Colombia",
        EmploymentType: EmploymentType.FullTime,
        Requirements: new[] { "5 años de experiencia en C#", "PostgreSQL" });
}
