using BuildCv.Application;
using BuildCv.Application.Features.Iterations;
using BuildCv.Infrastructure;
using BuildCv.Infrastructure.Iterations;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildCv.Infrastructure.Tests.Iterations;

public sealed class IterationDependencyInjectionTests
{
    [Fact]
    public void Registers_EfIterationStore_and_cleanup_worker_in_Postgres_branch()
    {
        var services = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "Postgres",
            ["Postgres:ConnectionString"] = "Host=localhost;Database=ignored",
        });

        using var provider = services.BuildServiceProvider();

        provider.GetService<IIterationStore>().Should().BeOfType<EfIterationStore>();
        provider.GetService<IIterationCleanupCapable>().Should().BeOfType<EfIterationStore>();
        var hosted = provider.GetServices<IHostedService>().OfType<IterationCleanupWorker>().ToList();
        hosted.Should().HaveCount(1);
    }

    [Fact]
    public void Registers_InMemoryIterationStore_for_inmemory_provider()
    {
        var services = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "InMemory",
        });

        using var provider = services.BuildServiceProvider();

        provider.GetService<IIterationStore>().Should().BeOfType<InMemoryIterationStore>();
        provider.GetService<IIterationCleanupCapable>().Should().BeOfType<InMemoryIterationStore>();
    }

    [Fact]
    public void Registers_iteration_service_and_handlers_in_both_branches()
    {
        var postgres = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "Postgres",
            ["Postgres:ConnectionString"] = "Host=localhost;Database=ignored",
        });
        using (var pgProvider = postgres.BuildServiceProvider())
        {
            pgProvider.GetService<IIterationService>().Should().NotBeNull();
            pgProvider.GetService<IterateAdaptationHandler>().Should().NotBeNull();
            pgProvider.GetService<GetIterationResultHandler>().Should().NotBeNull();
        }

        var inMemory = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "InMemory",
        });
        using var memProvider = inMemory.BuildServiceProvider();
        memProvider.GetService<IIterationService>().Should().NotBeNull();
        memProvider.GetService<IterateAdaptationHandler>().Should().NotBeNull();
        memProvider.GetService<GetIterationResultHandler>().Should().NotBeNull();
    }

    private static IServiceCollection BuildServices(IDictionary<string, string?> configValues)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddApplication();
        services.AddInfrastructure(config);
        return services;
    }
}
