using BuildCv.Application.Features.Scoring;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features;

public sealed class ScoreCvValidatorTests
{
    private readonly ScoreCvValidator _sut = new();

    [Fact]
    public void Rechaza_un_cv_demasiado_corto()
    {
        var command = new ScoreCvCommand(CvText: "muy corto", JobText: new string('a', 150));

        _sut.Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rechaza_una_vacante_demasiado_corta()
    {
        var command = new ScoreCvCommand(CvText: new string('a', 300), JobText: "corta");

        _sut.Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Acepta_textos_dentro_de_los_limites()
    {
        var command = new ScoreCvCommand(CvText: new string('a', 300), JobText: new string('b', 150));

        _sut.Validate(command).IsValid.Should().BeTrue();
    }
}
