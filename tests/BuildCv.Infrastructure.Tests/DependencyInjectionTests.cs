using BuildCv.Application.Features.Auth;
using BuildCv.Infrastructure.Auth;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildCv.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void InMemory_provider_resolves_InMemoryConsentStore()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "InMemory",
            })
            .Build();

        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IConsentStore>();

        store.Should().BeOfType<InMemoryConsentStore>();
    }

    [Fact]
    public void InMemory_provider_resolves_InMemoryUserDataStore()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "InMemory",
            })
            .Build();

        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IUserDataStore>();

        store.Should().BeOfType<InMemoryUserDataStore>();
    }

    [Fact]
    public void InMemory_provider_resolves_InMemoryRefreshTokenStore()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "InMemory",
            })
            .Build();

        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IRefreshTokenStore>();

        store.Should().BeOfType<InMemoryRefreshTokenStore>();
    }

    [Fact]
    public void InMemory_provider_resolves_IUserDataService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "InMemory",
            })
            .Build();

        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IUserDataService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public void Postgres_provider_resolves_EfConsentStore()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Postgres",
                ["Postgres:ConnectionString"] = "Host=localhost;Database=buildcv_test",
            })
            .Build();

        services.AddDbContext<BuildCvDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IConsentStore>();

        store.Should().BeOfType<EfConsentStore>();
    }

    [Fact]
    public void Postgres_provider_resolves_EfUserDataStore()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Postgres",
                ["Postgres:ConnectionString"] = "Host=localhost;Database=buildcv_test",
            })
            .Build();

        services.AddDbContext<BuildCvDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IUserDataStore>();

        store.Should().BeOfType<EfUserDataStore>();
    }

    [Fact]
    public void Postgres_provider_resolves_EfRefreshTokenStore()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Postgres",
                ["Postgres:ConnectionString"] = "Host=localhost;Database=buildcv_test",
            })
            .Build();

        services.AddDbContext<BuildCvDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IRefreshTokenStore>();

        store.Should().BeOfType<EfRefreshTokenStore>();
    }

    [Fact]
    public void Postgres_provider_resolves_IUserDataService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Postgres",
                ["Postgres:ConnectionString"] = "Host=localhost;Database=buildcv_test",
            })
            .Build();

        services.AddDbContext<BuildCvDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserDataService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public void Default_provider_resolves_InMemory_stores()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var consentStore = provider.GetRequiredService<IConsentStore>();
        var userDataStore = provider.GetRequiredService<IUserDataStore>();
        var refreshTokenStore = provider.GetRequiredService<IRefreshTokenStore>();

        consentStore.Should().BeOfType<InMemoryConsentStore>();
        userDataStore.Should().BeOfType<InMemoryUserDataStore>();
        refreshTokenStore.Should().BeOfType<InMemoryRefreshTokenStore>();
    }

    [Fact]
    public void Postgres_settings_are_bound()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "InMemory",
                ["Postgres:ConnectionString"] = "Host=localhost;Database=buildcv_test",
                ["Postgres:EnableAutoMigrate"] = "true",
            })
            .Build();

        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PostgresSettings>>().Value;

        settings.ConnectionString.Should().Be("Host=localhost;Database=buildcv_test");
        settings.EnableAutoMigrate.Should().BeTrue();
    }
}
