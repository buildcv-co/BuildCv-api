using BuildCv.Application.Features.Payments;
using BuildCv.Infrastructure.Payments;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildCv.Infrastructure.Tests.Payments;

public sealed class PaymentsDependencyInjectionTests
{
    [Fact]
    public void InMemory_provider_resolves_InMemoryPaymentStore()
    {
        var (services, _) = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "InMemory",
            ["Wompi:Enabled"] = "false",
        });

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IPaymentStore>();

        store.Should().BeOfType<InMemoryPaymentStore>();
    }

    [Fact]
    public void Wompi_disabled_resolves_DisabledPaymentProvider()
    {
        var (services, _) = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "InMemory",
            ["Wompi:Enabled"] = "false",
        });

        var provider = services.BuildServiceProvider();
        var paymentProvider = provider.GetRequiredService<IPaymentProvider>();

        paymentProvider.Should().BeOfType<DisabledPaymentProvider>();
    }

    [Fact]
    public void Wompi_enabled_resolves_WompiAdapter()
    {
        var (services, _) = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "InMemory",
            ["Wompi:Enabled"] = "true",
            ["Wompi:PublicKey"] = "pub_test",
            ["Wompi:PrivateKey"] = "prv_test",
            ["Wompi:WebhookSecret"] = "secret",
        });

        var provider = services.BuildServiceProvider();
        var paymentProvider = provider.GetRequiredService<IPaymentProvider>();

        paymentProvider.Should().BeOfType<WompiAdapter>();
    }

    [Fact]
    public void Wompi_settings_are_bound()
    {
        var (services, configuration) = BuildServices(new Dictionary<string, string?>
        {
            ["Wompi:Enabled"] = "true",
            ["Wompi:Environment"] = "production",
            ["Wompi:PublicKey"] = "pub_prod",
            ["Wompi:PrivateKey"] = "prv_prod",
            ["Wompi:WebhookSecret"] = "prod-secret",
        });

        var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WompiSettings>>().Value;

        settings.Enabled.Should().BeTrue();
        settings.Environment.Should().Be("production");
        settings.PublicKey.Should().Be("pub_prod");
        settings.PrivateKey.Should().Be("prv_prod");
        settings.WebhookSecret.Should().Be("prod-secret");
        settings.BaseUrl.Should().Be("https://api.wompi.co");
        _ = configuration;
    }

    [Fact]
    public void Postgres_provider_resolves_EfPaymentStore()
    {
        var (services, _) = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "Postgres",
            ["Postgres:ConnectionString"] = "Host=localhost;Database=buildcv_test",
            ["Wompi:Enabled"] = "false",
        });

        services.AddDbContext<BuildCvDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();

        store.Should().BeOfType<EfPaymentStore>();
    }

    [Fact]
    public void Wompi_enabled_resolves_reconciliation_service_and_worker()
    {
        var (services, _) = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "InMemory",
            ["Wompi:Enabled"] = "true",
            ["Wompi:PublicKey"] = "pub_test",
            ["Wompi:PrivateKey"] = "prv_test",
            ["Wompi:WebhookSecret"] = "secret",
        });

        var provider = services.BuildServiceProvider();
        var reconciliation = provider.GetRequiredService<IPaymentReconciliationService>();
        var workers = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<PaymentReconciliationWorker>()
            .ToList();

        reconciliation.Should().BeOfType<PaymentReconciliationService>();
        workers.Should().HaveCount(1);
    }

    [Fact]
    public void Wompi_disabled_does_not_resolve_reconciliation_service_or_worker()
    {
        var (services, _) = BuildServices(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "InMemory",
            ["Wompi:Enabled"] = "false",
        });

        var provider = services.BuildServiceProvider();
        var workers = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<PaymentReconciliationWorker>()
            .ToList();

        workers.Should().BeEmpty();
        provider.GetService<IPaymentReconciliationService>().Should().BeNull();
    }

    private static (IServiceCollection services, IConfiguration configuration) BuildServices(
        Dictionary<string, string?> config)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
        services.AddInfrastructure(configuration);
        return (services, configuration);
    }
}
