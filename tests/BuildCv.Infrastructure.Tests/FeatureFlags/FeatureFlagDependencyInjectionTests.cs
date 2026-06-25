using BuildCv.Application.Common;
using BuildCv.Infrastructure.Credits;
using BuildCv.Infrastructure.FeatureFlags;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildCv.Infrastructure.Tests.FeatureFlags;

public sealed class FeatureFlagDependencyInjectionTests
{
    [Fact]
    public void Postgres_provider_registers_IFeatureFlagStore_as_EfFeatureFlagStore()
    {
        var (sp, _) = BuildServices("Postgres");

        using var scope = sp.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFeatureFlagStore>();

        store.Should().BeOfType<EfFeatureFlagStore>();
    }

    [Fact]
    public void Postgres_provider_registers_IFeatureFlag_as_CachingFeatureFlagDecorator()
    {
        var (sp, _) = BuildServices("Postgres");

        using var scope = sp.CreateScope();
        var flags = scope.ServiceProvider.GetRequiredService<IFeatureFlag>();

        flags.Should().BeOfType<CachingFeatureFlagDecorator>();
    }

    [Fact]
    public void Postgres_provider_registers_IFeatureFlagAdminService_and_HostedService()
    {
        var (sp, _) = BuildServices("Postgres");

        using var scope = sp.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<IFeatureFlagAdminService>();
        admin.Should().BeOfType<FeatureFlagAdminService>();

        var hosted = sp.GetServices<IHostedService>().OfType<FeatureFlagMigrationService>().ToList();
        hosted.Should().ContainSingle("migration service must run on startup");
    }

    [Fact]
    public void InMemory_provider_registers_InMemoryFeatureFlagStore_and_CachingFeatureFlagDecorator()
    {
        var (sp, _) = BuildServices("InMemory");

        using var scope = sp.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFeatureFlagStore>();
        var flags = scope.ServiceProvider.GetRequiredService<IFeatureFlag>();

        store.Should().BeOfType<InMemoryFeatureFlagStore>();
        flags.Should().BeOfType<CachingFeatureFlagDecorator>();
    }

    [Fact]
    public void ICreditsFeatureFlag_is_registered_as_FeatureFlagCreditsAdapter()
    {
        var (sp, _) = BuildServices("InMemory");

        var creditsFlag = sp.GetRequiredService<ICreditsFeatureFlag>();

        creditsFlag.Should().BeOfType<FeatureFlagCreditsAdapter>();
    }

    [Fact]
    public void FeatureFlags_options_are_bound_from_configuration()
    {
        var (sp, _) = BuildServices("InMemory");

        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FeatureFlagsOptions>>().Value;

        options.CacheTtlSeconds.Should().Be(60);
        options.Defaults.Should().ContainKey("factus-enabled");
        options.Defaults["factus-enabled"].Should().BeFalse();
        options.Defaults["wompi-enabled"].Should().BeTrue();
        options.Defaults["credits-enabled"].Should().BeTrue();
    }

    private static (ServiceProvider sp, IConfiguration _) BuildServices(string persistence)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = persistence,
                ["Postgres:ConnectionString"] = "Host=localhost;Database=buildcv_test",
                ["FeatureFlags:CacheTtlSeconds"] = "60",
                ["FeatureFlags:Defaults:factus-enabled"] = "false",
                ["FeatureFlags:Defaults:wompi-enabled"] = "true",
                ["FeatureFlags:Defaults:credits-enabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return (services.BuildServiceProvider(), configuration);
    }
}
