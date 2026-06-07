using BuildCv.Domain.Common;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_es_exitoso_y_sin_error()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_transporta_el_error()
    {
        var error = new Error("cv.vacio", "El CV está vacío.");

        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Success_con_valor_expone_el_valor()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Leer_el_valor_de_un_resultado_fallido_lanza()
    {
        var result = Result.Failure<int>(new Error("x", "y"));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }
}
