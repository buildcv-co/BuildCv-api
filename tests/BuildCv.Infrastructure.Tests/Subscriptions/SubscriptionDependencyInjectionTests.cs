using BuildCv.Application.Features.Subscriptions;
using BuildCv.Infrastructure;
using BuildCv.Infrastructure.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildCv.Infrastructure.Tests.Subscriptions;

public sealed class SubscriptionDependencyInjectionTests
{
    [Fact]
    public void Registers_all_subscription_ports_in_default_configuration()
    {
        var services = BuildServices(new Dictionary<string, string?>());

        using var provider = services.BuildServiceProvider();

        provider.GetService<ISubscriptionStore>().Should().NotBeNull();
        provider.GetService<ISubscriptionFeatureFlag>().Should().NotBeNull();
        provider.GetService<ISubscriptionProvider>().Should().NotBeNull();
        provider.GetService<HandleRecurringChargeHandler>().Should().NotBeNull();
        provider.GetService<ProcessRetriesHandler>().Should().NotBeNull();
    }

    [Fact]
    public void Registers_SubscriptionReconciliationWorker_as_IHostedService()
    {
        var services = BuildServices(new Dictionary<string, string?>());

        using var provider = services.BuildServiceProvider();

        var hosted = provider.GetServices<IHostedService>().OfType<SubscriptionReconciliationWorker>().ToList();
        hosted.Should().HaveCount(1);
    }

    [Fact]
    public void FeatureFlag_disabled_by_default()
    {
        var services = BuildServices(new Dictionary<string, string?>());

        using var provider = services.BuildServiceProvider();

        var flag = provider.GetRequiredService<ISubscriptionFeatureFlag>();
        flag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void FeatureFlag_enabled_when_configured()
    {
        var services = BuildServices(new Dictionary<string, string?>
        {
            ["SubscriptionRecurring:Enabled"] = "true",
        });

        using var provider = services.BuildServiceProvider();

        var flag = provider.GetRequiredService<ISubscriptionFeatureFlag>();
        flag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Registers_in_memory_store_for_inmemory_provider()
    {
        var services = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "InMemory",
        });

        using var provider = services.BuildServiceProvider();

        provider.GetService<ISubscriptionStore>().Should().BeOfType<InMemorySubscriptionStore>();
    }

    private static IServiceCollection BuildServices(IDictionary<string, string?> configValues)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);
        return services;
    }
}
