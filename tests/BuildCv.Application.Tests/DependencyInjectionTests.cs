using BuildCv.Application;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BuildCv.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_es_encadenable()
    {
        var services = new ServiceCollection();

        var result = services.AddApplication();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddApplication_permite_construir_el_contenedor()
    {
        using var provider = new ServiceCollection()
            .AddApplication()
            .BuildServiceProvider();

        provider.Should().NotBeNull();
    }
}
